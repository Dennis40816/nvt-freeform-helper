// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfOverrideServiceTests
{
    [Fact]
    public void SetOverrideWritesNewIndexWithoutReplacingOtherIndices()
    {
        using var culture = TestCulture.UseEnUs();
        var overrides = new Dictionary<int, int> { [6] = 8 };
        var ics = new Dictionary<int, int> { [5] = 1, [6] = 1 };

        var result = DxfOverrideService.SetOverride(5, 7, overrides, ics);

        Assert.True(result.Changed);
        Assert.False(result.HadPrevious);
        Assert.Empty(result.ReplacedOverrides);
        Assert.Equal(7, result.ClampedIndex);
        Assert.Equal(1, result.TargetIc);
        Assert.Equal("CAD Output FW Diff override saved: IC 2, CAD 5 -> 7.", result.Message);
        Assert.Equal(new Dictionary<int, int> { [6] = 8, [5] = 7 }, overrides);
    }

    [Fact]
    public void SetOverrideWithSameIndexTwiceChangesNothingOnSecondCall()
    {
        var overrides = new Dictionary<int, int>();
        var ics = new Dictionary<int, int> { [5] = 1 };

        var first = DxfOverrideService.SetOverride(5, 7, overrides, ics);
        var repeated = DxfOverrideService.SetOverride(5, 7, overrides, ics);

        Assert.True(first.Changed);
        Assert.False(repeated.Changed);
        Assert.True(repeated.HadPrevious);
        Assert.Equal(7, repeated.PreviousIndex);
        Assert.Empty(repeated.ReplacedOverrides);
        Assert.Equal(string.Empty, repeated.Message);
        Assert.Equal(new Dictionary<int, int> { [5] = 7 }, overrides);
    }

    [Fact]
    public void SetOverrideReplacesOnlyConflictsInTheSameKnownIc()
    {
        using var culture = TestCulture.UseEnUs();
        var overrides = new Dictionary<int, int> { [5] = 3, [6] = 7, [8] = 7, [9] = 7, [10] = 8 };
        var ics = new Dictionary<int, int> { [5] = 1, [6] = 1, [8] = 2, [10] = 1 };

        var result = DxfOverrideService.SetOverride(5, 7, overrides, ics);

        Assert.True(result.Changed);
        Assert.True(result.HadPrevious);
        Assert.Equal(3, result.PreviousIndex);
        Assert.Equal(new KeyValuePair<int, int>(6, 7), Assert.Single(result.ReplacedOverrides));
        Assert.Equal(new Dictionary<int, int> { [5] = 7, [8] = 7, [9] = 7, [10] = 8 }, overrides);
        Assert.Equal(
            "CAD Output FW Diff override saved: IC 2, CAD 5 -> 7. Replaced 1 conflicting override(s) in same IC.",
            result.Message);
    }

    [Fact]
    public void SetOverrideWithoutKnownIcOmitsIcFromMessage()
    {
        using var culture = TestCulture.UseEnUs();
        var overrides = new Dictionary<int, int>();

        var result = DxfOverrideService.SetOverride(5, 7, overrides, new Dictionary<int, int>());

        Assert.True(result.Changed);
        Assert.False(result.HadPrevious);
        Assert.Null(result.TargetIc);
        Assert.Empty(result.ReplacedOverrides);
        Assert.Equal("CAD Output FW Diff override saved: CAD 5 -> 7.", result.Message);
        Assert.Equal(new Dictionary<int, int> { [5] = 7 }, overrides);
    }

    [Fact]
    public void SetOverrideWithoutKnownIcReplacesEveryMatchingIndex()
    {
        using var culture = TestCulture.UseEnUs();
        var overrides = new Dictionary<int, int> { [6] = 7, [8] = 7, [9] = 7, [10] = 8 };
        var ics = new Dictionary<int, int> { [6] = 1, [8] = 2 };

        var result = DxfOverrideService.SetOverride(5, 7, overrides, ics);

        Assert.True(result.Changed);
        Assert.Null(result.TargetIc);
        Assert.Equal(new Dictionary<int, int> { [6] = 7, [8] = 7, [9] = 7 }, result.ReplacedOverrides);
        Assert.Equal(new Dictionary<int, int> { [10] = 8, [5] = 7 }, overrides);
        Assert.Equal(
            "CAD Output FW Diff override saved: CAD 5 -> 7. Replaced 3 conflicting override(s) in same IC.",
            result.Message);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1_000_001, 1_000_000)]
    public void SetOverrideClampsIndexToSupportedRange(int requestedIndex, int expectedIndex)
    {
        var overrides = new Dictionary<int, int>();
        var ics = new Dictionary<int, int> { [5] = 1 };

        var result = DxfOverrideService.SetOverride(5, requestedIndex, overrides, ics);

        Assert.True(result.Changed);
        Assert.Equal(expectedIndex, result.ClampedIndex);
        Assert.Equal(expectedIndex, overrides[5]);
    }

    [Fact]
    public void ClearOverrideRemovesExistingIndexAndReturnsPreviousValue()
    {
        var overrides = new Dictionary<int, int> { [5] = 7, [6] = 8 };

        var result = DxfOverrideService.ClearOverride(5, overrides);

        Assert.True(result.HadPrevious);
        Assert.Equal(7, result.PreviousIndex);
        Assert.Equal(new Dictionary<int, int> { [6] = 8 }, overrides);
    }

    [Fact]
    public void ClearOverrideWithMissingCadReturnsNoneAndLeavesOtherOverrides()
    {
        var overrides = new Dictionary<int, int> { [6] = 8 };

        var result = DxfOverrideService.ClearOverride(5, overrides);

        Assert.False(result.HadPrevious);
        Assert.Equal(new Dictionary<int, int> { [6] = 8 }, overrides);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, false)]
    public void IsAnchorRequiresTheCadToMatchItsOwnIcAnchor(int cadPadId, bool expected)
    {
        var ics = new Dictionary<int, int> { [5] = 1, [6] = 1, [7] = 2, [8] = 3 };
        var anchors = new Dictionary<int, int> { [1] = 5, [2] = 6, [4] = 9 };

        var actual = DxfOverrideService.IsAnchor(cadPadId, ics, anchors);

        Assert.Equal(expected, actual);
    }
}
