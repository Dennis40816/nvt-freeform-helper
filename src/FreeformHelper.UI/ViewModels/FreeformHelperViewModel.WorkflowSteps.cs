using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const string DiagnosticsNotRunSummary = "Diagnostics: not run.";
    private const string NotchExportNotRunSummary = "Notch export: not run.";
    private int _notchFinalProjectionRevision;

    private void MoveToWorkflowStep(int step)
    {
        MoveToWorkflowStep(WorkflowPipelineService.NormalizeStepOrDefault(step));
    }

    private void MoveToWorkflowStep(WorkflowStepId step)
    {
        var expansion = WorkflowPipelineService.BuildExpansionState(step);
        IsStep1Expanded = expansion.IsStep1Expanded;
        IsStep2Expanded = expansion.IsStep2Expanded;
        IsStep3Expanded = expansion.IsStep3Expanded;
        IsStep4Expanded = expansion.IsStep4Expanded;
        IsStep5Expanded = expansion.IsStep5Expanded;
        IsStep6Expanded = expansion.IsStep6Expanded;
    }

    private void TryMoveToNextWorkflowStep(WorkflowStepId completedStep)
    {
        if (!WorkflowPipelineService.TryGetNextStepOnSuccess(completedStep, out var nextStep))
        {
            return;
        }

        if (TryEnsureWorkflowStep(nextStep, suppressStatus: true))
        {
            MoveToWorkflowStep(nextStep);
        }
    }

    private void OnWorkflowStepChanged(WorkflowStepId changedStep, bool showStatus = false)
    {
        InvalidateDownstreamFromStep(changedStep, showStatus);
    }

    private void OnWorkflowStepCompleted(
        WorkflowStepId completedStep,
        bool invalidateDownstream,
        bool moveToNextStep)
    {
        if (invalidateDownstream)
        {
            OnWorkflowStepChanged(completedStep, showStatus: false);
        }

        if (moveToNextStep)
        {
            TryMoveToNextWorkflowStep(completedStep);
        }
    }

    private void ResetWorkflowFromStep(WorkflowStepId changedStep, bool showStatus = false)
    {
        OnWorkflowStepChanged(changedStep, showStatus);
        MoveToWorkflowStep(changedStep);
    }

    private void InvalidateDownstreamFromStep1(bool showStatus)
    {
        OnWorkflowStepChanged(WorkflowStepId.Step1Match, showStatus);
    }

    private void InvalidateDownstreamFromStep2(bool showStatus)
    {
        OnWorkflowStepChanged(WorkflowStepId.Step2Freeform, showStatus);
    }

    private void InvalidateDownstreamFromStep3(bool showStatus)
    {
        OnWorkflowStepChanged(WorkflowStepId.Step3NotchPreview, showStatus);
    }

    private void InvalidateDownstreamFromStep4(bool showStatus)
    {
        OnWorkflowStepChanged(WorkflowStepId.Step4IndexDiagnostics, showStatus);
    }

    private void InvalidateDownstreamFromStep(WorkflowStepId changedStep, bool showStatus)
    {
        var steps = WorkflowPipelineService.GetDownstreamStepsToInvalidate(changedStep);
        foreach (var step in steps)
        {
            ClearStepResultCore(step);
        }

        if (!showStatus)
        {
            return;
        }

        var message = WorkflowPipelineService.GetInvalidationStatusMessage(changedStep);
        if (!string.IsNullOrWhiteSpace(message))
        {
            SetStatus(message);
        }
    }

    private void ClearStepResultCore(WorkflowStepId step)
    {
        switch (step)
        {
            case WorkflowStepId.Step2Freeform:
                ClearStep2ResultCore();
                break;
            case WorkflowStepId.Step3NotchPreview:
                ClearStep3ResultCore();
                break;
            case WorkflowStepId.Step4IndexDiagnostics:
                ClearStep4ResultCore();
                break;
            case WorkflowStepId.Step5Export:
                ClearStep5ResultCore();
                break;
        }
    }

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
        ResetFreeformAutoDetectStats("Freeform stats: not run.");
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
        DxfRegularMappingSummary = DiagnosticsNotRunSummary;
        DxfRegularMappingIssues.Clear();
        HasDxfRegularMappingIssues = false;
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
        Interlocked.Increment(ref _notchFinalProjectionRevision);
        _notchExportGenerationCacheService.InvalidateCadAllocationProjectedRowCount();
        NotchExportSummary = NotchExportNotRunSummary;
        _lastGeneratedNotchTable = null;
        ClearNotchValidation();
    }

    private void ExecuteStep3PreviewWorkflow()
    {
        OnWorkflowStepChanged(WorkflowStepId.Step3NotchPreview, showStatus: false);
        RefreshNotchCanvasPreview(showStatus: true);
    }

    private void ExecuteClearWorkflowStep(WorkflowStepId step)
    {
        switch (step)
        {
            case WorkflowStepId.Step1Match:
                ClearNotchCanvasPreview();
                ClearPadMatchResult(clearRegularAssignments: true);
                ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
                CanvasHost?.Invalidate();
                SetStatus("Step 1 result cleared.");
                break;
            case WorkflowStepId.Step2Freeform:
                ClearStep2ResultCore();
                ResetWorkflowFromStep(WorkflowStepId.Step2Freeform, showStatus: false);
                SetStatus("Step 2 result cleared.");
                break;
            case WorkflowStepId.Step3NotchPreview:
                ClearStep3ResultCore();
                ResetWorkflowFromStep(WorkflowStepId.Step3NotchPreview, showStatus: false);
                SetStatus("Step 3 result cleared.");
                break;
            case WorkflowStepId.Step4IndexDiagnostics:
                ClearStep4ResultCore();
                ResetWorkflowFromStep(WorkflowStepId.Step4IndexDiagnostics, showStatus: false);
                SetStatus("Step 4 result cleared.");
                break;
            case WorkflowStepId.Step5Export:
                ClearStep5ResultCore();
                MoveToWorkflowStep(WorkflowStepId.Step5Export);
                SetStatus("Step 5 result cleared.");
                break;
        }
    }

    internal async Task<WorkflowStepExecutionResult> RunWorkflowStepAsync(WorkflowStepId step)
    {
        switch (step)
        {
            case WorkflowStepId.Step1Match:
                await MatchAsync();
                break;
            case WorkflowStepId.Step2Freeform:
                await AutoDetectFreeformsAsync();
                break;
            case WorkflowStepId.Step3NotchPreview:
                ExecuteStep3PreviewWorkflow();
                break;
            case WorkflowStepId.Step4IndexDiagnostics:
                await AnalyzeIndexMappingAsync();
                break;
            default:
                return WorkflowStepExecutionResult.Unsupported(step, BuildWorkflowStateSnapshot(), StatusText);
        }

        var snapshot = BuildWorkflowStateSnapshot();
        var isReady = step switch
        {
            WorkflowStepId.Step1Match => snapshot.HasStep1Result,
            WorkflowStepId.Step2Freeform => snapshot.HasStep2Result,
            WorkflowStepId.Step3NotchPreview => ShowNotchCanvasPreview || NotchCanvasPreviewItems.Count > 0,
            WorkflowStepId.Step4IndexDiagnostics => snapshot.HasStep4Result,
            _ => false
        };

        return new WorkflowStepExecutionResult(
            IsSupported: true,
            IsReady: isReady,
            Step: step,
            Snapshot: snapshot,
            StatusText: StatusText);
    }

    internal WorkflowStepExecutionResult ClearWorkflowStep(WorkflowStepId step)
    {
        if (step is < WorkflowStepId.Step1Match or > WorkflowStepId.Step5Export)
        {
            return WorkflowStepExecutionResult.Unsupported(step, BuildWorkflowStateSnapshot(), StatusText);
        }

        ExecuteClearWorkflowStep(step);
        var snapshot = BuildWorkflowStateSnapshot();
        return new WorkflowStepExecutionResult(
            IsSupported: true,
            IsReady: true,
            Step: step,
            Snapshot: snapshot,
            StatusText: StatusText);
    }
}

internal readonly record struct WorkflowStepExecutionResult(
    bool IsSupported,
    bool IsReady,
    WorkflowStepId Step,
    WorkflowStateSnapshot Snapshot,
    string? StatusText)
{
    public static WorkflowStepExecutionResult Unsupported(
        WorkflowStepId step,
        WorkflowStateSnapshot snapshot,
        string? statusText)
    {
        return new WorkflowStepExecutionResult(
            IsSupported: false,
            IsReady: false,
            Step: step,
            Snapshot: snapshot,
            StatusText: statusText);
    }
}
