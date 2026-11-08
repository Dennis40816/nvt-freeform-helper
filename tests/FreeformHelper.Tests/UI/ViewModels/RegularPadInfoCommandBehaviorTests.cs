// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularPadInfoCommandBehaviorTests
{
    [Fact]
    public void LocateMatch_WithoutFocusCallback_HighlightsTheMatchedRegularPad()
    {
        using var culture = TestCulture.UseEnUs();
        var highlighted = new List<int>();
        var vm = CreateViewModel([274], highlight: highlighted.Add);

        vm.LocateMatchCommand.Execute(null);

        Assert.Equal(42, Assert.Single(highlighted));
        Assert.False(vm.HasMatchNavigation);
        Assert.True(vm.HasHighlightMatchAction);
        Assert.True(vm.HasLocateMatchAction);
        Assert.Equal("Focus the best match when available; otherwise highlight related CAD pads.", vm.LocateMatchToolTip);
        Assert.Equal(vm.IdentityText, vm.PanelIdentityText);
        Assert.Equal($"X/Y are 1-based; row/col are 0-based{Environment.NewLine}Row 1, Col 2", vm.IdentityToolTip);
    }

    [Fact]
    public void LocateMatch_WithBothCallbacks_FocusesWithoutAlsoHighlighting()
    {
        using var culture = TestCulture.UseEnUs();
        var focused = new List<int>();
        var highlighted = new List<int>();
        var vm = CreateViewModel([274], focus: focused.Add, highlight: highlighted.Add);

        vm.LocateMatchCommand.Execute(null);

        Assert.Equal(42, Assert.Single(focused));
        Assert.Empty(highlighted);
        Assert.True(vm.HasMatchNavigation);
        Assert.True(vm.HasHighlightMatchAction);
    }

    [Fact]
    public void LocateMatch_WithoutRelatedCadPads_DoesNotHighlightAndExplainsWhy()
    {
        using var culture = TestCulture.UseEnUs();
        var highlighted = new List<int>();
        var vm = CreateViewModel([], highlight: highlighted.Add);

        vm.LocateMatchCommand.Execute(null);

        Assert.Empty(highlighted);
        Assert.False(vm.HasLocateMatchAction);
        Assert.Equal("Unmatched", vm.MatchText);
        Assert.Equal("No related CAD pads are available", vm.LocateMatchToolTip);
    }

    [Fact]
    public void LocateMatch_WithoutNavigationCallbacks_KeepsTheMatchAndExplainsUnavailableAction()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateViewModel([274]);

        vm.LocateMatchCommand.Execute(null);

        Assert.False(vm.HasLocateMatchAction);
        Assert.False(vm.HasMatchNavigation);
        Assert.False(vm.HasHighlightMatchAction);
        Assert.Equal("CAD id 274", vm.MatchText);
        Assert.Equal("Match navigation is unavailable for this selection", vm.LocateMatchToolTip);
    }

    [Fact]
    public void MultiSelection_WithGaps_CompressesSortedRowAndColumnRangesAndShowsCombinedBounds()
    {
        using var culture = TestCulture.UseEnUs();
        var locations = new[] { (Row: 6, Col: 7), (Row: 0, Col: 0), (Row: 5, Col: 5), (Row: 1, Col: 2), (Row: 3, Col: 3) };
        var pads = locations.Select((location, index) => TestGeometryFactory.CreateRegularPad(
            location.Row, location.Col, 100 + index,
            location.Col * 10, location.Row * 8, (location.Col + 1) * 10, (location.Row + 1) * 8,
            diffIndex: 42)).ToArray();
        var highlighted = new List<int>();
        var vm = CreateViewModel([274], highlight: highlighted.Add, pads: pads);

        vm.LocateMatchCommand.Execute(null);

        Assert.Equal("0-1, 3, 5-6", vm.RowRangeText);
        Assert.Equal("0, 2-3, 5, 7", vm.ColRangeText);
        Assert.Equal("X 0 - 80\nY 0 - 56", vm.BoundsText);
        Assert.Equal("Regular pads", vm.PanelIdentityText);
        Assert.Equal("IC 1 · Diff idx 42", vm.IdentityToolTip);
        Assert.Equal("Select a single Regular pad first", vm.LocateMatchToolTip);
        Assert.False(vm.HasLocateMatchAction);
        Assert.Empty(highlighted);
    }

    [Fact]
    public void DiscardChanges_HeightOnlyAfterWidthWasApplied_KeepsAppliedWidthAndReturnsToCleanState()
    {
        using var culture = TestCulture.UseEnUs();
        var applied = new List<(decimal? Width, decimal? Height)>();
        var closeCount = 0;
        var vm = CreateViewModel([], applySize: (_, _, width, height) => applied.Add((width, height)),
            close: () => closeCount++);
        vm.PadWidth = 12m;
        vm.ApplyChangesCommand.Execute(null);
        Assert.Equal(((decimal?)12m, (decimal?)null), Assert.Single(applied));
        vm.PadHeight = 9m;
        Assert.True(vm.HasPendingChanges);

        vm.DiscardChangesCommand.Execute(null);

        Assert.Equal(12m, vm.PadWidth);
        Assert.Equal(8m, vm.PadHeight);
        Assert.False(vm.HasPendingChanges);
        Assert.Single(applied);
        Assert.Equal(1, closeCount);
        var changes = new List<string?>();
        vm.PropertyChanged += (_, change) => changes.Add(change.PropertyName);

        vm.DiscardChangesCommand.Execute(null);

        Assert.Empty(changes);
        Assert.Single(applied);
        Assert.Equal(1, closeCount);
    }

    private static RegularPadInfoViewModel CreateViewModel(
        IReadOnlyList<int> matchedCadIds,
        Action<int>? focus = null,
        Action<int>? highlight = null,
        IReadOnlyList<RegularPad>? pads = null,
        Action<IReadOnlyList<int>, IReadOnlyList<int>, decimal?, decimal?>? applySize = null,
        Action? close = null)
    {
        return new RegularPadInfoViewModel(
            pads: pads ?? [TestGeometryFactory.CreateRegularPad(1, 2, 42, 0, 0, 10, 8, diffIndex: 42)],
            toDisplayRow: static row => row,
            getDxfIndex: null,
            getMatchedCadPadIds: _ => matchedCadIds,
            getMatchedCadLinks: null,
            applyPadSize: applySize,
            resetPadSize: null,
            setFreeform: null,
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: null,
            focusRegularMatches: focus,
            highlightRegularMatches: highlight,
            closePadInfo: close);
    }
}
