// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchToFullRuleTextBehaviorTests
{
    [Theory]
    [InlineData(" GATE_TOFULL_DISABLED ", false, true, "Global To Full switch is OFF. (GATE_TOFULL_DISABLED)")]
    [InlineData("gate_not_boundary", false, false, "Regular pad is not boundary. (gate_not_boundary)")]
    [InlineData("GATE_SOURCE_EMPTY", false, true, "No valid source overlap in this regular pad. (GATE_SOURCE_EMPTY)")]
    [InlineData("GATE_MULTI_OWNER", false, true, "Expansion blocked by neighboring CAD owners. (GATE_MULTI_OWNER)")]
    [InlineData("EXPAND_SHARED_REACHABLE", true, true,
        "Expansion applied on connected safe-fill path. (EXPAND_SHARED_REACHABLE)")]
    [InlineData("NO_EXPANSION_NEEDED", false, true, "No effective expansion area remains. (NO_EXPANSION_NEEDED)")]
    [InlineData("EXPAND_CLEAR_PATH", true, true, "Expansion applied on clear path. (EXPAND_CLEAR_PATH)")]
    [InlineData(" expand_partial_with_blockers ", true, true,
        "Expansion applied, but limited by neighboring CAD blockers. (expand_partial_with_blockers)")]
    [InlineData(null, true, false, "Expansion applied.")]
    [InlineData(null, false, false, "Regular pad is not boundary.")]
    [InlineData(null, false, true, "To Full disabled by gate.")]
    [InlineData("", true, true, "Expansion applied.")]
    [InlineData(" \t ", false, true, "To Full disabled by gate.")]
    [InlineData(" FUTURE_RULE ", true, false, "Expansion applied. (FUTURE_RULE)")]
    [InlineData("FUTURE_RULE", false, false, "Regular pad is not boundary. (FUTURE_RULE)")]
    [InlineData("FUTURE_RULE", false, true, "To Full disabled by gate. (FUTURE_RULE)")]
    public void BuildReasonWithCode_ExplainsCurrentAndLegacyRulesAndFallsBackForMissingOrUnknownCodes(
        string? ruleCode, bool isToFullApplied, bool isBoundaryRegular, string expected)
    {
        var reason = NotchToFullRuleText.BuildReasonWithCode(ruleCode, isToFullApplied, isBoundaryRegular);

        Assert.Equal(expected, reason);
    }
}
