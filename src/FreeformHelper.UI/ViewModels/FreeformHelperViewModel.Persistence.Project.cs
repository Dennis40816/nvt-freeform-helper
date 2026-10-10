using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using NLog;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages project save/load operations.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    internal Task<bool>? TryStartSaveProjectCommand()
    {
        if (SaveProjectCommand.IsRunning || !SaveProjectCommand.CanExecute(null))
        {
            return null;
        }

        // The save command preserves the Task<bool> returned by SaveProjectAsync.
        return (Task<bool>)SaveProjectCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Asynchronously saves the current project state to a file.
    /// This includes current settings, pad overrides, UI snapshot, and optionally embeds DXF data.
    /// </summary>
    /// <returns>True if the project was saved successfully, false otherwise.</returns>
    public async Task<bool> SaveProjectAsync()
    {
        if (!IsProjectEditingEnabled) return false;

        var status = CreateStatusScope("SaveProject");
        if (PickSaveProjectPathAsync is null)
        {
            status.ReportBlocked("Save project: dialog handler not wired.");
            return false;
        }

        return await RunUiOperationAsync(
            operationName: "Save project",
            showModalSpinner: false,
            onStart: () => Logger.Info(CultureInfo.InvariantCulture, "Saving project."),
            operationAsync: async () =>
            {
                CaptureDxfEditProjectState(_projectFile);
                var result = await _projectPersistenceUseCase.SaveAsync(new ProjectSaveRequest(
                    Project: _projectFile,
                    Grid: _grid,
                    CadPadCustomValues: _cadPadCustomValues,
                    PickSaveProjectPathAsync: PickSaveProjectPathAsync,
                    ConfirmEmbedDxfAsync: ConfirmEmbedDxfAsync,
                    BuildUiSnapshot: BuildUiSnapshot,
                    ApplyUiToSettings: ApplyUiToSettings,
                    HasCadLoaded: _cad is not null));

                if (result.IsCancelled)
                {
                    return false;
                }

                var successMessage = result.EmbedWarning is null
                    ? $"Project saved: {result.Path}"
                    : $"Project saved: {result.Path}. {result.EmbedWarning}";
                status.ReportSuccess(successMessage);
                Logger.Info(CultureInfo.InvariantCulture, "Project saved: {0}", result.Path);
                if (!string.IsNullOrWhiteSpace(result.EmbedWarning))
                {
                    Logger.Warn(result.EmbedWarning);
                }

                HasUnsavedChanges = false;
                _lastSavedPath = result.Path;
                FlushDeferredAppGeneralSettingsIfNeeded();
                return true;
            },
            onError: ex =>
            {
                status.ReportError("Save project failed", ex);
                Logger.Error(ex, "Save project failed.");
                return false;
            });
    }

    /// <summary>
    /// Asynchronously loads a project from a file.
    /// This restores settings, pad overrides, UI snapshot, and optionally reloads DXF data.
    /// </summary>
    private async Task LoadProjectAsync()
    {
        _ = await RunProjectLoadAsync(async () =>
        {
            var status = CreateStatusScope("LoadProject");
            if (PickLoadProjectPathAsync is null)
            {
                status.ReportBlocked("Load project: dialog handler not wired.");
                return ProjectLoadCommandResult.Failed("LOAD_FAILED", "Load project: dialog handler not wired.");
            }

            var path = await PickLoadProjectPathAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return ProjectLoadCommandResult.Failed("CANCELLED", "Load cancelled.");
            }

            return await LoadProjectFromPathCoreAsync(path);
        });
    }

    internal Task<ProjectLoadCommandResult> LoadProjectFromPathAsync(string path)
    {
        return RunProjectLoadAsync(() => LoadProjectFromPathCoreAsync(path));
    }

    private async Task<ProjectLoadCommandResult> LoadProjectFromPathCoreAsync(string path)
    {
        var status = CreateStatusScope("LoadProject");
        if (string.IsNullOrWhiteSpace(path))
        {
            return ProjectLoadCommandResult.Failed(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--path' is required.");
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            return ProjectLoadCommandResult.Failed(
                code: "INVALID_ARGUMENTS",
                message: $"Invalid project path '{path}': {ex.Message}");
        }

        if (!File.Exists(normalizedPath))
        {
            return ProjectLoadCommandResult.Failed(
                code: "PATH_NOT_FOUND",
                message: $"Project file not found: {normalizedPath}",
                path: normalizedPath);
        }

        var commandResult = ProjectLoadCommandResult.Failed(
            code: "LOAD_FAILED",
            message: $"Load project failed: {normalizedPath}",
            path: normalizedPath);

        await RunUiOperationAsync(
            operationName: "Load project",
            showModalSpinner: true,
            onStart: () =>
            {
                Logger.Info(CultureInfo.InvariantCulture, "Loading project from path: {0}", normalizedPath);
                Logger.Debug(CultureInfo.InvariantCulture, "PERF LOAD_PROJECT: stage=start path={0}.", normalizedPath);
            },
            operationAsync: async () =>
            {
                var loadSw = Stopwatch.StartNew();
                var result = await _projectPersistenceUseCase.LoadAsync(new ProjectLoadRequest(
                    PickLoadProjectPathAsync: () => Task.FromResult<string?>(normalizedPath),
                    CanProceedWithPendingEdits));
                loadSw.Stop();

                if (result.IsCancelled || result.Project is null || string.IsNullOrWhiteSpace(result.Path))
                {
                    Logger.Debug(CultureInfo.InvariantCulture, "PERF LOAD_PROJECT: stage=cancelled path={0}.", normalizedPath);
                    commandResult = ProjectLoadCommandResult.Failed(
                        code: "CANCELLED",
                        message: "Load cancelled (pending edits were not committed).",
                        path: normalizedPath);
                    return;
                }

                var timings = await ApplyLoadedProjectStateAsync(result.Project, result.Path, loadSw.ElapsedMilliseconds);
                commandResult = ProjectLoadCommandResult.Loaded(
                    path: result.Path,
                    totalElapsedMs: timings.TotalElapsedMs,
                    persistenceElapsedMs: loadSw.ElapsedMilliseconds,
                    applyStateElapsedMs: timings.ApplyStateElapsedMs,
                    dxfElapsedMs: timings.DxfElapsedMs,
                    rebuildElapsedMs: timings.RebuildElapsedMs,
                    step1ReplayElapsedMs: timings.Step1ReplayElapsedMs,
                    step2ReplayElapsedMs: timings.Step2ReplayElapsedMs,
                    step1Replayed: timings.Step1Replayed,
                    step2Replayed: timings.Step2Replayed);
            },
            onError: ex =>
            {
                status.ReportError("Load project failed", ex);
                Logger.Error(ex, "Load project failed.");
                commandResult = ProjectLoadCommandResult.Failed(
                    code: "LOAD_FAILED",
                    message: ex.Message,
                    path: normalizedPath);
            });

        return commandResult;
    }

    private async Task<ProjectLoadApplyTiming> ApplyLoadedProjectStateAsync(ProjectFile file, string path, long persistenceElapsedMs)
    {
        var status = CreateStatusScope("LoadProject");
        var totalSw = Stopwatch.StartNew();
        Logger.Info(CultureInfo.InvariantCulture, "Loading project: {0}", path);
        var originalSuppressAppGeneralPersistence = _suppressAppGeneralPersistence;
        _suppressAppGeneralPersistence = true;
        _projectFile = file;
        InvalidateWorkflowDataSnapshot();
        _lastSavedPath = path;
        try
        {
            using var sourceChangeBatch = BeginSimulationWorkspaceSourceChangeBatch();
            var applyStateSw = Stopwatch.StartNew();
            _cadPadCustomValues.Clear();
            foreach (var kv in file.CadPadCustomValues)
            {
                _cadPadCustomValues[kv.Key] = kv.Value;
            }

            LoadSettingsToUi(file.Settings);
            ApplyViewSnapshot(file.UiSnapshot.View);
            ApplyImportSnapshot(file.UiSnapshot.Import, file);
            TryApplyAppGeneralVisualPreferencesAfterProjectLoad();
            ClearStep5ResultCore();
            status.ReportSuccess($"Project loaded: {path}");
            Logger.Info(CultureInfo.InvariantCulture, "Project loaded: {0}", path);
            HasUnsavedChanges = false;
            MarkProjectLoadedForAppGeneralPersistence();
            applyStateSw.Stop();

            var dxfElapsedMs = 0L;
            var rebuildElapsedMs = 0L;
            var replayTiming = ProjectLoadReplayTiming.Empty;

            var wasLoading = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                _cad = null;
                ResetDxfEditBaseline(null);
                _cachedFilteredCadForBuild = null;
                ClearDxfLayerCatalog();
                CadPads = new ObservableCollection<CadPad>();
                foreach (var toggle in LayerToggles)
                {
                    toggle.PropertyChanging -= OnLayerToggleChanging;
                    toggle.PropertyChanged -= OnLayerToggleChanged;
                }

                LayerToggles.Clear();

                await RunWithCadLoadCanvasOverlayAsync(async () =>
                {
                    var dxfOptions = BuildDxfOptions();
                    var dxfLoadSw = Stopwatch.StartNew();
                    var outcome = await Task.Run(() => _cadLoadUseCase.TryLoadFromProject(file.LastDxfPath, file.EmbeddedDxf, dxfOptions));
                    dxfLoadSw.Stop();
                    dxfElapsedMs = dxfLoadSw.ElapsedMilliseconds;
                    if (outcome is not null)
                    {
                        var persistedDxfEditState = CapturePersistedDxfEditProjectState(file);
                        if (outcome.Source == CadLoadSource.Path && !string.IsNullOrWhiteSpace(outcome.SourcePath))
                        {
                            await TryLoadDxfLayerCatalogFromPathAsync(outcome.SourcePath);
                        }
                        else
                        {
                            await TryLoadDxfLayerCatalogFromEmbeddedAsync(file.EmbeddedDxf);
                        }

                        await ApplyCadLoadOutcomeAsync(outcome, file.UiSnapshot.View);
                        ApplyImportSnapshot(file.UiSnapshot.Import, file);
                        ApplyCapturedDxfEditProjectState(persistedDxfEditState);
                        status.ReportProgress(BuildCadLoadStatus(outcome, CadLoadContext.LoadProject, path));
                        LogCadLoadOutcome(outcome, CadLoadContext.LoadProject);
                    }

                    var rebuildSw = Stopwatch.StartNew();
                    await TriggerGridRebuildAsync(requestFit: true);
                    rebuildSw.Stop();
                    rebuildElapsedMs = rebuildSw.ElapsedMilliseconds;

                    replayTiming = await ReplayWorkflowStepsAfterProjectLoadAsync();
                });
            }
            finally
            {
                _isLoadingSettings = wasLoading;
            }

            totalSw.Stop();
            Logger.Info(CultureInfo.InvariantCulture, "Load project timing: total={0} ms, persistence={1} ms, apply-state={2} ms, dxf={3} ms, rebuild={4} ms.",
                totalSw.ElapsedMilliseconds,
                persistenceElapsedMs,
                applyStateSw.ElapsedMilliseconds,
                dxfElapsedMs,
                rebuildElapsedMs);
            Logger.Info(CultureInfo.InvariantCulture, "Load project timing detail: step1-auto-replay={0} ms (replayed={1}).", replayTiming.Step1ReplayElapsedMs, replayTiming.Step1Replayed);
            Logger.Info(CultureInfo.InvariantCulture, "Load project timing detail: step2-auto-replay={0} ms (replayed={1}).", replayTiming.Step2ReplayElapsedMs, replayTiming.Step2Replayed);
            Logger.Debug(CultureInfo.InvariantCulture, "PERF LOAD_PROJECT SUMMARY: total={0}ms persistence={1}ms applyState={2}ms dxf={3}ms rebuild={4}ms step1={5}ms step2={6}ms step1Replayed={7} step2Replayed={8} path={9}",
                totalSw.ElapsedMilliseconds,
                persistenceElapsedMs,
                applyStateSw.ElapsedMilliseconds,
                dxfElapsedMs,
                rebuildElapsedMs,
                replayTiming.Step1ReplayElapsedMs,
                replayTiming.Step2ReplayElapsedMs,
                replayTiming.Step1Replayed ? "Y" : "N",
                replayTiming.Step2Replayed ? "Y" : "N",
                path);

            return new ProjectLoadApplyTiming(
                TotalElapsedMs: totalSw.ElapsedMilliseconds,
                ApplyStateElapsedMs: applyStateSw.ElapsedMilliseconds,
                DxfElapsedMs: dxfElapsedMs,
                RebuildElapsedMs: rebuildElapsedMs,
                Step1ReplayElapsedMs: replayTiming.Step1ReplayElapsedMs,
                Step2ReplayElapsedMs: replayTiming.Step2ReplayElapsedMs,
                Step1Replayed: replayTiming.Step1Replayed,
                Step2Replayed: replayTiming.Step2Replayed);
        }
        finally
        {
            _suppressAppGeneralPersistence = originalSuppressAppGeneralPersistence;
        }
    }

    private async Task<ProjectLoadReplayTiming> ReplayWorkflowStepsAfterProjectLoadAsync()
    {
        if (!AutoReplayStep2AfterProjectLoad)
        {
            Logger.Info(WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(
                WorkflowStepId.Step2Freeform,
                "disabled by setting"));
        }

        var step1ElapsedMs = 0L;
        var step2ElapsedMs = 0L;
        var step1Replayed = false;
        var step2Replayed = false;
        var replayPlan = WorkflowPipelineService.BuildProjectLoadReplayPlan(AutoReplayStep2AfterProjectLoad);

        foreach (var step in replayPlan)
        {
            Logger.Info(WorkflowPipelineService.GetProjectLoadReplayStartedMessage(step));
            var stepSw = Stopwatch.StartNew();
            var replayed = step switch
            {
                WorkflowStepId.Step1Match => await RestoreStep1MatchAfterProjectLoadAsync(),
                WorkflowStepId.Step2Freeform => await RestoreStep2FreeformAfterProjectLoadAsync(),
                _ => false
            };
            stepSw.Stop();

            if (step == WorkflowStepId.Step1Match)
            {
                step1ElapsedMs = stepSw.ElapsedMilliseconds;
                step1Replayed = replayed;
            }
            else if (step == WorkflowStepId.Step2Freeform)
            {
                step2ElapsedMs = stepSw.ElapsedMilliseconds;
                step2Replayed = replayed;
            }

            if (replayed)
            {
                Logger.Info(WorkflowPipelineService.GetProjectLoadReplayCompletedMessage(step));
            }
            else
            {
                Logger.Info(WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(step, "preconditions not met"));
            }
        }

        return new ProjectLoadReplayTiming(
            Step1ReplayElapsedMs: step1ElapsedMs,
            Step2ReplayElapsedMs: step2ElapsedMs,
            Step1Replayed: step1Replayed,
            Step2Replayed: step2Replayed);
    }
}

