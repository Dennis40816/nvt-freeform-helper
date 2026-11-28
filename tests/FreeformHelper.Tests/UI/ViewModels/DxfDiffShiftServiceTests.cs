// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfDiffShiftServiceTests
{
    [Fact]
    public void BuildSelectionSummaryDescribesEmptyAndVisibleSelections()
    {
        using var culture = TestCulture.UseEnUs();
        var diffs = new Dictionary<int, int> { [101] = 11, [102] = 10 };

        var empty = DxfDiffShiftService.BuildSelectionSummary([], diffs, -2m);
        var missing = DxfDiffShiftService.BuildSelectionSummary([999], diffs, -2m);
        var selected = DxfDiffShiftService.BuildSelectionSummary([102, 999, 101], diffs, -2m);

        Assert.Equal("Diff shift: select CAD output pads in AA first.", empty);
        Assert.Equal("Diff shift: select CAD output pads in AA first.", missing);
        Assert.Equal("Diff shift target: 2 CAD pad(s), current range 10-11, offset -2.", selected);
        Assert.False(DxfDiffShiftService.CanOffset([], diffs, -2m));
    }

    [Theory]
    [InlineData(101, 0, false)]
    [InlineData(101, 0.5, false)]
    [InlineData(999, 2, false)]
    [InlineData(101, -2, true)]
    public void CanOffsetRequiresAnIndexedSelectionAndNonzeroIntegerDelta(int selectedId, double delta, bool expected)
    {
        var diffs = new Dictionary<int, int> { [101] = 10 };

        var actual = DxfDiffShiftService.CanOffset([selectedId], diffs, (decimal)delta);

        Assert.Equal(expected, actual);
    }

    [AvaloniaFact]
    public async Task ApplyShiftReturnsPreviousOverridesAndReapplyingThePlanChangesNothingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        await vm.MatchCommand.ExecuteAsync(null);
        var ids = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        var diffs = new Dictionary<int, int> { [ids[0]] = 11, [ids[1]] = 10 };
        var ics = ids.ToDictionary(static id => id, static _ => 0);
        var overrides = new Dictionary<int, int> { [ids[0]] = 11, [999] = 50 };
        var expectedPrevious = new Dictionary<int, int>(overrides);

        var result = DxfDiffShiftService.ApplyShift(ids, diffs, ics, 2, overrides);

        Assert.True(result.Plan.IsSuccessful);
        Assert.Equal(2, result.ChangedCount);
        Assert.Equal(expectedPrevious, result.PreviousOverrides);
        Assert.NotSame(overrides, result.PreviousOverrides);
        Assert.Equal(new Dictionary<int, int> { [ids[0]] = 13, [999] = 50, [ids[1]] = 12 }, overrides);
        Assert.Equal(new Dictionary<int, int> { [ids[0]] = 13, [ids[1]] = 12 }, result.Plan.ShiftedDiffByCadId);
        Assert.Empty(result.Plan.MissingCadPadIds);
        Assert.Equal("Shifted 2 CAD pad(s) by 2. Range 10-11 -> 12-13.", result.Plan.Message);

        var repeated = DxfDiffShiftService.ApplyShift(ids, diffs, ics, 2, overrides);

        Assert.True(repeated.Plan.IsSuccessful);
        Assert.Equal(0, repeated.ChangedCount);
        Assert.Equal(new Dictionary<int, int> { [ids[0]] = 13, [999] = 50, [ids[1]] = 12 }, overrides);
        Assert.Equal(overrides, repeated.PreviousOverrides);
        Assert.Equal(expectedPrevious, result.PreviousOverrides);
        Assert.Equal(result.Plan.Message, repeated.Plan.Message);
    }

    [Fact]
    public void ApplyShiftLeavesOverridesUnchangedWhenThePlanConflicts()
    {
        var diffs = new Dictionary<int, int> { [101] = 10, [102] = 11 };
        var ics = new Dictionary<int, int> { [101] = 0, [102] = 0 };
        var overrides = new Dictionary<int, int> { [101] = 10, [102] = 11 };

        var result = DxfDiffShiftService.ApplyShift([101], diffs, ics, 1, overrides);

        Assert.False(result.Plan.IsSuccessful);
        Assert.Equal(0, result.ChangedCount);
        Assert.Equal(new Dictionary<int, int> { [101] = 10, [102] = 11 }, overrides);
        Assert.Equal(
            "CAD Output FW Diff offset blocked: duplicate diff after shift at IC 1, diff 11 -> CAD 101, 102.",
            result.Plan.Message);
    }
}
