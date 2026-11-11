// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.UseCases.ProjectLoad;
using FreeformHelper.UI.ViewModels.Project;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Compatibility entries and service adapters for project persistence; CAD leaf behavior stays here until stage 2.</summary>
public sealed partial class FreeformHelperViewModel
{
    internal Task<bool>? TryStartSaveProjectCommandAsync()
        => _projectCommands.TryStartSaveAsync();

    /// <summary>Saves settings, overrides, the UI snapshot, and optionally embedded DXF data.</summary>
    /// <returns>True if the project was saved successfully, false otherwise.</returns>
    public Task<bool> SaveProjectAsync() => _projectPersistenceCoordinator.SaveProjectAsync();

    private Task LoadProjectAsync() => _projectPersistenceCoordinator.LoadProjectAsync();

    internal Task<ProjectLoadCommandResult> LoadProjectFromPathAsync(string path)
        => _projectPersistenceCoordinator.LoadProjectFromPathAsync(path);

    private ProjectPersistenceCoordinator CreateProjectPersistenceCoordinator()
    {
        var operations = CreateProjectOperationPorts();
        var applier = new ProjectLoadApplier(
            CreateProjectApplyPorts(), new ProjectCadPorts(LoadProjectCadAsync), CreateProjectReplayPorts(), operations);
        return new ProjectPersistenceCoordinator(
            CreateProjectPersistencePorts(), CreateProjectSessionPorts(), operations, applier.ApplyAsync);
    }

    private ProjectPersistencePorts CreateProjectPersistencePorts() => new(
        getSavePicker: () => PickSaveProjectPathAsync,
        getLoadPicker: () => PickLoadProjectPathAsync,
        getEmbedConfirmation: () => ConfirmEmbedDxfAsync,
        saveAsync: SaveCurrentProjectStateAsync,
        loadAsync: LoadPersistedProjectAsync,
        resolvePath: path =>
        {
            var result = ProjectPathResolver.Resolve(path, File.Exists);
            return (result.NormalizedPath, result.Code, result.Message);
        },
        prepareReplacementAsync: PrepareProjectReplacementAsync,
        saveThenLoadAsync: ReplaceProjectWorkflow.SaveThenLoadAsync,
        getBackupPath: () => UnsavedProjectBackup.PathFor(_appGeneralSettingsStore.SettingsPath),
        deleteBackup: UnsavedProjectBackup.TryDelete);

    private ProjectSessionPorts CreateProjectSessionPorts() => new(
        isEditingEnabled: () => IsProjectEditingEnabled,
        runLoadAsync: RunProjectLoadAsync,
        hasUnsavedWork: _projectSession.HasUnsavedProjectWork,
        captureCadEdits: () => CaptureDxfEditProjectState(_projectFile),
        setDirty: value => HasUnsavedChanges = value,
        setLastSavedPath: path => _projectSession.LastSavedPath = path,
        flushDeferredAppSettings: FlushDeferredAppGeneralSettingsIfNeeded);

    private async Task<(bool IsCancelled, string? Path, string? EmbedWarning)> SaveCurrentProjectStateAsync(
        Func<Task<string?>> pickPathAsync, Func<Task<bool>>? confirmEmbedAsync)
    {
        var result = await _projectPersistenceUseCase.SaveAsync(new ProjectSaveRequest(
            Project: _projectFile,
            Grid: _grid,
            CadPadCustomValues: _cadPadCustomValues,
            PickSaveProjectPathAsync: pickPathAsync,
            ConfirmEmbedDxfAsync: confirmEmbedAsync,
            BuildUiSnapshot: BuildUiSnapshot,
            ApplyUiToSettings: ApplyUiToSettings,
            HasCadLoaded: _cad is not null));
        return (result.IsCancelled, result.Path, result.EmbedWarning);
    }

    private async Task<(bool IsCancelled, ProjectFile? Project, string? Path)> LoadPersistedProjectAsync(string path)
    {
        var result = await ProjectPersistenceUseCase.LoadAsync(new ProjectLoadRequest(
            PickLoadProjectPathAsync: () => Task.FromResult<string?>(path),
            CanProceedWithPendingEdits));
        return (result.IsCancelled, result.Project, result.Path);
    }

    private async Task<ProjectReplacementPlan> PrepareProjectReplacementAsync(
        Func<Task<string?>> pickPathAsync, Func<bool> hasUnsavedWork, Func<Task<string?>> backUpAsync)
    {
        var plan = await ReplaceProjectWorkflow.PrepareAsync(
            pickPathAsync, hasUnsavedWork, backUpAsync, () => ProjectDialogs.ConfirmSaveBeforeLoadAsync);
        return new ProjectReplacementPlan(
            plan.Outcome switch
            {
                ReplaceProjectOutcome.Cancel => ProjectReplacementOutcome.Cancel,
                ReplaceProjectOutcome.Load => ProjectReplacementOutcome.Load,
                ReplaceProjectOutcome.SaveThenLoad => ProjectReplacementOutcome.SaveThenLoad,
                _ => ProjectReplacementOutcome.Other
            },
            plan.Path, plan.BackupPath, plan.Message);
    }