internal sealed record ProjectLoadCommandResult(
    bool IsLoaded,
    string Code,
    string Message,
    string? Path,
    long TotalElapsedMs,
    long PersistenceElapsedMs,
    long ApplyStateElapsedMs,
    long DxfElapsedMs,
    long RebuildElapsedMs,
    long Step1ReplayElapsedMs,
    long Step2ReplayElapsedMs,
    bool Step1Replayed,
    bool Step2Replayed)
{
    public static ProjectLoadCommandResult Loaded(
        string path,
        long totalElapsedMs,
        long persistenceElapsedMs,
        long applyStateElapsedMs,
        long dxfElapsedMs,
        long rebuildElapsedMs,
        long step1ReplayElapsedMs,
        long step2ReplayElapsedMs,
        bool step1Replayed,
        bool step2Replayed)
    {
        return new ProjectLoadCommandResult(
            IsLoaded: true,
            Code: "OK",
            Message: $"Project loaded: {path}",
            Path: path,
            TotalElapsedMs: totalElapsedMs,
            PersistenceElapsedMs: persistenceElapsedMs,
            ApplyStateElapsedMs: applyStateElapsedMs,
            DxfElapsedMs: dxfElapsedMs,
            RebuildElapsedMs: rebuildElapsedMs,
            Step1ReplayElapsedMs: step1ReplayElapsedMs,
            Step2ReplayElapsedMs: step2ReplayElapsedMs,
            Step1Replayed: step1Replayed,
            Step2Replayed: step2Replayed);
    }

    public static ProjectLoadCommandResult Failed(string code, string message, string? path = null)
    {
        return new ProjectLoadCommandResult(
            IsLoaded: false,
            Code: code,
            Message: message,
            Path: path,
            TotalElapsedMs: 0,
            PersistenceElapsedMs: 0,
            ApplyStateElapsedMs: 0,
            DxfElapsedMs: 0,
            RebuildElapsedMs: 0,
            Step1ReplayElapsedMs: 0,
            Step2ReplayElapsedMs: 0,
            Step1Replayed: false,
            Step2Replayed: false);
    }
}

internal sealed record ProjectLoadApplyTiming(
    long TotalElapsedMs,
    long ApplyStateElapsedMs,
    long DxfElapsedMs,
    long RebuildElapsedMs,
    long Step1ReplayElapsedMs,
    long Step2ReplayElapsedMs,
    bool Step1Replayed,
    bool Step2Replayed);

internal sealed record ProjectLoadReplayTiming(
    long Step1ReplayElapsedMs,
    long Step2ReplayElapsedMs,
    bool Step1Replayed,
    bool Step2Replayed)
{
    public static ProjectLoadReplayTiming Empty => new(
        Step1ReplayElapsedMs: 0,
        Step2ReplayElapsedMs: 0,
        Step1Replayed: false,
        Step2Replayed: false);
}

