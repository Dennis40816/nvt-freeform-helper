// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadInfoEditAndLocateBehaviorTests
{
    [Fact]
    public void ApplyChanges_MultiSelection_AdvancesDiscardBaselineWithoutReapplying()
    {
        using var culture = TestCulture.UseEnUs();
        var commits = new List<(IReadOnlyList<int> PadIds, decimal Value)>();
        var closeCount = 0;
        var vm = new CadPadInfoViewModel(
            pads:
            [
                TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8),
                TestGeometryFactory.CreateCadPad(275, "AA", 20, 0, 30, 8),
            ],
            getDxfIndex: static _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static id => id == 274 ? 3.5 : 4.5,
            applyCustomValue: (ids, value) => commits.Add((ids.ToArray(), value)),
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null,
            closePadInfo: () => closeCount++);
        Assert.Equal(0m, vm.CustomValue);
        Assert.False(vm.HasPendingChanges);
        vm.CustomValue = 7.25m;

        vm.ApplyChangesCommand.Execute(null);
        vm.DiscardChangesCommand.Execute(null);

        var commit = Assert.Single(commits);
        Assert.Equal([274, 275], commit.PadIds);
        Assert.Equal(7.25m, commit.Value);
        Assert.Equal(7.25m, vm.CustomValue);
        Assert.False(vm.HasPendingChanges);
        Assert.Equal(1, closeCount);

        vm.CustomValue = 8m;
        vm.DiscardChangesCommand.Execute(null);
        vm.ApplyChangesCommand.Execute(null);

        Assert.Equal(7.25m, vm.CustomValue);
        Assert.False(vm.HasPendingChanges);
        Assert.Single(commits);
        Assert.Equal(2, closeCount);
    }

    [Fact]
    public void LocateMatch_WithHighlightOnly_HighlightsSelectedCadPad()
    {
        using var culture = TestCulture.UseEnUs();
        var highlightedCadIds = new List<int>();
        var vm = CreateMatchedViewModel(focusCadMatches: null, highlightCadMatches: highlightedCadIds.Add);
        Assert.False(vm.HasFocusMatchAction);
        Assert.True(vm.HasHighlightMatchAction);
        Assert.True(vm.HasLocateMatchAction);

        vm.LocateMatchCommand.Execute(null);

        Assert.Equal(274, Assert.Single(highlightedCadIds));
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void LocateMatch_WithBothActions_FocusesWithoutHighlighting()
    {
        using var culture = TestCulture.UseEnUs();
        var focusedCadIds = new List<int>();
        var highlightedCadIds = new List<int>();
        var vm = CreateMatchedViewModel(focusedCadIds.Add, highlightedCadIds.Add);
        Assert.True(vm.HasFocusMatchAction);
        Assert.True(vm.HasHighlightMatchAction);

        vm.LocateMatchCommand.Execute(null);

        Assert.Equal(274, Assert.Single(focusedCadIds));
        Assert.Empty(highlightedCadIds);
    }

    private static CadPadInfoViewModel CreateMatchedViewModel(
        Action<int>? focusCadMatches,
        Action<int> highlightCadMatches)
    {
        return new CadPadInfoViewModel(
            pads: [TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8)],
            getDxfIndex: static _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null,
            getMatchedRegularPadIds: static _ => [4808],
            focusCadMatches: focusCadMatches,
            highlightCadMatches: highlightCadMatches);
    }
}
