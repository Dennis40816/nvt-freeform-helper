namespace FreeformHelper.UI.Services;

/// <summary>
/// Centralized workflow step dependency rules.
/// Keeps step gating logic in one place to avoid scattered ad-hoc checks.
/// </summary>
public sealed class WorkflowStepGateService
{
    public static WorkflowStepGateResult Validate(WorkflowStepId step, in WorkflowStateSnapshot snapshot)
    {
        return step switch
        {
            WorkflowStepId.Step1Match => ValidateStep1(snapshot),
            WorkflowStepId.Step2Freeform => ValidateStep2(snapshot),
            WorkflowStepId.Step3NotchPreview => ValidateStep3(snapshot),
            WorkflowStepId.Step4IndexDiagnostics => ValidateStep4(snapshot),
            WorkflowStepId.Step5Export => ValidateStep5(snapshot),
            _ => WorkflowStepGateResult.Blocked("Unknown workflow step."),
        };
    }

    private static WorkflowStepGateResult ValidateStep1(in WorkflowStateSnapshot s)
    {
        if (!s.HasGrid || !s.HasCad)
        {
            return WorkflowStepGateResult.Blocked("Step 1: import DXF and build grid first.");
        }

        return WorkflowStepGateResult.Allowed();
    }

    private static WorkflowStepGateResult ValidateStep2(in WorkflowStateSnapshot s)
    {
        var step1 = ValidateStep1(s);
        if (!step1.IsAllowed)
        {
            return WorkflowStepGateResult.Blocked("Step 2: import DXF and run Step 1 first.");
        }

        if (!s.HasStep1Result)
        {
            return WorkflowStepGateResult.Blocked("Step 2: run Step 1 (Match) first.");
        }

        return WorkflowStepGateResult.Allowed();
    }

    private static WorkflowStepGateResult ValidateStep3(in WorkflowStateSnapshot s)
    {
        var step1 = ValidateStep1(s);
        if (!step1.IsAllowed)
        {
            return WorkflowStepGateResult.Blocked("Step 3: import DXF and build grid first.");
        }

        if (!s.HasStep1Result)
        {
            return WorkflowStepGateResult.Blocked("Step 3: run Step 1 (Match) first.");
        }

        return WorkflowStepGateResult.Allowed();
    }

    private static WorkflowStepGateResult ValidateStep4(in WorkflowStateSnapshot s)
    {
        var step1 = ValidateStep1(s);
        if (!step1.IsAllowed)
        {
            return WorkflowStepGateResult.Blocked("Step 4: import DXF and build grid first.");
        }

        if (!s.HasStep1Result)
        {
            return WorkflowStepGateResult.Blocked("Step 4: run Step 1 (Match) first.");
        }

        return WorkflowStepGateResult.Allowed();
    }

    private static WorkflowStepGateResult ValidateStep5(in WorkflowStateSnapshot s)
    {
        var step1 = ValidateStep1(s);
        if (!step1.IsAllowed)
        {
            return WorkflowStepGateResult.Blocked("Step 5: import DXF and run Step 1 first. Step 4 is optional diagnostics.");
        }

        if (!s.HasStep1Result)
        {
            return WorkflowStepGateResult.Blocked("Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional.");
        }

        return WorkflowStepGateResult.Allowed();
    }
}

public enum WorkflowStepId
{
    Step1Match = 1,
    Step2Freeform = 2,
    Step3NotchPreview = 3,
    Step4IndexDiagnostics = 4,
    Step5Export = 5,
}

public readonly record struct WorkflowStateSnapshot(
    bool HasCad,
    bool HasGrid,
    bool HasStep1Result,
    bool HasStep2Result,
    bool HasStep3Result,
    bool HasStep4Result);

public readonly record struct WorkflowStepGateResult(bool IsAllowed, string? Message)
{
    public static WorkflowStepGateResult Allowed() => new(true, null);

    public static WorkflowStepGateResult Blocked(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new WorkflowStepGateResult(false, message);
    }
}
