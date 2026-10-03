namespace FreeformHelper.Application.Services;

/// <summary>
/// Provides user-facing text for To Full rule codes.
/// </summary>
public static class NotchToFullRuleText
{
    public static string ResolveReasonText(
        string? ruleCode,
        bool isToFullApplied,
        bool isBoundaryRegular)
    {
        var normalized = ruleCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ResolveFallback(isToFullApplied, isBoundaryRegular);
        }

        if (string.Equals(normalized, NotchToFullRuleEngine.RuleToFullDisabled, StringComparison.OrdinalIgnoreCase))
        {
            return "Global To Full switch is OFF.";
        }

        if (string.Equals(normalized, NotchToFullRuleEngine.RuleNonBoundary, StringComparison.OrdinalIgnoreCase))
        {
            return "Regular pad is not boundary.";
        }

        if (string.Equals(normalized, NotchToFullRuleEngine.RuleSourceEmpty, StringComparison.OrdinalIgnoreCase))
        {
            return "No valid source overlap in this regular pad.";
        }

        if (string.Equals(normalized, NotchToFullRuleEngine.RuleBlockedByNeighbor, StringComparison.OrdinalIgnoreCase))
        {
            return "Expansion blocked by neighboring CAD owners.";
        }

        if (string.Equals(normalized, NotchToFullRuleEngine.RuleExpandSharedReachable, StringComparison.OrdinalIgnoreCase))
        {
            return "Expansion applied on connected safe-fill path.";
        }

        if (string.Equals(normalized, NotchToFullRuleEngine.RuleNoExpansionNeeded, StringComparison.OrdinalIgnoreCase))
        {
            return "No effective expansion area remains.";
        }

        if (string.Equals(normalized, NotchToFullRuleEngine.RuleExpandClearPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Expansion applied on clear path.";
        }

        // Compatibility with older diagnostics payloads.
        if (string.Equals(normalized, "EXPAND_PARTIAL_WITH_BLOCKERS", StringComparison.OrdinalIgnoreCase))
        {
            return "Expansion applied, but limited by neighboring CAD blockers.";
        }

        return ResolveFallback(isToFullApplied, isBoundaryRegular);
    }

    public static string BuildReasonWithCode(
        string? ruleCode,
        bool isToFullApplied,
        bool isBoundaryRegular)
    {
        var text = ResolveReasonText(ruleCode, isToFullApplied, isBoundaryRegular);
        if (string.IsNullOrWhiteSpace(ruleCode))
        {
            return text;
        }

        return $"{text} ({ruleCode.Trim()})";
    }

    private static string ResolveFallback(bool isToFullApplied, bool isBoundaryRegular)
    {
        if (isToFullApplied)
        {
            return "Expansion applied.";
        }

        if (!isBoundaryRegular)
        {
            return "Regular pad is not boundary.";
        }

        return "To Full disabled by gate.";
    }
}
