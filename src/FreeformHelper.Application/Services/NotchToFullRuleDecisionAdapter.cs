namespace FreeformHelper.Application.Services;

/// <summary>
/// Adapts legacy inline To Full gate behavior into the same decision contract used by the rule engine.
/// </summary>
public static class NotchToFullRuleDecisionAdapter
{
    public static NotchToFullRuleDecision Evaluate(in NotchToFullRuleAdapterContext context)
    {
        if (context.EnableRuleEngine)
        {
            return NotchToFullRuleEngine.Evaluate(
                new NotchToFullRuleContext(
                    EnableToFull: context.EnableToFull,
                    HasSourceArea: context.HasSourceArea,
                    HasEffectiveExpansion: context.HasEffectiveExpansion,
                    HasDirectionalBlocker: context.HasDirectionalBlocker,
                    HasReachableExpansion: context.HasReachableExpansion),
                enableTrace: context.EnableRuleTrace);
        }

        var legacyRuleCode = ResolveLegacyRuleCode(
            context.EnableToFull,
            context.HasSourceArea,
            context.HasEffectiveExpansion,
            context.HasDirectionalBlocker,
            context.HasReachableExpansion);
        var legacyShouldApply = string.Equals(
            legacyRuleCode,
            NotchToFullRuleEngine.RuleExpandClearPath,
            StringComparison.Ordinal) ||
            string.Equals(
                legacyRuleCode,
                NotchToFullRuleEngine.RuleExpandSharedReachable,
            StringComparison.Ordinal);

        return NotchToFullRuleEngine.CreateLegacyDecision(
            legacyRuleCode,
            legacyShouldApply,
            enableTrace: context.EnableRuleTrace);
    }

    private static string ResolveLegacyRuleCode(
        bool enableToFull,
        bool hasSourceArea,
        bool hasEffectiveExpansion,
        bool hasDirectionalBlocker,
        bool hasReachableExpansion)
    {
        if (!enableToFull)
        {
            return NotchToFullRuleEngine.RuleToFullDisabled;
        }

        if (!hasSourceArea)
        {
            return NotchToFullRuleEngine.RuleSourceEmpty;
        }

        if (!hasEffectiveExpansion)
        {
            return NotchToFullRuleEngine.RuleNoExpansionNeeded;
        }

        if (!hasReachableExpansion)
        {
            return hasDirectionalBlocker
                ? NotchToFullRuleEngine.RuleBlockedByNeighbor
                : NotchToFullRuleEngine.RuleNoExpansionNeeded;
        }

        return hasDirectionalBlocker
            ? NotchToFullRuleEngine.RuleExpandSharedReachable
            : NotchToFullRuleEngine.RuleExpandClearPath;
    }
}

public readonly record struct NotchToFullRuleAdapterContext(
    bool EnableRuleEngine,
    bool EnableRuleTrace,
    bool EnableToFull,
    bool HasSourceArea,
    bool HasEffectiveExpansion,
    bool HasDirectionalBlocker,
    bool HasReachableExpansion);
