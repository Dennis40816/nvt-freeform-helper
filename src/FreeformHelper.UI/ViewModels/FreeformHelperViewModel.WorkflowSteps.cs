using System.Collections.ObjectModel;
using System.Windows.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel : IWorkflowDispatchPort, IWorkspaceNavigationSource
{
    private const string DiagnosticsNotRunSummary = MappingDiagnosticsResult.NotRunSummary;
    private const string NotchExportNotRunSummary = "Notch export: not run.";
    private readonly AsyncGeneration _notchFinalProjection = new();
    private readonly WorkflowDispatchCoordinator _workflowDispatch;
    private readonly WorkspaceNavigationPresentation _workspaceNavigation;
    private readonly WorkspaceToggleRegistry _workspaceToggleRegistry;

    private void MoveToWorkflowStep(WorkspaceWorkStep step) => MoveToWorkflowStep(step switch
    {
        WorkspaceWorkStep.Step1Match => WorkflowStepId.Step1Match,
        WorkspaceWorkStep.Step2Freeform => WorkflowStepId.Step2Freeform,
        WorkspaceWorkStep.Step3NotchPreview => WorkflowStepId.Step3NotchPreview,
        _ => WorkflowStepId.Step5Export,
    });

    private void MoveToWorkflowStep(WorkflowStepId step) => _workflowDispatch.MoveToWorkflowStep(step);

    private void OnWorkflowStepChanged(WorkflowStepId changedStep, bool showStatus = false)
        => _workflowDispatch.OnWorkflowStepChanged(changedStep, showStatus);

    private void OnWorkflowStepCompleted(WorkflowStepId completedStep, bool invalidateDownstream, bool moveToNextStep)
        => _workflowDispatch.OnWorkflowStepCompleted(completedStep, invalidateDownstream, moveToNextStep);

    private void ResetWorkflowFromStep(WorkflowStepId changedStep, bool showStatus = false)
        => _workflowDispatch.ResetWorkflowFromStep(changedStep, showStatus);

    private void InvalidateDownstreamFromStep2(bool showStatus)
        => OnWorkflowStepChanged(WorkflowStepId.Step2Freeform, showStatus);

    private void InvalidateDownstreamFromStep3(bool showStatus)
        => OnWorkflowStepChanged(WorkflowStepId.Step3NotchPreview, showStatus);

    private void InvalidateDownstreamFromStep4(bool showStatus)
        => OnWorkflowStepChanged(WorkflowStepId.Step4IndexDiagnostics, showStatus);

    internal Task<WorkflowStepExecutionResult> RunWorkflowStepAsync(WorkflowStepId step)
        => _workflowDispatch.RunWorkflowStepAsync(step);

    internal WorkflowStepExecutionResult ClearWorkflowStep(WorkflowStepId step)
        => _workflowDispatch.ClearWorkflowStep(step);

    private void ClearStep2ResultCore()
    {
        if (_grid is not null)
        {
            foreach (RegularPad regularPad in _grid.Pads)
            {
                regularPad.Freeform = FreeformType.None;
            }

            BumpNotchExportGridFingerprint();
        }

        _projectFile.FreeformOverrides.Clear();
        ResetFreeformAutoDetectStats();
        NotifySimulationWorkspaceSourceChanged();
        CanvasHost?.Invalidate();
    }

    private void ClearStep3ResultCore()
    {
        InvalidateNotchCompensationCache();
        ClearNotchCanvasPreview();
    }

    private void ClearStep4ResultCore()
    {
        MappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun();
        IndexMappingProgress = 0;
    }

    private void ClearStep5ResultCore()
    {
        ClearStep5ProjectedResultCore();
        NotchExportProgress = 0.0;
        NotchExportProgressText = "Idle.";
        IsNotchExporting = false;
        _notchExportGenerationCacheService.Clear();
    }

    private void ClearStep5ProjectedResultCore()
    {
        _notchFinalProjection.Next();
        _notchExportGenerationCacheService.InvalidateCadAllocationProjectedRowCount();
        NotchExportSummary = NotchExportNotRunSummary;
        _notchCache.LastGeneratedTable = null;
        ClearNotchValidation();
    }

    WorkflowStateSnapshot IWorkflowDispatchPort.BuildWorkflowStateSnapshot() => BuildWorkflowStateSnapshot();
    bool IWorkflowDispatchPort.CanAdvanceTo(WorkflowStepId step) => TryEnsureWorkflowStep(step, suppressStatus: true);
    void IWorkflowDispatchPort.SetStatus(string message) => SetStatus(message);
    void IWorkflowDispatchPort.InvalidateCanvas() => CanvasHost?.Invalidate();
    void IWorkflowDispatchPort.ClearStep1Result()
    {
        ClearNotchCanvasPreview();
        ClearPadMatchResult(clearRegularAssignments: true);
    }
    void IWorkflowDispatchPort.ClearStep2Result() => ClearStep2ResultCore();
    void IWorkflowDispatchPort.ClearStep3Result() => ClearStep3ResultCore();
    void IWorkflowDispatchPort.ClearStep4Result() => ClearStep4ResultCore();
    void IWorkflowDispatchPort.ClearStep5Result() => ClearStep5ResultCore();
    void IWorkflowDispatchPort.RefreshStep3Preview() => RefreshNotchCanvasPreview(showStatus: true);
    Task IWorkflowDispatchPort.MatchAsync() => MatchAsync();
    Task IWorkflowDispatchPort.AutoDetectFreeformsAsync() => AutoDetectFreeformsAsync();
    Task IWorkflowDispatchPort.AnalyzeIndexMappingAsync() => AnalyzeIndexMappingAsync();

    void IWorkflowDispatchPort.ApplyExpansion(WorkflowStepExpansionState expansion)
        => _workflowPanelState.ApplyExpansion(expansion.IsStep1Expanded, expansion.IsStep2Expanded,
            expansion.IsStep3Expanded, expansion.IsStep4Expanded, expansion.IsStep5Expanded, expansion.IsStep6Expanded);

    public ObservableCollection<WorkspaceHeaderItem> WorkspaceHeaderItems
    {
        get => _workspaceToggleRegistry.WorkspaceHeaderItems;
        set => _workspaceToggleRegistry.WorkspaceHeaderItems = value;
    }

    public string WorkspacePrimaryActionText => _workspaceNavigation.WorkspacePrimaryActionText;
    public ICommand WorkspacePrimaryActionCommand => _workspaceNavigation.WorkspacePrimaryActionCommand;
    public string WorkspaceWorkHint => _workspaceNavigation.WorkspaceWorkHint;
    public string WorkspaceMatchSummary => _workspaceNavigation.WorkspaceMatchSummary;
    public string ManualOverrideHint => _workspaceNavigation.ManualOverrideHint;
    public string ToFullPreviewToggleHint => _workspaceNavigation.ToFullPreviewToggleHint;
    public string DxfCheckHeaderText => _workspaceNavigation.DxfCheckHeaderText;
    public string DxfEditsHeaderText => _workspaceNavigation.DxfEditsHeaderText;
    public string DxfRotationScopeHint => _workspaceNavigation.DxfRotationScopeHint;
    public string ValidationTraceHint => _workspaceNavigation.ValidationTraceHint;

    private void NotifyWorkspaceLayoutChanged(string? propertyName) => _workspaceNavigation.NotifyWorkspaceLayoutChanged(propertyName);
    private void SelectWorkspaceWorkStep(WorkspaceWorkStep step) => _workspaceNavigation.SelectWorkspaceWorkStep(step);

    bool IWorkspaceNavigationSource.HasCad => GetWorkflowStateSnapshot().HasCad;
    bool IWorkspaceNavigationSource.HasStep1Result => GetWorkflowStateSnapshot().HasStep1Result;
    WorkspaceWorkStep IWorkspaceNavigationSource.WorkStep
    {
        get => _inspectorPanel.WorkStep;
        set => _inspectorPanel = _inspectorPanel with { WorkStep = value };
    }
    ICommand IWorkspaceNavigationSource.OpenDxfCommand => OpenDxfCommand;
    ICommand IWorkspaceNavigationSource.MatchCommand => MatchCommand;
    ICommand IWorkspaceNavigationSource.AutoDetectFreeformsCommand => AutoDetectFreeformsCommand;
    ICommand IWorkspaceNavigationSource.ExportNotchCommand => ExportNotchCommand;
    int IWorkspaceNavigationSource.SelectedRegularCount => SelectionSummaryResult.RegularCount;
    bool IWorkspaceNavigationSource.IsDxfOverlapNotRun => DxfOverlapSummary == DxfOverlapResult.NotRunSummary;

    (bool UsesLayer, bool MatchesSelection, int TargetCount) IWorkspaceNavigationSource.GetRotationScope()
    {
        if (!IsDxfEditRotationLayerScope || _cad is null) return (false, false, 0);
        var targets = GetRotationTargetCadIds(_cad);
        return (true, targets.SetEquals(_selectedCadIds), targets.Count);
    }
}
