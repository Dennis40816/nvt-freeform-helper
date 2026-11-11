using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // UI thread only. Construction is inert; all delegates read the current host state.
    private IndexMappingOwner? _indexMapping;
    private IndexMappingOwner IndexMapping => _indexMapping ??= CreateIndexMappingOwner();

    private IndexMappingOwner CreateIndexMappingOwner()
    {
        var notifications = new IndexMappingNotifications(name => OnPropertyChanging(name), name => OnPropertyChanged(name));
        var inputs = new IndexAssignmentInputs(() => _projectFile, () => _grid, () => CadPads,
            () => RegularPads, () => _workflowData.LatestPadMatch.CadToRegular,
            () => RegularMask.Workflow.ActiveRegularPadIds, () => SelectedScanOrder,
            () => CadOutputFwDiffAutoMode, () => _selectedCadIds);
        var effects = new IndexMappingEffects(SetStatus, MarkUnsaved,
            () => InvalidateDownstreamFromStep4(showStatus: false), NotifySimulationWorkspaceSourceChanged,
            PushUndo, (cad, regular, focus, missing, success) => TryLocateSelection(cad, regular, focus, missing, success),
            (message, arguments) => _logger.Info(CultureInfo.InvariantCulture, message, arguments),
            (message, arguments) => _logger.Warn(CultureInfo.InvariantCulture, message, arguments));
        var assignmentPorts = new IndexAssignmentPorts(ReadIndexMappingSnapshot,
            pads => CadPads = pads, cad => CadDocument.PublishFilteredCad(cad),
            BumpNotchExportCadFingerprint, BumpNotchExportIndexFingerprint, InvalidateWorkflowDataSnapshot,
            ids => CadOutputFwDiffIndexOverrideCadIds = ids,
            ids => CadOutputFwDiffIndexAnchorCadIdsForCanvas = ids,
            summary => CadOutputFwDiffAssignmentSummary = summary,
            () => CadOutputFwDiffIndexAnchorCadId, value => CadOutputFwDiffIndexAnchorCadId = value,
            () => _settingsSuppression.EnterDxfIndexAnchorChange(), () => _undoSuppression.Enter());
        var ports = new IndexMappingPorts(PrepareIndexMappingInputs, RunIndexMappingOperationAsync,
            () => ApplyUiToSettings(_projectFile.Settings), AnalyzeIndexMappingReportAsync,
            () => OpenIndexMappingReportAsync is not null, OpenMappingReportAsync,
            () => OnWorkflowStepCompleted(WorkflowStepId.Step4IndexDiagnostics,
                invalidateDownstream: false, moveToNextStep: true));
        return new IndexMappingOwner(inputs, ports, effects, notifications,
            new IndexAssignmentOwner(inputs, assignmentPorts, effects, notifications));
    }

    private IndexMappingAnalysisInput? PrepareIndexMappingInputs(Action onCadUnavailable)
        => TryGetOperationCadAndGrid(BuildFilteredCadPadSet, "Step 4: import DXF and build grid first.",
            onCadUnavailable, out var cad, out var grid) ? new(cad, grid) : null;

    private Task RunIndexMappingOperationAsync(Action onStart, Func<Action<double>, Task> operationAsync)
        => RunUiProgressOperationAsync("DXF/Regular mapping analysis",
            busy => IsAnalyzingIndexMapping = busy, progress => IndexMappingProgress = progress,
            operationAsync, ex =>
            {
                SetStatusError("Mapping analyze failed", ex);
                _logger.Error(ex, "Mapping analyze failed.");
            }, onStart);

    private Task<DxfRegularMappingResult> AnalyzeIndexMappingReportAsync(
        CadPadSet cadVisible, RegularGrid grid, Action<double> reportProgress)
    {
        var workflowSnapshot = BuildWorkflowDataSnapshot();
        return Task.Run(() => DxfRegularMappingUseCase.Analyze(
            cadVisible, grid, _projectFile.Settings, _projectFile.DxfRegularMappingOverrides,
            workflowSnapshot.CadOutputFwDiffIndexByCadId, workflowSnapshot.CadToRegularByCadId,
            workflowSnapshot.CadIcIndexByCadId, workflowSnapshot.ActiveRegularVisibilityMaskPadIds,
            workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId, reportProgress));
    }

    private Task OpenMappingReportAsync(DxfRegularMappingReport report)
    {
        var reportViewModel = CreateIndexMappingReportViewModel(report);
        return OpenIndexMappingReportAsync!(reportViewModel);
    }

    private Task AnalyzeIndexMappingAsync() => IndexMapping.AnalyzeIndexMappingAsync();

    private IndexMappingReportViewModel CreateIndexMappingReportViewModel(DxfRegularMappingReport report)
    {
        var reportViewModel = new IndexMappingReportViewModel(
            DxfRegularMappingSummary,
            report,
            LocateMappingTargetFromReport,
            ApplyMappingOverrideFromReport,
            ApplyCadOutputFwDiffOverrideFromReport,
            ClearMappingOverrideFromReport,
            GetMappingOverrideRegularIndex);
        reportViewModel.Editing.AttachProject(this);
        return reportViewModel;
    }

    private bool LocateMappingTargetFromReport(int? cadPadId, int? regularPadIndex)
        => IndexMapping.LocateMappingTargetFromReport(cadPadId, regularPadIndex);

    private bool ApplyMappingOverrideFromReport(int cadId, int regularIndex)
        => IndexMapping.ApplyMappingOverrideFromReport(cadId, regularIndex);

    private bool ClearMappingOverrideFromReport(int cadId)
        => IndexMapping.ClearMappingOverrideFromReport(cadId);

    private bool ApplyCadOutputFwDiffOverrideFromReport(int cadId, int diffIndex)
        => IndexMapping.ApplyCadOutputFwDiffOverrideFromReport(cadId, diffIndex);

    private int? GetMappingOverrideRegularIndex(int cadPadId)
        => IndexMapping.GetMappingOverrideRegularIndex(cadPadId);

    /// <summary>Gets or sets the accepted DXF/Regular mapping diagnostics result.</summary>
    public MappingDiagnosticsResult MappingDiagnosticsResult
    {
        get => IndexMapping.MappingDiagnosticsResult;
        set => IndexMapping.MappingDiagnosticsResult = value;
    }

    /// <summary>Gets the summary message for DXF/Regular mapping analysis.</summary>
    public string DxfRegularMappingSummary => IndexMapping.DxfRegularMappingSummary;

    /// <summary>Gets the observable collection of detailed DXF/Regular mapping issues.</summary>
    public ObservableCollection<string> DxfRegularMappingIssues => IndexMapping.DxfRegularMappingIssues;

    /// <summary>Gets a value indicating whether any DXF/Regular mapping issues were found.</summary>
    public bool HasDxfRegularMappingIssues => IndexMapping.HasDxfRegularMappingIssues;

    /// <summary>
    /// Gets or sets a value indicating whether index mapping diagnostics are currently running.
    /// </summary>
    public bool IsAnalyzingIndexMapping
    {
        get => _indexMappingState.Current.IsBusy;
        set => SetOperationBusy(_indexMappingState, value, nameof(IsAnalyzingIndexMapping));
    }

    /// <summary>
    /// Gets or sets the current progress value (0-1) for index mapping diagnostics.
    /// </summary>
    public double IndexMappingProgress
    {
        get => _indexMappingState.Current.Progress;
        set => SetOperationProgress(_indexMappingState, value, nameof(IndexMappingProgress));
    }

    private void UpdateCadOutputFwDiffIndexing(IReadOnlyList<CadPad> visiblePads)
        => IndexMapping.Assignment.UpdateCadOutputFwDiffIndexing(visiblePads);

    private void RebuildVisibleDxfIndexMap(IReadOnlyList<CadPad> ordered)
        => IndexMapping.Assignment.RebuildVisibleDxfIndexMap(ordered);
}
