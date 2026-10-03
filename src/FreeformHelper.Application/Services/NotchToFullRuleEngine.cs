namespace FreeformHelper.Application.Services;

/// <summary>
/// Evaluates per-regular To Full gate decisions and optional trace details.
/// </summary>
public sealed class NotchToFullRuleEngine
{
    public const string RuleToFullDisabled = "GATE_TOFULL_DISABLED";
    public const string RuleNonBoundary = "GATE_NOT_BOUNDARY";
    public const string RuleSourceEmpty = "GATE_SOURCE_EMPTY";
    public const string RuleBlockedByNeighbor = "GATE_MULTI_OWNER";
    public const string RuleNoExpansionNeeded = "NO_EXPANSION_NEEDED";
    public const string RuleExpandClearPath = "EXPAND_CLEAR_PATH";
    public const string RuleExpandSharedReachable = "EXPAND_SHARED_REACHABLE";
    private static readonly IReadOnlyList<NotchToFullRuleTraceEntry> EmptyTrace = Array.Empty<NotchToFullRuleTraceEntry>();

    public static NotchToFullRuleDecision Evaluate(in NotchToFullRuleContext context, bool enableTrace)
    {
        List<NotchToFullRuleTraceEntry>? trace = enableTrace ? new List<NotchToFullRuleTraceEntry>(6) : null;

        if (!context.EnableToFull)
        {
            AppendTrace(trace, "switch.enableToFull", passed: false, "global To Full switch is OFF");
            return new NotchToFullRuleDecision(
                RuleCode: RuleToFullDisabled,
                ShouldApplyToFull: false,
                Trace: FreezeTrace(trace));
        }

        AppendTrace(trace, "switch.enableToFull", passed: true, "global To Full switch is ON");

        if (!context.HasSourceArea)
        {
            AppendTrace(trace, "gate.sourceArea", passed: false, "source overlap <= strict threshold");
            return new NotchToFullRuleDecision(
                RuleCode: RuleSourceEmpty,
                ShouldApplyToFull: false,
                Trace: FreezeTrace(trace));
        }

        AppendTrace(trace, "gate.sourceArea", passed: true, "source overlap > strict threshold");

        if (!context.HasEffectiveExpansion)
        {
            AppendTrace(trace, "gate.effectiveExpansion", passed: false, "source already near full regular");
            return new NotchToFullRuleDecision(
                RuleCode: RuleNoExpansionNeeded,
                ShouldApplyToFull: false,
                Trace: FreezeTrace(trace));
        }

        AppendTrace(trace, "gate.effectiveExpansion", passed: true, "expansion area exists");

        if (!context.HasReachableExpansion)
        {
            if (context.HasDirectionalBlocker)
            {
                AppendTrace(trace, "gate.reachableExpansion", passed: false, "blocked by neighboring owners");
                return new NotchToFullRuleDecision(
                    RuleCode: RuleBlockedByNeighbor,
                    ShouldApplyToFull: false,
                    Trace: FreezeTrace(trace));
            }

            AppendTrace(trace, "gate.reachableExpansion", passed: false, "no reachable expansion delta");
            return new NotchToFullRuleDecision(
                RuleCode: RuleNoExpansionNeeded,
                ShouldApplyToFull: false,
                Trace: FreezeTrace(trace));
        }

        if (context.HasDirectionalBlocker)
        {
            AppendTrace(trace, "gate.reachableExpansion", passed: true, "shared regular has connected safe fill");
            return new NotchToFullRuleDecision(
                RuleCode: RuleExpandSharedReachable,
                ShouldApplyToFull: true,
                Trace: FreezeTrace(trace));
        }

        AppendTrace(trace, "gate.reachableExpansion", passed: true, "clear path to expand");
        return new NotchToFullRuleDecision(
            RuleCode: RuleExpandClearPath,
            ShouldApplyToFull: true,
            Trace: FreezeTrace(trace));
    }

    public static NotchToFullRuleDecision CreateLegacyDecision(
        string ruleCode,
        bool shouldApplyToFull,
        bool enableTrace)
    {
        List<NotchToFullRuleTraceEntry>? trace = enableTrace ? new List<NotchToFullRuleTraceEntry>(1) : null;
        AppendTrace(
            trace,
            rule: "legacy.inlineGate",
            passed: shouldApplyToFull,
            detail: $"legacy gate resolved as {ruleCode}");
        return new NotchToFullRuleDecision(
            RuleCode: ruleCode,
            ShouldApplyToFull: shouldApplyToFull,
            Trace: FreezeTrace(trace));
    }

    private static void AppendTrace(
        List<NotchToFullRuleTraceEntry>? trace,
        string rule,
        bool passed,
        string detail)
    {
        if (trace is null)
        {
            return;
        }

        trace.Add(new NotchToFullRuleTraceEntry(rule, passed, detail));
    }

    private static IReadOnlyList<NotchToFullRuleTraceEntry> FreezeTrace(List<NotchToFullRuleTraceEntry>? trace)
    {
        return trace is null ? EmptyTrace : trace;
    }
}

public readonly record struct NotchToFullRuleContext(
    bool EnableToFull,
    bool HasSourceArea,
    bool HasEffectiveExpansion,
    bool HasDirectionalBlocker,
    bool HasReachableExpansion);

public sealed record NotchToFullRuleTraceEntry(
    string Rule,
    bool Passed,
    string Detail);

public sealed record NotchToFullRuleDecision(
    string RuleCode,
    bool ShouldApplyToFull,
    IReadOnlyList<NotchToFullRuleTraceEntry> Trace);