    private ProjectOperationPorts CreateProjectOperationPorts() => new(
        createStatus: operation =>
        {
            var status = CreateStatusScope(operation);
            return new ProjectOperationStatus(
                status.ReportSuccess, status.ReportProgress, status.ReportWarning, status.ReportBlocked, status.ReportError);
        },
        runAsync: RunUiOperationAsync,
        runBooleanAsync: RunUiOperationAsync,
        info: (message, arguments) => _logger.Info(CultureInfo.InvariantCulture, message, arguments),
        debug: (message, arguments) => _logger.Debug(CultureInfo.InvariantCulture, message, arguments),
        warnFormat: (message, arguments) => _logger.Warn(CultureInfo.InvariantCulture, message, arguments),
        infoMessage: message => _logger.Info(message),
        warn: message => _logger.Warn(message),
        error: (exception, message) => _logger.Error(exception, message));

    private ProjectApplyPorts CreateProjectApplyPorts() => new(
        getSuppressPersistence: () => _appGeneralPersistence.SuppressPersistence,
        setSuppressPersistence: value => _appGeneralPersistence.SuppressPersistence = value,
        installDocument: file => _projectFile = file,
        invalidateWorkflowSnapshot: InvalidateWorkflowDataSnapshot,
        setPath: path => _projectSession.LastSavedPath = path,
        beginSourceChangeBatch: () => BeginSimulationWorkspaceSourceChangeBatch(),
        replaceCustomPadValues: file =>
        {
            _cadPadCustomValues.Clear();
            foreach (var kv in file.CadPadCustomValues)
            {
                _cadPadCustomValues[kv.Key] = kv.Value;
            }
        },
        loadSettings: LoadSettingsToUi,
        applyView: ApplyViewSnapshot,
        applyImport: (snapshot, file) => ApplyImportSnapshot(snapshot, file),
        applyAppVisualPreferences: TryApplyAppGeneralVisualPreferencesAfterProjectLoad,
        clearStep5: ClearStep5ResultCore,
        setDirty: value => HasUnsavedChanges = value,
        deferAppPersistence: MarkProjectLoadedForAppGeneralPersistence,
        enterSettingsLoading: () => _settingsSuppression.EnterSettingsLoading(),
        resetCad: () => _cad = null,
        resetCadEditBaseline: () => ResetDxfEditBaseline(null),
        clearCadBuildCache: () => _cachedFilteredCadForBuild = null,
        clearCadCatalog: ClearDxfLayerCatalog,
        publishEmptyCadPads: () => CadPads = new ObservableCollection<CadPad>(),
        detachLayerToggles: DetachAndClearLayerToggles,
        runCadOverlayAsync: RunWithCadLoadCanvasOverlayAsync,
        rebuildGridAsync: () => TriggerGridRebuildAsync(requestFit: true));

    private ProjectReplayPorts CreateProjectReplayPorts() => new(
        isStep2Enabled: () => AutoReplayStep2AfterProjectLoad,
        getDisabledMessage: () => WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(
            WorkflowStepId.Step2Freeform, "disabled by setting"),
        buildPlan: () => WorkflowPipelineService.BuildProjectLoadReplayPlan(AutoReplayStep2AfterProjectLoad)
            .Select(CreateProjectReplayAction).ToArray());

    private ProjectReplayAction CreateProjectReplayAction(WorkflowStepId step)
    {
        var identity = step switch
        {
            WorkflowStepId.Step1Match => ProjectReplayStep.Step1Match,
            WorkflowStepId.Step2Freeform => ProjectReplayStep.Step2Freeform,
            _ => ProjectReplayStep.Other
        };
        Func<Task<bool>> executeAsync = step switch
        {
            WorkflowStepId.Step1Match => RestoreStep1MatchAfterProjectLoadAsync,
            WorkflowStepId.Step2Freeform => RestoreStep2FreeformAfterProjectLoadAsync,
            _ => () => Task.FromResult(false)
        };
        return new ProjectReplayAction(
            identity,
            () => WorkflowPipelineService.GetProjectLoadReplayStartedMessage(step),
            () => WorkflowPipelineService.GetProjectLoadReplayCompletedMessage(step),
            () => WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(step, "preconditions not met"),
            executeAsync);
    }

    private async Task<(long DxfElapsedMs, DxfCombinedGroupReplayResult? CombinedGroupReplay)> LoadProjectCadAsync(
        ProjectFile file,
        string path,
        ProjectOperationStatus status)
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

    private void DetachAndClearLayerToggles()
    {
        foreach (var toggle in LayerToggles)
        {
            toggle.PropertyChanging -= OnLayerToggleChanging;
            toggle.PropertyChanged -= OnLayerToggleChanged;
        }

        LayerToggles.Clear();
    }
}
