// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchToFullRuleDecisionAdapterRejectionBehaviorTests
{
    [Theory]
    [InlineData(false, false, false, true, false, "GATE_TOFULL_DISABLED")]
    [InlineData(true, false, false, true, false, "GATE_SOURCE_EMPTY")]
    [InlineData(true, true, false, true, false, "NO_EXPANSION_NEEDED")]
    [InlineData(true, true, true, true, false, "GATE_MULTI_OWNER")]
    [InlineData(true, true, true, false, false, "NO_EXPANSION_NEEDED")]
    public void LegacyGateRejectsExpansionWithTheFirstApplicableReason(
        bool enableToFull,
        bool hasSourceArea,
        bool hasEffectiveExpansion,
        bool hasDirectionalBlocker,
        bool hasReachableExpansion,
        string expectedRuleCode)
    {
        var context = new NotchToFullRuleAdapterContext(
            EnableRuleEngine: false,
            EnableRuleTrace: true,
            EnableToFull: enableToFull,
            HasSourceArea: hasSourceArea,
            HasEffectiveExpansion: hasEffectiveExpansion,
            HasDirectionalBlocker: hasDirectionalBlocker,
            HasReachableExpansion: hasReachableExpansion);

        var decision = NotchToFullRuleDecisionAdapter.Evaluate(context);

        Assert.False(decision.ShouldApplyToFull);
        Assert.Equal(expectedRuleCode, decision.RuleCode);
        var trace = Assert.Single(decision.Trace);
        Assert.False(trace.Passed);
        Assert.Equal("legacy.inlineGate", trace.Rule);
        Assert.Equal($"legacy gate resolved as {expectedRuleCode}", trace.Detail);
    }
}
