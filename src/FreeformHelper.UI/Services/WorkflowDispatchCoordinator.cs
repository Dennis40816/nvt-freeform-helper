// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Dispatches run, clear and navigation effects in their existing synchronous order.</summary>
internal sealed class WorkflowDispatchCoordinator(IWorkflowDispatchPort port)
{
    internal void MoveToWorkflowStep(WorkflowStepId step)
    {
        port.ApplyExpansion(WorkflowPipelineService.BuildExpansionState(step));
    }

    private void TryMoveToNextWorkflowStep(WorkflowStepId completedStep)
    {
        if (!WorkflowPipelineService.TryGetNextStepOnSuccess(completedStep, out var nextStep))
        {
            return;
        }

        if (port.CanAdvanceTo(nextStep))
        {
            MoveToWorkflowStep(nextStep);
        }
    }

    internal void OnWorkflowStepChanged(WorkflowStepId changedStep, bool showStatus = false)
    {
        InvalidateDownstreamFromStep(changedStep, showStatus);
    }

    internal void OnWorkflowStepCompleted(
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

    internal void ResetWorkflowFromStep(WorkflowStepId changedStep, bool showStatus = false)
    {
        OnWorkflowStepChanged(changedStep, showStatus);
        MoveToWorkflowStep(changedStep);
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
            port.SetStatus(message);
        }
    }

    private void ClearStepResultCore(WorkflowStepId step)
    {
        switch (step)
        {
            case WorkflowStepId.Step2Freeform:
                port.ClearStep2Result();
                break;
            case WorkflowStepId.Step3NotchPreview:
                port.ClearStep3Result();
                break;
            case WorkflowStepId.Step4IndexDiagnostics:
                port.ClearStep4Result();
                break;
            case WorkflowStepId.Step5Export:
                port.ClearStep5Result();
                break;
        }
    }

    internal void ExecuteStep3PreviewWorkflow()
    {
        OnWorkflowStepChanged(WorkflowStepId.Step3NotchPreview, showStatus: false);
        port.RefreshStep3Preview();
    }

    internal void ExecuteClearWorkflowStep(WorkflowStepId step)
    {
        switch (step)
        {
            case WorkflowStepId.Step1Match:
                port.ClearStep1Result();
                ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
                port.InvalidateCanvas();
                port.SetStatus("Step 1 result cleared.");
                break;
            case WorkflowStepId.Step2Freeform:
                port.ClearStep2Result();
                ResetWorkflowFromStep(WorkflowStepId.Step2Freeform, showStatus: false);
                port.SetStatus("Step 2 result cleared.");
                break;
            case WorkflowStepId.Step3NotchPreview:
                port.ClearStep3Result();
                ResetWorkflowFromStep(WorkflowStepId.Step3NotchPreview, showStatus: false);
                port.SetStatus("Step 3 result cleared.");
                break;
            case WorkflowStepId.Step4IndexDiagnostics:
                port.ClearStep4Result();
                ResetWorkflowFromStep(WorkflowStepId.Step4IndexDiagnostics, showStatus: false);
                port.SetStatus("Step 4 result cleared.");
                break;
            case WorkflowStepId.Step5Export:
                port.ClearStep5Result();
                MoveToWorkflowStep(WorkflowStepId.Step5Export);
                port.SetStatus("Step 5 result cleared.");
                break;
        }
    }

    internal async Task<WorkflowStepExecutionResult> RunWorkflowStepAsync(WorkflowStepId step)
    {
        switch (step)
        {
            case WorkflowStepId.Step1Match:
                await port.MatchAsync();
                break;
            case WorkflowStepId.Step2Freeform:
                await port.AutoDetectFreeformsAsync();
                break;
            case WorkflowStepId.Step3NotchPreview:
                ExecuteStep3PreviewWorkflow();
                break;
            case WorkflowStepId.Step4IndexDiagnostics:
                await port.AnalyzeIndexMappingAsync();
                break;
            default:
                return WorkflowStepExecutionResult.Unsupported(step, port.BuildWorkflowStateSnapshot(), port.StatusText);
        }

        var snapshot = port.BuildWorkflowStateSnapshot();
        var isReady = step switch
        {
            WorkflowStepId.Step1Match => snapshot.HasStep1Result,
            WorkflowStepId.Step2Freeform => snapshot.HasStep2Result,
            WorkflowStepId.Step3NotchPreview => snapshot.HasStep3Result,
            WorkflowStepId.Step4IndexDiagnostics => snapshot.HasStep4Result,
            _ => false
        };

        return new WorkflowStepExecutionResult(
            IsSupported: true,
            IsReady: isReady,
            Step: step,
            Snapshot: snapshot,
            StatusText: port.StatusText);
    }

    internal WorkflowStepExecutionResult ClearWorkflowStep(WorkflowStepId step)
    {
        if (step is < WorkflowStepId.Step1Match or > WorkflowStepId.Step5Export)
        {
            return WorkflowStepExecutionResult.Unsupported(step, port.BuildWorkflowStateSnapshot(), port.StatusText);
        }

        ExecuteClearWorkflowStep(step);
        var snapshot = port.BuildWorkflowStateSnapshot();
        return new WorkflowStepExecutionResult(
            IsSupported: true,
            IsReady: true,
            Step: step,
            Snapshot: snapshot,
            StatusText: port.StatusText);
    }
}
