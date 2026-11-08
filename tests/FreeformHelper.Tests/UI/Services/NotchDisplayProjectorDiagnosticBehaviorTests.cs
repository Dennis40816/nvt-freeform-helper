// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDisplayProjectorDiagnosticBehaviorTests
{
    [Fact]
    public void Build_WithoutEmittedMembership_ExcludesAnchorAndBelowGateTargetsFromEffectiveList()
    {
        using var culture = TestCulture.UseEnUs();
        NotchDisplayTargetInput[] targets =
        [
            new(IcIndex: 0, DiffIndex: 7, EffectiveArea: 50, Ratio: 0.5,
                RatioPercentRounded: 50, PassesStrictThreshold: true, IsAnchorDiff: true,
                ToFullAppliedRegularCount: 0, RegularCount: 1,
                RegularAreas: [new NotchDisplayRegularAreaInput(42, 50)], RegularPadIds: [42]),
            new(IcIndex: 0, DiffIndex: 8, EffectiveArea: 30, Ratio: 0.3,
                RatioPercentRounded: 30, PassesStrictThreshold: true, IsAnchorDiff: false,
                ToFullAppliedRegularCount: 0, RegularCount: 1,
                RegularAreas: [new NotchDisplayRegularAreaInput(43, 30)], RegularPadIds: [43]),
            new(IcIndex: 0, DiffIndex: 9, EffectiveArea: 20, Ratio: 0.2,
                RatioPercentRounded: 20, PassesStrictThreshold: false, IsAnchorDiff: false,
                ToFullAppliedRegularCount: 0, RegularCount: 1,
                RegularAreas: [new NotchDisplayRegularAreaInput(44, 20)], RegularPadIds: [44]),
        ];

        var display = NotchDisplayProjector.Build(
            toRegularRatio: 1, toFullRatio: 1, combinedRatio: 1,
            isToFullEnabled: false, stage3Area: 100, targets: targets, diagnostics: null);

        Assert.Equal("Targets: 1 effective / 3 total", display.TargetAllocationSummaryText);
        Assert.Equal("IC1/diff8  A 30 mm²  R 30%", Assert.Single(display.TargetAllocationLines));
        Assert.Collection(display.TargetAllocationItems,
            item =>
            {
                Assert.Equal("Self before To Full", item.RoleText);
                Assert.Equal(42, Assert.Single(item.RegularPadIds));
            },
            item =>
            {
                Assert.Equal("Target", item.RoleText);
                Assert.Equal(43, Assert.Single(item.RegularPadIds));
            },
            item =>
            {
                Assert.Equal("Below gate", item.RoleText);
                Assert.Equal(44, Assert.Single(item.RegularPadIds));
            });
    }

    [Fact]
    public void Build_InvalidNumericDiagnostics_PreservesRawLinesAndUsesOnlyTheValidReason()
    {
        using var culture = TestCulture.UseEnUs();
        const string validLine =
            "IC2/diff7 reg42: ov=1.5, src=2, blk=0.25, reach=1, blocker=4, " +
            "boundary=Y, candidate=Y, tofull=N (GATE_MULTI_OWNER)";
        string[] damagedLines =
        [
            validLine.Replace("IC2/", "IC2147483648/", StringComparison.Ordinal),
            validLine.Replace("/diff7 ", "/diff2147483648 ", StringComparison.Ordinal),
            validLine.Replace("reg42:", "reg2147483648:", StringComparison.Ordinal),
            validLine.Replace("ov=1.5,", "ov=1..5,", StringComparison.Ordinal),
            validLine.Replace("src=2,", "src=2..0,", StringComparison.Ordinal),
            validLine.Replace("blk=0.25,", "blk=0..25,", StringComparison.Ordinal),
            validLine.Replace("reach=1,", "reach=1..0,", StringComparison.Ordinal),
            validLine.Replace("blocker=4,", "blocker=2147483648,", StringComparison.Ordinal),
        ];
        var diagnostics = string.Join("\n", damagedLines.Append(validLine));

        var display = NotchDisplayProjector.Build(
            toRegularRatio: 0.75, toFullRatio: 1, combinedRatio: 0.75,
            isToFullEnabled: false, stage3Area: 2, targets: null, diagnostics: diagnostics);

        var entry = Assert.Single(display.DiagnosticEntries);
        Assert.Equal(2, entry.IcIndex);
        Assert.Equal(7, entry.DiffIndex);
        Assert.Equal(42, entry.RegularPadId);
        Assert.Equal(1.5, entry.OverlapArea);
        Assert.Equal(2.0, entry.SourceArea);
        Assert.Equal(0.25, entry.BlockedArea);
        Assert.Equal(1.0, entry.ReachableArea);
        Assert.Equal(4, entry.BlockerCount);
        Assert.Equal("Expansion blocked by neighboring CAD owners. (GATE_MULTI_OWNER)", display.ToFullReasonFullText);
        var rawPrefix = string.Join("\n\n", damagedLines) + "\n\n";
        Assert.StartsWith(rawPrefix,
            display.ToFullDiagnosticsText.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("IC2/diff7  REG42", display.ToFullDiagnosticsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DiagnosticWithoutRuleOrCandidate_ShowsFallbackReasonAndNoOwners()
    {
        using var culture = TestCulture.UseEnUs();
        const string diagnostics =
            "IC2/diff7 reg42: ov=1.5, src=2, blk=0.25, reach=1, blocker=0, " +
            "owners=-, boundary=N, tofull=N";

        var display = NotchDisplayProjector.Build(
            toRegularRatio: 0.75, toFullRatio: 1, combinedRatio: 0.75,
            isToFullEnabled: false, stage3Area: 2, targets: null, diagnostics: diagnostics);

        var entry = Assert.Single(display.DiagnosticEntries);
        Assert.Equal("UNKNOWN_RULE", entry.RuleCode);
        Assert.True(entry.IsCandidate);
        Assert.False(entry.IsBoundary);
        Assert.False(entry.IsToFull);
        Assert.Empty(entry.OwnerCadPadIds);
        Assert.Empty(display.OwnerCadIds);
        Assert.Empty(display.OwnerSummaryText);
        Assert.Equal("Regular pad is not boundary.", display.ToFullReasonShortText);
        Assert.Equal("Regular pad is not boundary. (UNKNOWN_RULE)", display.ToFullReasonFullText);
        const string expectedBlock =
            "IC2/diff7  REG42\nArea  ov=1.5, src=2, blk=0.25, reach=1\nOwners 0 (-)\nShare  -\n" +
            "Flags  boundary=N, candidate=Y, tofull=N, blocker=0\nRule   Regular pad is not boundary. (UNKNOWN_RULE)";
        Assert.Equal(expectedBlock, display.ToFullDiagnosticsText.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
