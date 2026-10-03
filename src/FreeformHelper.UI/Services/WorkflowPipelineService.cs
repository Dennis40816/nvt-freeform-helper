namespace FreeformHelper.UI.Services;

/// <summary>
/// Centralized workflow pipeline policy:
/// - which downstream steps should be invalidated when a step changes
/// - which workflow block should be expanded/focused
/// - default status text for invalidation feedback
/// </summary>
public sealed class WorkflowPipelineService
{
    public static bool TryGetNextStepOnSuccess(WorkflowStepId completedStep, out WorkflowStepId nextStep)
    {
        switch (completedStep)
        {
            case WorkflowStepId.Step1Match:
                nextStep = WorkflowStepId.Step2Freeform;
                return true;
            case WorkflowStepId.Step2Freeform:
                // Step 3 owns export-affecting compensation guards, so keep it in the main path.
                // Step 4 remains optional diagnostics.
                nextStep = WorkflowStepId.Step3NotchPreview;
                return true;
            case WorkflowStepId.Step3NotchPreview:
                nextStep = WorkflowStepId.Step4IndexDiagnostics;
                return true;
            case WorkflowStepId.Step4IndexDiagnostics:
                nextStep = WorkflowStepId.Step5Export;
                return true;
            default:
                nextStep = default;
                return false;
        }
    }

    public static IReadOnlyList<WorkflowStepId> BuildProjectLoadReplayPlan(bool autoReplayStep2AfterLoad)
    {
        return autoReplayStep2AfterLoad
            ? new[] { WorkflowStepId.Step1Match, WorkflowStepId.Step2Freeform }
            : new[] { WorkflowStepId.Step1Match };
    }

    public static string GetProjectLoadReplayStartedMessage(WorkflowStepId step)
    {
        return step switch
        {
            WorkflowStepId.Step1Match => "Project load auto replay started: rebuilding Step 1 match.",
            WorkflowStepId.Step2Freeform => "Project load Step 2 auto replay started.",
            _ => "Project load auto replay started."
        };
    }

    public static string GetProjectLoadReplayCompletedMessage(WorkflowStepId step)
    {
        return step switch
        {
            WorkflowStepId.Step1Match => "Project load auto replay complete: Step 1 match restored.",
            WorkflowStepId.Step2Freeform => "Project load Step 2 auto replay complete.",
            _ => "Project load auto replay complete."
        };
    }

    public static string GetProjectLoadReplaySkippedMessage(WorkflowStepId step, string reason)
    {
        var suffix = string.IsNullOrWhiteSpace(reason)
            ? "unknown reason"
            : reason.Trim();
        return step switch
        {
            WorkflowStepId.Step1Match => $"Project load auto replay skipped: Step 1 ({suffix}).",
            WorkflowStepId.Step2Freeform => $"Project load auto replay skipped: Step 2 ({suffix}).",
            _ => $"Project load auto replay skipped: {suffix}."
        };
    }

    public static IReadOnlyList<WorkflowStepId> GetDownstreamStepsToInvalidate(WorkflowStepId changedStep)
    {
        return changedStep switch
        {
            WorkflowStepId.Step1Match => new[] { WorkflowStepId.Step2Freeform, WorkflowStepId.Step3NotchPreview, WorkflowStepId.Step4IndexDiagnostics, WorkflowStepId.Step5Export },
            WorkflowStepId.Step2Freeform => new[] { WorkflowStepId.Step3NotchPreview, WorkflowStepId.Step4IndexDiagnostics, WorkflowStepId.Step5Export },
            WorkflowStepId.Step3NotchPreview => new[] { WorkflowStepId.Step4IndexDiagnostics, WorkflowStepId.Step5Export },
            WorkflowStepId.Step4IndexDiagnostics => new[] { WorkflowStepId.Step5Export },
            _ => Array.Empty<WorkflowStepId>(),
        };
    }

    public static string? GetInvalidationStatusMessage(WorkflowStepId changedStep)
    {
        return changedStep switch
        {
            WorkflowStepId.Step1Match => "Step 1 changed: cleared Step 2-5 results.",
            WorkflowStepId.Step2Freeform => "Step 2 changed: cleared Step 3-5 results.",
            WorkflowStepId.Step3NotchPreview => "Step 3 changed: cleared Step 4-5 results.",
            WorkflowStepId.Step4IndexDiagnostics => "Step 4 changed: cleared Step 5 result.",
            _ => null,
        };
    }

    public static WorkflowStepExpansionState BuildExpansionState(WorkflowStepId activeStep)
    {
        return new WorkflowStepExpansionState(
            IsStep1Expanded: activeStep == WorkflowStepId.Step1Match,
            IsStep2Expanded: activeStep == WorkflowStepId.Step2Freeform,
            IsStep3Expanded: activeStep == WorkflowStepId.Step3NotchPreview,
            IsStep4Expanded: activeStep == WorkflowStepId.Step4IndexDiagnostics,
            IsStep5Expanded: activeStep == WorkflowStepId.Step5Export,
            IsStep6Expanded: false);
    }

    public static WorkflowStepId NormalizeStepOrDefault(int step)
    {
        return step switch
        {
            1 => WorkflowStepId.Step1Match,
            2 => WorkflowStepId.Step2Freeform,
            3 => WorkflowStepId.Step3NotchPreview,
            4 => WorkflowStepId.Step4IndexDiagnostics,
            5 => WorkflowStepId.Step5Export,
            _ => WorkflowStepId.Step1Match,
        };
    }
}

public readonly record struct WorkflowStepExpansionState(
    bool IsStep1Expanded,
    bool IsStep2Expanded,
    bool IsStep3Expanded,
    bool IsStep4Expanded,
    bool IsStep5Expanded,
    bool IsStep6Expanded);
