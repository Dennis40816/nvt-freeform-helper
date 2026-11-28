using System.Globalization;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Grid;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private async Task TriggerGridRebuildAsync(bool requestFit = false) =>
        await _gridRebuildUseCase.RequestAsync(requestFit);

    private Task RebuildGridAsync() => GridMatching.Rebuild.RebuildGridAsync();

    private void ClearPadMatchResult(bool clearRegularAssignments) =>
        GridMatching.Rebuild.ClearPadMatchResult(clearRegularAssignments);

    private GridStatePorts CreateGridStatePorts() => new(
        () => _grid,
        grid => _grid = grid,
        () => _cad is not null,
        () => RegularPads.Count,
        () => CadPads,
        () => _isLoadingSettings,
        () => _selectedCadIds.Count > 0,
        () => _selection.RegularIndices,
        () => _workflowData.LatestPadMatch,
        SetLatestPadMatchResult);

    private GridEffectPorts CreateGridEffectPorts() => new(
        () => ApplyUiToSettings(_projectFile.Settings),
        BumpNotchExportGridFingerprint,
        BumpNotchExportIndexFingerprint,
        InvalidateNotchCompensationCache,
        () => RefreshNotchCanvasPreview(showStatus: false),
        NotifySimulationWorkspaceSourceChanged,
        () => CanvasHost?.Invalidate(),
        () => CanvasHost?.FitToContent(),
        MarkUnsaved,
        SetStatus,
        OnPropertyChanged,
        (message, args) => _logger.Info(CultureInfo.InvariantCulture, message, args),
        (message, args) => _logger.Debug(CultureInfo.InvariantCulture, message, args));

    private GridWorkflowPorts CreateGridWorkflowPorts() => new(
        () => TryEnsureWorkflowStep(WorkflowStepId.Step2Freeform),
        (invalidate, move) => OnWorkflowStepCompleted(WorkflowStepId.Step1Match, invalidate, move),
        () => OnWorkflowStepChanged(WorkflowStepId.Step2Freeform, showStatus: false),
        move => OnWorkflowStepCompleted(WorkflowStepId.Step2Freeform, invalidateDownstream: false, moveToNextStep: move),
        () => ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false));

    private GridRebuildOwner CreateGridRebuildOwner() => new(new GridRebuildPorts(
        CreateGridStatePorts(), CreateGridEffectPorts(), CreateGridWorkflowPorts(),
        new GridBuildPorts(
            RunGridRebuildOperationAsync,
            BuildGridSnapshotAsync,
            hint => CanvasHintText = hint,
            hint => DxfRegularSourceHint = hint,
            pads => RegularPads = pads,
            grid => RegularMask.Workflow.Refresh(grid),
            () => RegularMask.Workflow.ResetResult(grid: null),
            UpdateCadOutputFwDiffIndexing,
            RefreshDxfRegularSourceHint,
            ResetFreeformAutoDetectStats,
            links => MatchLinksForCanvas = links),
        new GridSelectionPorts(
            () => _selection.SuppressClearOnRebuild,
            () => _selection.SuppressClearOnRebuild = false,
            () => _selectionCoordinator.ClearSelection(CanvasHost),
            UpdateSelectedRangeSummary),
        new GridStartupPorts(
            () => _gridStartup.InitialGridBuiltMarked,
            () => _gridStartup.InitialGridBuiltMarked = true,
            StartupPerfTracker.Mark,
            () => _gridStartup.FitAfterNextBuild,
            () => _gridStartup.FitAfterNextBuild = false)));

    private Task RunGridRebuildOperationAsync(Func<Task> operationAsync) =>
        RunUiOperationAsync("Grid rebuild", operationAsync, ex =>
        {
            SetStatusError("Rebuild grid failed", ex);
            _logger.Error(ex, "Rebuild grid failed.");
        });

    private async Task<GridBuildOutcome> BuildGridSnapshotAsync()
    {
        var buildRequest = CreateGridRebuildBuildRequestSnapshot();
        var result = IsCadLoadCanvasOverlayVisible
            ? await Task.Run(() => GridRebuildOrchestrator.Build(buildRequest))
            : GridRebuildOrchestrator.Build(buildRequest);
        return new GridBuildOutcome(result.Success, result.Grid, result.StatusText, result.CanvasHint, result.DxfRegularSourceHint);
    }

    private GridRebuildBuildRequest CreateGridRebuildBuildRequestSnapshot()
    {
        var fullCadSnapshot = _cad;
        var boundsCadSnapshot = GridAlignmentMode == GridAlignmentMode.FromCadBounds
            ? BuildCadPadSetForBounds()
            : fullCadSnapshot;

        return new GridRebuildBuildRequest
        {
            ProjectFile = GridRebuildOwner.CreateProjectSnapshot(_projectFile),
            Settings = GridRebuildOwner.CloneGridSettings(_projectFile.Settings.Grid),
            RegularSourceMode = RegularSourceMode,
            GridAlignmentMode = GridAlignmentMode,
            Cad = fullCadSnapshot,
            RegularSourceLayerName = SelectedRegularSourceLayerOption?.Name,
            HiddenCadPadIds = CadDocument.BuildEffectiveHiddenIds(),
            BuildCadPadSetForBounds = () => boundsCadSnapshot,
        };
    }
}
