// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.UseCases.ProjectLoad;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages project save/load operations.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    internal Task<bool>? TryStartSaveProjectCommandAsync()
        => _projectCommands.TryStartSaveAsync();

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
            onStart: () => _logger.Info(CultureInfo.InvariantCulture, "Saving project."),
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
                _logger.Info(CultureInfo.InvariantCulture, "Project saved: {0}", result.Path);
                if (!string.IsNullOrWhiteSpace(result.EmbedWarning))
                {
                    _logger.Warn(result.EmbedWarning);
                }

                HasUnsavedChanges = false;
                _projectSession.LastSavedPath = result.Path;
                FlushDeferredAppGeneralSettingsIfNeeded();
                return true;
            },
            onError: ex =>
            {
                status.ReportError("Save project failed", ex);
                _logger.Error(ex, "Save project failed.");
                return false;
            });
    }

    /// <summary>
    /// Asynchronously loads a project from a file.
    /// This restores settings, pad overrides, UI snapshot, and optionally reloads DXF data.
    /// </summary>
    private async Task LoadProjectAsync()
    {
        ReplaceProjectPlan? plan = null;
        _ = await RunProjectLoadAsync(async () =>
        {
            var status = CreateStatusScope("LoadProject");
            if (PickLoadProjectPathAsync is null)
            {
                status.ReportBlocked("Load project: dialog handler not wired.");
                return ProjectLoadCommandResult.Failed("LOAD_FAILED", "Load project: dialog handler not wired.");
            }

            plan = await ReplaceProjectWorkflow.PrepareAsync(
                PickLoadProjectPathAsync,
                HasUnsavedProjectWork,
                TryBackUpUnsavedProjectAsync,
                () => ProjectDialogs.ConfirmSaveBeforeLoadAsync);
            if (plan.Outcome != ReplaceProjectOutcome.Load)
            {
                if (plan.Outcome == ReplaceProjectOutcome.Cancel && plan.Path is not null)
                {
                    status.ReportBlocked(plan.Message);
                }

                return ProjectLoadCommandResult.Failed("CANCELLED", plan.Message, plan.Path);
            }

            return await LoadProjectFromPathCoreAsync(plan.Path!);
        });

        if (plan?.Outcome != ReplaceProjectOutcome.SaveThenLoad)
        {
            return;
        }

        if (!await ReplaceProjectWorkflow.SaveThenLoadAsync(
            plan.Path!, plan.BackupPath, SaveProjectAsync, UnsavedProjectBackup.TryDelete,
            async path => { _ = await LoadProjectFromPathAsync(path); }))
        {
            CreateStatusScope("LoadProject").ReportBlocked("Load cancelled: the project was not saved, so unsaved changes were kept.");
        }
    }

    // Writes the current state to the one backup file. A failed backup is reported and never blocks the load prompt.
    private async Task<string?> TryBackUpUnsavedProjectAsync()
    {
        var backupPath = UnsavedProjectBackup.PathFor(_appGeneralSettingsStore.SettingsPath);
        try
        {
            CaptureDxfEditProjectState(_projectFile);
            _ = await _projectPersistenceUseCase.SaveAsync(new ProjectSaveRequest(
                Project: _projectFile,
                Grid: _grid,
                CadPadCustomValues: _cadPadCustomValues,
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(backupPath),
                ConfirmEmbedDxfAsync: null,
                BuildUiSnapshot: BuildUiSnapshot,
                ApplyUiToSettings: ApplyUiToSettings,
                HasCadLoaded: _cad is not null));
            _logger.Info(CultureInfo.InvariantCulture, "Unsaved project backed up: {0}", backupPath);
            return backupPath;
        }
        catch (IOException ex)
        {
            return ReportBackupFailed(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ReportBackupFailed(ex);
        }
        catch (InvalidOperationException ex)
        {
            // The store refuses settings that fail validation, and the backup must not block the prompt.
            return ReportBackupFailed(ex);
        }
    }

    private string? ReportBackupFailed(Exception ex)
    {
        _logger.Warn(CultureInfo.InvariantCulture, "Unsaved project backup failed: {0}", ex.Message);
        CreateStatusScope("LoadProject").ReportWarning($"Could not back up the unsaved project: {ex.Message}");
        return null;
    }

    private bool HasUnsavedProjectWork()
        => _projectSession.HasUnsavedProjectWork();

    internal Task<ProjectLoadCommandResult> LoadProjectFromPathAsync(string path)
    {
        return RunProjectLoadAsync(() => LoadProjectFromPathCoreAsync(path));
    }

    // A rejected path must reach the status bar for every caller, so the user never sees a silent failed load.
    private ProjectLoadCommandResult ReportLoadRejected(string code, string message, string? path)
    {
        CreateStatusScope("LoadProject").ReportBlocked(message);
        return ProjectLoadCommandResult.Failed(code, message, path);
    }

    private async Task<ProjectLoadCommandResult> LoadProjectFromPathCoreAsync(string path)
    {
        var status = CreateStatusScope("LoadProject");
        var resolution = ProjectPathResolver.Resolve(path, File.Exists);
        if (resolution.Code is not null)
        {
            return ReportLoadRejected(resolution.Code, resolution.Message!, resolution.NormalizedPath);
        }

        var normalizedPath = resolution.NormalizedPath!;

        var commandResult = ProjectLoadCommandResult.Failed(
            code: "LOAD_FAILED",
            message: $"Load project failed: {normalizedPath}",
            path: normalizedPath);

        await RunUiOperationAsync(
            operationName: "Load project",
            showModalSpinner: true,
            onStart: () =>
            {
                _logger.Info(CultureInfo.InvariantCulture, "Loading project from path: {0}", normalizedPath);
                _logger.Debug(CultureInfo.InvariantCulture, "PERF LOAD_PROJECT: stage=start path={0}.", normalizedPath);
            },
            operationAsync: async () =>
            {
                var loadSw = Stopwatch.StartNew();
                var result = await ProjectPersistenceUseCase.LoadAsync(new ProjectLoadRequest(
                    PickLoadProjectPathAsync: () => Task.FromResult<string?>(normalizedPath),
                    CanProceedWithPendingEdits));
                loadSw.Stop();

                if (result.IsCancelled || result.Project is null || string.IsNullOrWhiteSpace(result.Path))
                {
                    _logger.Debug(CultureInfo.InvariantCulture, "PERF LOAD_PROJECT: stage=cancelled path={0}.", normalizedPath);
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
                _logger.Error(ex, "Load project failed.");
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
        _logger.Info(CultureInfo.InvariantCulture, "Loading project: {0}", path);
        var originalSuppressAppGeneralPersistence = _appGeneralPersistence.SuppressPersistence;
        _appGeneralPersistence.SuppressPersistence = true;
        _projectFile = file;
        InvalidateWorkflowDataSnapshot();
        _projectSession.LastSavedPath = path;
        try
        {
            using var sourceChangeBatch = BeginSimulationWorkspaceSourceChangeBatch();
            var applyStateSw = Stopwatch.StartNew();
            ApplyLoadedProjectSettings(file, path, status);
            applyStateSw.Stop();

            var cadReload = await ReloadCadAfterProjectLoadAsync(file, path, status);

            cadReload.CombinedGroupReplay?.ReportWarnings(status.ReportWarning, message => _logger.Warn(CultureInfo.InvariantCulture, "{0}", message));
            totalSw.Stop();
            LogProjectLoadTiming(path, persistenceElapsedMs, totalSw.ElapsedMilliseconds, applyStateSw.ElapsedMilliseconds, cadReload);

            return new ProjectLoadApplyTiming(
                TotalElapsedMs: totalSw.ElapsedMilliseconds,
                ApplyStateElapsedMs: applyStateSw.ElapsedMilliseconds,
                DxfElapsedMs: cadReload.DxfElapsedMs,
                RebuildElapsedMs: cadReload.RebuildElapsedMs,
                Step1ReplayElapsedMs: cadReload.ReplayTiming.Step1ReplayElapsedMs,
                Step2ReplayElapsedMs: cadReload.ReplayTiming.Step2ReplayElapsedMs,
                Step1Replayed: cadReload.ReplayTiming.Step1Replayed,
                Step2Replayed: cadReload.ReplayTiming.Step2Replayed);
        }
        finally
        {
            _appGeneralPersistence.SuppressPersistence = originalSuppressAppGeneralPersistence;
        }
    }

    private void ApplyLoadedProjectSettings(ProjectFile file, string path, UiOperationStatusReporter.Scope status)
    {
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
        _logger.Info(CultureInfo.InvariantCulture, "Project loaded: {0}", path);
        HasUnsavedChanges = false;
        MarkProjectLoadedForAppGeneralPersistence();
    }

    private async Task<ProjectLoadCadReload> ReloadCadAfterProjectLoadAsync(ProjectFile file, string path, UiOperationStatusReporter.Scope status)
    {
        var dxfElapsedMs = 0L;
        var rebuildElapsedMs = 0L;
        var replayTiming = ProjectLoadReplayTiming.Empty;
        DxfCombinedGroupReplayResult? combinedGroupReplay = null;

        using (_settingsSuppression.EnterSettingsLoading())
        {
            _cad = null;
            ResetDxfEditBaseline(null);
            _cachedFilteredCadForBuild = null;
            ClearDxfLayerCatalog();
            CadPads = new ObservableCollection<CadPad>();
            DetachAndClearLayerToggles();

            await RunWithCadLoadCanvasOverlayAsync(async () =>
            {
                (dxfElapsedMs, combinedGroupReplay) = await LoadProjectCadAsync(file, path, status);

                var rebuildSw = Stopwatch.StartNew();
                await TriggerGridRebuildAsync(requestFit: true);
                rebuildSw.Stop();
                rebuildElapsedMs = rebuildSw.ElapsedMilliseconds;

                replayTiming = await ReplayWorkflowStepsAfterProjectLoadAsync();
            });
        }

        return new ProjectLoadCadReload(dxfElapsedMs, rebuildElapsedMs, replayTiming, combinedGroupReplay);
    }

    private async Task<(long DxfElapsedMs, DxfCombinedGroupReplayResult? CombinedGroupReplay)> LoadProjectCadAsync(
        ProjectFile file,
        string path,
        UiOperationStatusReporter.Scope status)
    {
        var dxfOptions = BuildDxfOptions();
        var dxfLoadSw = Stopwatch.StartNew();
        var outcome = await Task.Run(() => _cadLoadUseCase.TryLoadFromProject(file.LastDxfPath, file.EmbeddedDxf, dxfOptions));
        dxfLoadSw.Stop();
        var dxfElapsedMs = dxfLoadSw.ElapsedMilliseconds;
        if (outcome is null)
        {
            return (dxfElapsedMs, KeepDxfEditsWithoutCad(file));
        }

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
        var combinedGroupReplay = ApplyCapturedDxfEditProjectState(persistedDxfEditState, outcome.FallbackWarning);
        status.ReportProgress(BuildCadLoadStatus(outcome, CadLoadContext.LoadProject, path));
        LogCadLoadOutcome(outcome, CadLoadContext.LoadProject);
        return (dxfElapsedMs, combinedGroupReplay);
    }

    private static void LogProjectLoadTiming(string path, long persistenceElapsedMs, long totalElapsedMs, long applyStateElapsedMs, ProjectLoadCadReload cadReload)
    {
        var replayTiming = cadReload.ReplayTiming;
        _logger.Info(CultureInfo.InvariantCulture, "Load project timing: total={0} ms, persistence={1} ms, apply-state={2} ms, dxf={3} ms, rebuild={4} ms.",
            totalElapsedMs,
            persistenceElapsedMs,
            applyStateElapsedMs,
            cadReload.DxfElapsedMs,
            cadReload.RebuildElapsedMs);
        _logger.Info(CultureInfo.InvariantCulture, "Load project timing detail: step1-auto-replay={0} ms (replayed={1}).", replayTiming.Step1ReplayElapsedMs, replayTiming.Step1Replayed);
        _logger.Info(CultureInfo.InvariantCulture, "Load project timing detail: step2-auto-replay={0} ms (replayed={1}).", replayTiming.Step2ReplayElapsedMs, replayTiming.Step2Replayed);
        _logger.Debug(CultureInfo.InvariantCulture, "PERF LOAD_PROJECT SUMMARY: total={0}ms persistence={1}ms applyState={2}ms dxf={3}ms rebuild={4}ms step1={5}ms step2={6}ms step1Replayed={7} step2Replayed={8} path={9}",
            totalElapsedMs,
            persistenceElapsedMs,
            applyStateElapsedMs,
            cadReload.DxfElapsedMs,
            cadReload.RebuildElapsedMs,
            replayTiming.Step1ReplayElapsedMs,
            replayTiming.Step2ReplayElapsedMs,
            replayTiming.Step1Replayed ? "Y" : "N",
            replayTiming.Step2Replayed ? "Y" : "N",
            path);
    }

    private void DetachAndClearLayerToggles()
    {
        foreach (var toggle in LayerToggles)
        {
            toggle.PropertyChanging -= OnLayerToggleChanging;
            toggle.PropertyChanged -= OnLayerToggleChanged;
        }

        LayerToggles.Clear();
    }

    private async Task<ProjectLoadReplayTiming> ReplayWorkflowStepsAfterProjectLoadAsync()
    {
        if (!AutoReplayStep2AfterProjectLoad)
        {
            _logger.Info(WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(
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
            _logger.Info(WorkflowPipelineService.GetProjectLoadReplayStartedMessage(step));
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
                _logger.Info(WorkflowPipelineService.GetProjectLoadReplayCompletedMessage(step));
            }
            else
            {
                _logger.Info(WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(step, "preconditions not met"));
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

internal sealed record ProjectLoadCadReload(
    long DxfElapsedMs,
    long RebuildElapsedMs,
    ProjectLoadReplayTiming ReplayTiming,
    DxfCombinedGroupReplayResult? CombinedGroupReplay);

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
