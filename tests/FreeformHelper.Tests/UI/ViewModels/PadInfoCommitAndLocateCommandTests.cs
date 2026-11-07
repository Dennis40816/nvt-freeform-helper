// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInfoCommitAndLocateCommandTests
{
    private const int CadPadId = 274;
    private const int RegularPadId = 42;
    private const decimal InitialCustomValue = 3.5m;
    private const string OwnerDiagnostics =
        "IC3/diff112 reg4808: ov=10, src=10, blk=0, reach=10, blocker=0, owners=32|274|31, " +
        "ownerShare=50/50, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)";

    private static readonly int[] _matchedCadPadIds = [CadPadId];
    private static readonly int[] _relationCadPadIds = [31, 32];
    private static readonly PadMatchLink[] _matchLinks =
    [
        new(31, RegularPadId, 4.0, 0.4, 0.5),
        new(32, RegularPadId, 6.0, 0.6, 0.75),
    ];

    [Fact]
    public void CadPadInfo_ApplyChangesCommand_CommitsCustomValueAndCloses()
    {
        var commits = new List<(IReadOnlyList<int> PadIds, decimal Value)>();
        var closeCount = 0;
        var vm = CreateCadPadViewModel(
            applyCustomValue: (ids, value) => commits.Add((ids, value)),
            closePadInfo: () => closeCount++);
        Assert.Equal(InitialCustomValue, vm.CustomValue);
        Assert.False(vm.HasPendingChanges);
        vm.CustomValue = 7.25m;
        Assert.True(vm.HasPendingChanges);
        Assert.Empty(commits);

        vm.ApplyChangesCommand.Execute(null);

        var commit = Assert.Single(commits);
        Assert.Equal(CadPadId, Assert.Single(commit.PadIds));
        Assert.Equal(7.25m, commit.Value);
        Assert.Equal(7.25m, vm.CustomValue);
        Assert.False(vm.HasPendingChanges);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void CadPadInfo_DiscardChangesCommand_RestoresCustomValueWithoutCommitting()
    {
        var commits = new List<(IReadOnlyList<int> PadIds, decimal Value)>();
        var vm = CreateCadPadViewModel(
            applyCustomValue: (ids, value) => commits.Add((ids, value)));
        Assert.Equal(InitialCustomValue, vm.CustomValue);
        vm.CustomValue = 7.25m;
        Assert.True(vm.HasPendingChanges);

        vm.DiscardChangesCommand.Execute(null);

        Assert.Empty(commits);
        Assert.Equal(InitialCustomValue, vm.CustomValue);
        Assert.False(vm.HasPendingChanges);

        vm.ApplyChangesCommand.Execute(null);

        Assert.Empty(commits);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void RegularPadInfo_LocateMatchCommand_WithMatch_PassesRegularPadIdToFocus()
    {
        var focusedRegularPadIds = new List<int>();
        var vm = CreateRegularPadViewModel(
            matchedCadPadIds: _matchedCadPadIds,
            focusRegularMatches: focusedRegularPadIds.Add);
        Assert.True(vm.HasLocateMatchAction);
        Assert.True(vm.HasMatchNavigation);
        Assert.Equal("CAD id 274", vm.MatchText);
        Assert.Empty(focusedRegularPadIds);

        vm.LocateMatchCommand.Execute(null);

        Assert.Equal(RegularPadId, Assert.Single(focusedRegularPadIds));
    }

    [Fact]
    public void RegularPadInfo_LocateMatchCommand_WithoutMatch_DoesNotFocus()
    {
        var focusedRegularPadIds = new List<int>();
        var vm = CreateRegularPadViewModel(
            matchedCadPadIds: Array.Empty<int>(),
            focusRegularMatches: focusedRegularPadIds.Add);
        Assert.False(vm.HasLocateMatchAction);
        Assert.False(vm.HasMatchNavigation);
        Assert.Equal("Unmatched", vm.MatchText);

        vm.LocateMatchCommand.Execute(null);

        Assert.Empty(focusedRegularPadIds);
        Assert.Equal("Unmatched", vm.MatchText);
    }

    [Fact]
    public void RelationCard_HighlightCommand_HighlightsEachCardsCadPad()
    {
        var highlightedGroups = new List<IReadOnlyCollection<int>>();
        var vm = CreateRegularPadViewModel(
            matchedCadPadIds: _relationCadPadIds,
            highlightCadOwnerPads: highlightedGroups.Add,
            matchedCadLinks: _matchLinks);
        var firstCard = Assert.Single(vm.MatchAreaCards, static card => card.TitleText == "CAD 31");
        var secondCard = Assert.Single(vm.MatchAreaCards, static card => card.TitleText == "CAD 32");
        Assert.NotNull(firstCard.HighlightCommand);
        Assert.NotNull(secondCard.HighlightCommand);
        Assert.Empty(highlightedGroups);

        firstCard.HighlightCommand.Execute(null);

        Assert.Equal(31, Assert.Single(Assert.Single(highlightedGroups)));

        secondCard.HighlightCommand.Execute(null);

        Assert.Equal(2, highlightedGroups.Count);
        Assert.Equal(32, Assert.Single(highlightedGroups[1]));
    }

    [Fact]
    public void NotchOwnerLink_HighlightCommand_HighlightsEachLinksOwnerCadPad()
    {
        var highlightedGroups = new List<IReadOnlyCollection<int>>();
        var vm = CreateCadPadViewModel(highlightCadOwnerPads: highlightedGroups.Add);
        var firstLink = Assert.Single(vm.NotchOwnerLinks, static link => link.Text == "CAD 31 (1 reg)");
        var secondLink = Assert.Single(vm.NotchOwnerLinks, static link => link.Text == "CAD 32 (1 reg)");
        Assert.Empty(highlightedGroups);

        firstLink.HighlightCommand.Execute(null);

        Assert.Equal(31, Assert.Single(Assert.Single(highlightedGroups)));

        secondLink.HighlightCommand.Execute(null);

        Assert.Equal(2, highlightedGroups.Count);
        Assert.Equal(32, Assert.Single(highlightedGroups[1]));
    }

    private static CadPadInfoViewModel CreateCadPadViewModel(
        Action<IReadOnlyList<int>, decimal>? applyCustomValue = null,
        Action? closePadInfo = null,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads = null)
    {
        var pad = TestGeometryFactory.CreateCadPad(CadPadId, "AA", 0, 0, 10, 8);
        return new CadPadInfoViewModel(
            pads: [pad],
            getDxfIndex: static _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static _ => (double)InitialCustomValue,
            applyCustomValue: applyCustomValue,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null,
            closePadInfo: closePadInfo,
            highlightCadOwnerPads: highlightCadOwnerPads,
            getCadV22CompensationPreview: static _ => (1.0, 1.0, 1.0, true),
            getCadV22CompensationDiagnostics: static _ => OwnerDiagnostics);
    }

    private static RegularPadInfoViewModel CreateRegularPadViewModel(
        IReadOnlyList<int> matchedCadPadIds,
        Action<int>? focusRegularMatches = null,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads = null,
        IReadOnlyList<PadMatchLink>? matchedCadLinks = null)
    {
        var pad = TestGeometryFactory.CreateRegularPad(1, 2, RegularPadId, 0, 0, 10, 8, diffIndex: 42);
        return new RegularPadInfoViewModel(
            pads: [pad],
            toDisplayRow: static row => row,
            getDxfIndex: null,
            getMatchedCadPadIds: _ => matchedCadPadIds,
            getMatchedCadLinks: _ => matchedCadLinks ?? Array.Empty<PadMatchLink>(),
            applyPadSize: null,
            resetPadSize: null,
            setFreeform: null,
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: highlightCadOwnerPads,
            focusRegularMatches: focusRegularMatches,
            highlightRegularMatches: null);
    }
}
