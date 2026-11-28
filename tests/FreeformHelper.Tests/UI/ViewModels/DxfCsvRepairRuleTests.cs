// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfCsvRepairRuleTests
{
    private static readonly int[] _expectedAppliedCadIds = [101, 103];

    [Theory]
    [InlineData(CadOutputFwDiffAssignmentMode.GeometryOnly, 0.9, 7, 2, false)]
    [InlineData(CadOutputFwDiffAssignmentMode.CsvConstrained, 0.79, 7, 2, false)]
    [InlineData(CadOutputFwDiffAssignmentMode.CsvConstrained, null, 7, 2, false)]
    [InlineData(CadOutputFwDiffAssignmentMode.CsvConstrained, 0.9, null, 2, false)]
    [InlineData(CadOutputFwDiffAssignmentMode.CsvConstrained, 0.9, 7, 7, false)]
    [InlineData(CadOutputFwDiffAssignmentMode.CsvConstrained, 0.9, 7, 2, true)]
    public void SelectRepairsSkipsIneligibleDecisionsWithoutWriting(
        CadOutputFwDiffAssignmentMode mode, double? confidence, int? suggestion, int primary, bool hasOverride)
    {
        var decision = Decision(101, 0) with
        {
            Mode = mode,
            Confidence = confidence,
            RepairSuggestionDiffIndex = suggestion,
            CurrentPrimaryDiffIndex = primary,
        };
        var overrides = hasOverride
            ? new Dictionary<int, int> { [101] = primary }
            : new Dictionary<int, int>();
        var diffs = new Dictionary<int, int> { [101] = primary };

        var applied = DxfCsvRepairRule.SelectRepairs(
            new Dictionary<int, CadOutputFwDiffAssignmentDecision> { [101] = decision },
            overrides, diffs, new Dictionary<int, int> { [101] = 0 });

        Assert.False(DxfCsvRepairRule.ShouldAutoApply(decision, overrides));
        Assert.Empty(applied);
        Assert.Equal(new Dictionary<int, int> { [101] = primary }, diffs);
    }

    [Fact]
    public void SelectRepairsAppliesAtMinimumConfidenceWithoutCurrentPrimary()
    {
        var decision = Decision(101, 0) with { CurrentPrimaryDiffIndex = null };
        var overrides = new Dictionary<int, int>();
        var diffs = new Dictionary<int, int>();

        var applied = DxfCsvRepairRule.SelectRepairs(
            new Dictionary<int, CadOutputFwDiffAssignmentDecision> { [101] = decision },
            overrides, diffs, new Dictionary<int, int> { [101] = 0 });

        Assert.True(DxfCsvRepairRule.ShouldAutoApply(decision, overrides));
        Assert.Equal(101, Assert.Single(applied));
        Assert.Equal(new KeyValuePair<int, int>(101, 7), Assert.Single(diffs));
    }

    [Fact]
    public void SelectRepairsWritesInOrderedCadOrderAndRejectsOnlySameIcDuplicate()
    {
        var decisions = new Dictionary<int, CadOutputFwDiffAssignmentDecision>
        {
            [103] = Decision(103, 2) with { CurrentPrimaryDiffIndex = 4 },
            [102] = Decision(102, 1) with { CurrentPrimaryDiffIndex = 3 },
            [101] = Decision(101, 0),
        };
        var diffs = new Dictionary<int, int> { [101] = 2, [102] = 3, [103] = 4 };
        var ics = new Dictionary<int, int> { [101] = 0, [102] = 0, [103] = 1 };

        var applied = DxfCsvRepairRule.SelectRepairs(decisions, new Dictionary<int, int>(), diffs, ics);

        Assert.Equal(_expectedAppliedCadIds, applied);
        Assert.Equal(new Dictionary<int, int> { [101] = 7, [102] = 3, [103] = 7 }, diffs);
    }

    [Theory]
    [InlineData(101, false)]
    [InlineData(102, true)]
    [InlineData(105, false)]
    public void WouldCreateSameIcDuplicateRequiresAnotherMatchingDiffAndKnownIc(int cadPadId, bool expected)
    {
        var diffs = new Dictionary<int, int> { [101] = 7, [102] = 8, [103] = 7, [104] = 7 };
        var ics = new Dictionary<int, int> { [101] = 0, [102] = 0, [103] = 1 };

        var actual = DxfCsvRepairRule.WouldCreateSameIcDuplicate(cadPadId, 7, diffs, ics);

        Assert.Equal(expected, actual);
    }

    private static CadOutputFwDiffAssignmentDecision Decision(int cadPadId, int orderedCadIndex) =>
        CadOutputFwDiffAssignmentDecision.Empty(cadPadId, orderedCadIndex, CadOutputFwDiffAssignmentMode.CsvConstrained) with
        {
            CurrentPrimaryDiffIndex = 2,
            RepairSuggestionDiffIndex = 7,
            Confidence = 0.80,
        };
}
