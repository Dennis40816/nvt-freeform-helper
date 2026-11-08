// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDisplayProjectorBehaviorTests
{
    private static readonly int[] _expectedOwnerCadIds = [1, 3, 5, 7, 9];
    private static readonly int[] _expectedRegularPadIds = [42];

    [Fact]
    public void Build_CompensationWithoutAllocation_ShowsSourceAreaAndUnavailableTargets()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        var cad = TestGeometryFactory.CreateCadPad(42, "PAD", 2, 1, 8, 9);
        var compensation = NotchV22CompensationService.Compute(
            cad, grid, enableToRegular: true, enableToFull: false);

        var display = NotchDisplayProjector.Build(compensation);

        Assert.Equal("Stage3 area: 48 mm²", display.Stage3AreaText);
        Assert.Equal("Undo NF (To Regular): 48.00%", display.ToRegularRatioText);
        Assert.Equal("To Full: 100.00% (disabled)", display.ToFullRatioText);
        Assert.Equal("48.00 %", display.CombinedValueText);
        Assert.Equal("Targets: -", display.TargetAllocationSummaryText);
        Assert.Empty(display.TargetAllocationLines);
        Assert.Empty(display.TargetAllocationItems);
        Assert.Empty(display.DiagnosticEntries);
        Assert.Empty(display.ToFullReasonFullText);
    }

    [Fact]
    public void Build_LegacyBlockedDiagnostics_CompactsOwnersAndDefaultsMissingCandidate()
    {
        using var culture = TestCulture.UseEnUs();
        const string diagnostics =
            "IC2/diff7 reg42: ov=1.5, src=2, blk=0.25, reach=1, blocker=4, " +
            "owners=9;3|7|1|5, boundary=Y, tofull=N (GATE_MULTI_OWNER)";

        var display = NotchDisplayProjector.Build(
            toRegularRatio: 0.75, toFullRatio: 1, combinedRatio: 0.75,
            isToFullEnabled: false, stage3Area: 2, targets: null, diagnostics: diagnostics);

        Assert.Equal(_expectedOwnerCadIds, display.OwnerCadIds);
        Assert.Equal("Owners 5: CAD 1, CAD 3, CAD 5, CAD 7 (+1)", display.OwnerSummaryText);
        Assert.Contains("Owners 5 (9, 3, 7 (+2))", display.ToFullDiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("Share  -", display.ToFullDiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("boundary=Y, candidate=Y, tofull=N, blocker=4",
            display.ToFullDiagnosticsText, StringComparison.Ordinal);
        Assert.Equal("Expansion blocked by neighboring CAD owners.", display.ToFullReasonShortText);
        Assert.Equal("Expansion blocked by neighboring CAD owners. (GATE_MULTI_OWNER)", display.ToFullReasonFullText);
        var entry = Assert.Single(display.DiagnosticEntries);
        Assert.True(entry.IsCandidate);
        Assert.False(entry.IsToFull);
        Assert.Equal(4, entry.BlockerCount);
    }

    [Fact]
    public void Build_TruncatedDiagnosticsWithoutAppliedEntry_UsesFirstAvailableReason()
    {
        using var culture = TestCulture.UseEnUs();
        // The applied regular is outside the truncated diagnostics preview.
        const string diagnostics =
            "IC1/diff7 reg42: ov=1, src=1, blk=1, reach=1, blocker=1, " +
            "boundary=Y, candidate=N, tofull=N (GATE_MULTI_OWNER)\n+ 3 more";

        var display = NotchDisplayProjector.Build(
            toRegularRatio: 0.5, toFullRatio: 1.5, combinedRatio: 0.75,
            isToFullEnabled: true, stage3Area: 3, targets: null, diagnostics: diagnostics);

        Assert.False(Assert.Single(display.DiagnosticEntries).IsToFull);
        Assert.Equal("Expansion blocked by neighboring CAD owners.", display.ToFullReasonShortText);
        Assert.Equal("Expansion blocked by neighboring CAD owners. (GATE_MULTI_OWNER)", display.ToFullReasonFullText);
        Assert.Contains("+ 3 more", display.ToFullDiagnosticsText, StringComparison.Ordinal);
        Assert.Equal("To Full: 150.00% (enabled)", display.ToFullRatioText);
    }

    [Fact]
    public void Build_TargetWithLegacyRegularIds_PreservesSelectablePadAndArea()
    {
        using var culture = TestCulture.UseEnUs();
        var target = new NotchDisplayTargetInput(
            IcIndex: 1, DiffIndex: 7, EffectiveArea: 12.5, Ratio: 0.625,
            RatioPercentRounded: 63, PassesStrictThreshold: true, IsAnchorDiff: false,
            ToFullAppliedRegularCount: 0, RegularCount: 1, RegularAreas: [], RegularPadIds: [42]);

        var display = NotchDisplayProjector.Build(
            toRegularRatio: 0.625, toFullRatio: 1, combinedRatio: 0.625,
            isToFullEnabled: false, stage3Area: 12.5, targets: [target], diagnostics: null);

        Assert.Equal("Targets: 1 effective / 1 total", display.TargetAllocationSummaryText);
        Assert.Equal("IC2/diff7  A 12.5 mm²  R 63%", Assert.Single(display.TargetAllocationLines));
        var item = Assert.Single(display.TargetAllocationItems);
        Assert.Equal("Target", item.RoleText);
        Assert.Equal("REG 42", item.TargetText);
        Assert.Equal("IC2/diff7", item.DiffText);
        Assert.Equal("12.5 mm²", item.AreaText);
        Assert.Equal("63%", item.RatioText);
        Assert.Equal(_expectedRegularPadIds, item.RegularPadIds);
        Assert.Equal("REG 42 · IC2/diff7 · 12.5 mm² · 63%", item.RawText);
    }
}
