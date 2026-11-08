// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadOutputFwDiffIndexShiftBehaviorTests
{
    private static readonly int[] _expectedMissingCadPadIds = [8, 9];
    private static readonly int[] _expectedCurrentDiffs = [10, 50, 50, 20, 11];

    [Theory]
    [InlineData(false, 1, "CAD Output FW Diff offset: select CAD pads first.")]
    [InlineData(true, 0, "CAD Output FW Diff offset: input a non-zero offset first.")]
    public void BuildShiftPlan_MissingSelectionOrOffset_ReturnsActionableReason(bool hasSelection, int delta, string expectedMessage)
    {
        using var culture = TestCulture.UseEnUs();
        int[] selected = hasSelection ? [1] : [];

        var result = CadOutputFwDiffIndexShiftService.BuildShiftPlan(
            selected, new Dictionary<int, int> { [1] = 10 }, new Dictionary<int, int> { [1] = 0 }, delta);

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Empty(result.ShiftedDiffByCadId);
        Assert.Empty(result.MissingCadPadIds);
    }

    [Theory]
    [InlineData(1, "CAD Output FW Diff offset blocked: CAD Output FW Diff missing for CAD 1.")]
    [InlineData(5, "CAD Output FW Diff offset blocked: CAD Output FW Diff missing for CAD 1, 2, 3, 4 (+).")]
    public void BuildShiftPlan_AllSelectedMappingsMissing_BlocksWithSortedBoundedCadList(int count, string expectedMessage)
    {
        using var culture = TestCulture.UseEnUs();
        var selected = Enumerable.Range(1, count).Reverse().ToArray();

        var result = CadOutputFwDiffIndexShiftService.BuildShiftPlan(
            selected, new Dictionary<int, int> { [100] = 10 }, new Dictionary<int, int> { [100] = 0 }, 1);

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Empty(result.ShiftedDiffByCadId);
        Assert.Empty(result.MissingCadPadIds);
    }

    [Fact]
    public void BuildShiftPlan_PartiallyMappedSelection_ReturnsUniqueTargetsAndMissingIdsWithoutMutatingInput()
    {
        using var culture = TestCulture.UseEnUs();
        int[] selected = [9, 4, 1, 1, 8];
        var current = new Dictionary<int, int> { [1] = 10, [2] = 50, [3] = 50, [4] = 20, [5] = 11 };
        var icIndices = new Dictionary<int, int> { [2] = 1, [3] = 1, [4] = 0, [5] = 1 };

        var result = CadOutputFwDiffIndexShiftService.BuildShiftPlan(selected, current, icIndices, 1);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.ShiftedDiffByCadId.Count);
        Assert.Equal(11, result.ShiftedDiffByCadId[1]);
        Assert.Equal(21, result.ShiftedDiffByCadId[4]);
        Assert.Equal(_expectedMissingCadPadIds, result.MissingCadPadIds);
        Assert.Equal("Shifted 2 CAD pad(s) by 1. Range 10-20 -> 11-21.", result.Message);
        Assert.Equal(_expectedCurrentDiffs, Enumerable.Range(1, 5).Select(id => current[id]));
        Assert.False(icIndices.ContainsKey(1));
    }
}
