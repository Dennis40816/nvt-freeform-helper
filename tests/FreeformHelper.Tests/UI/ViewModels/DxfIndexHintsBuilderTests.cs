// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfIndexHintsBuilderTests
{
    private static readonly int[] _expectedFiveAnchorCadIds = [10, 20, 30, 40, 50];
    private static readonly int[] _expectedOverrideCadIds = [11, 13];
    private static readonly int[] _expectedVisibleAnchorCadIds = [12, 11];

    [Fact]
    public void BuildWithoutOverridesOrAnchorsReturnsExactSummaryWithoutIssues()
    {
        using var culture = TestCulture.UseEnUs();

        var result = DxfIndexHintsBuilder.Build(
            new HashSet<int> { 11, 12 }, new Dictionary<int, int>(), new Dictionary<int, int>(),
            ScanOrder.LeftToRight_TopToBottom, 1, CadOutputFwDiffAutoMode.StrictUnique, CreateAssignment());

        Assert.Empty(result.OverrideCadIds);
        Assert.Empty(result.AnchorCadIdsForCanvas);
        Assert.Equal(
            "CAD Output FW Diff assignment: override=0, auto=2, unresolved=0, IC groups=1, anchor=none, " +
            "scan=LeftToRight_TopToBottom. Mode: geometry seed (unique per IC). " +
            "Priority: override > direct geometry-seed (per-IC unique).",
            result.Summary);
    }

    [Fact]
    public void BuildWithFiveAnchorsShowsFirstFourInIcOrderAndMoreMark()
    {
        using var culture = TestCulture.UseEnUs();
        var anchors = new Dictionary<int, int> { [4] = 50, [0] = 10, [3] = 40, [2] = 30, [1] = 20 };

        var result = DxfIndexHintsBuilder.Build(
            new HashSet<int> { 10, 20, 30, 40, 50 }, new Dictionary<int, int>(), anchors,
            ScanOrder.LeftToRight_TopToBottom, 5, CadOutputFwDiffAutoMode.StrictUnique, CreateAssignment());

        Assert.Equal(_expectedFiveAnchorCadIds, result.AnchorCadIdsForCanvas);
        Assert.Contains(
            "anchor=stored (inactive in current modes: IC1->CAD 10, IC2->CAD 20, IC3->CAD 30, IC4->CAD 40 (+))",
            result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFiltersHiddenIdsSortsOverridesAndKeepsDistinctAnchorsInIcOrder()
    {
        using var culture = TestCulture.UseEnUs();
        var overrides = new Dictionary<int, int> { [13] = 8, [99] = 9, [11] = 7 };
        var anchors = new Dictionary<int, int> { [3] = 12, [1] = 99, [2] = 11, [0] = 12 };

        var result = DxfIndexHintsBuilder.Build(
            new HashSet<int> { 11, 12, 13 }, overrides, anchors,
            ScanOrder.LeftToRight_TopToBottom, 4, CadOutputFwDiffAutoMode.StrictUnique, CreateAssignment());

        Assert.Equal(_expectedOverrideCadIds, result.OverrideCadIds);
        Assert.Equal(_expectedVisibleAnchorCadIds, result.AnchorCadIdsForCanvas);
        Assert.Contains(
            "anchor=stored (inactive in current modes: IC1->CAD 12, IC2->CAD 99, IC3->CAD 11, IC4->CAD 12)",
            result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CadOutputFwDiffAutoMode.BestMatchDirect, "geometry seed", "override > direct geometry-seed")]
    [InlineData(CadOutputFwDiffAutoMode.StrictUnique, "geometry seed (unique per IC)", "override > direct geometry-seed (per-IC unique)")]
    public void BuildUsesModePriorityAndSelectedScanOrder(CadOutputFwDiffAutoMode mode, string modeText, string priorityText)
    {
        using var culture = TestCulture.UseEnUs();

        var result = DxfIndexHintsBuilder.Build(
            new HashSet<int> { 11, 12 }, new Dictionary<int, int>(), new Dictionary<int, int>(),
            ScanOrder.RightToLeft_BottomToTop, 2, mode, CreateAssignment());

        Assert.Contains("IC groups=2, anchor=none, scan=RightToLeft_BottomToTop.", result.Summary, StringComparison.Ordinal);
        Assert.EndsWith($"Mode: {modeText}. Priority: {priorityText}.", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(" | issues:", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWithConflictsIncludesUnresolvedTotalAndExactQualityFields()
    {
        using var culture = TestCulture.UseEnUs();
        var assignment = CreateAssignment() with
        {
            DuplicateOverrides = 1,
            InvalidOverrides = 2,
            AutoConflictCount = 3,
            AutoUnmatchedCount = 4,
            OverrideMismatchCount = 5,
        };

        var result = DxfIndexHintsBuilder.Build(
            new HashSet<int> { 11, 12 }, new Dictionary<int, int>(), new Dictionary<int, int>(),
            ScanOrder.LeftToRight_TopToBottom, 1, CadOutputFwDiffAutoMode.BestMatchDirect, assignment);

        Assert.Contains("override=0, auto=2, unresolved=7,", result.Summary, StringComparison.Ordinal);
        Assert.EndsWith(
            " | issues: overrideDuplicate=1, overrideInvalid=2, autoConflict=3, autoUnmatched=4, overrideMismatch=5.",
            result.Summary, StringComparison.Ordinal);
    }

    private static CadOutputFwDiffIndexAssignmentResult CreateAssignment() => new(
        AssignedIndexByCadId: new Dictionary<int, int> { [11] = 7, [12] = 8 },
        CadIcIndexByCadId: new Dictionary<int, int> { [11] = 0, [12] = 0 },
        SequenceAdjustedCadIds: Array.Empty<int>(),
        ConflictSamples: Array.Empty<string>(),
        UnmatchedSamples: Array.Empty<string>(),
        OverrideAssignedCount: 0,
        AutoAssignedCount: 2,
        AutoConflictCount: 0,
        AutoUnmatchedCount: 0,
        DuplicateOverrides: 0,
        InvalidOverrides: 0,
        OverrideMismatchCount: 0,
        SequenceAdjustedCount: 0,
        AnchorAppliedCount: 0,
        AnchorIgnoredCount: 0,
        RowSequenceAppliedCount: 0,
        RowSequenceFallbackCount: 0);
}
