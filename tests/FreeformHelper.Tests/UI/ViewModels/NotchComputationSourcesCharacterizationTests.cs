// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchComputationSourcesCharacterizationTests
{
    private static readonly int[] _matchedIds = [1, 3, 4, 5, 7];
    private static readonly int[] _maskIds = [0, 1];
    private static readonly int[] _centerOwners = [101, 102];

    private static readonly int[] _matchedIdsWithoutSouthNeighbor = [1, 3, 4, 5];

    [AvaloniaFact]
    public async Task Compensation_UsesCadOwnersAndMatchedNeighborsWhileMaskExcludesTheSelectedPadAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateSourceViewModelAsync(includeSouthNeighborPad: true);

        Assert.Equal(6, vm.CadPads.Count);
        Assert.Equal(_matchedIds, vm.WorkflowDataForTests.LatestPadMatch.RegularToCad.Keys.Order());
        var snapshot = FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm);
        Assert.NotNull(snapshot.ActiveRegularVisibilityMaskPadIds);
        Assert.Equal(_maskIds, snapshot.ActiveRegularVisibilityMaskPadIds.Order());

        var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(101));

        var center = Assert.Single(resolved.Compensation.RegularDebugInfos);
        Assert.Equal(4, center.RegularPadId);
        Assert.DoesNotContain(center.RegularPadId, snapshot.ActiveRegularVisibilityMaskPadIds);
        Assert.Equal(_centerOwners, center.OwnerCadPadIds.Order());
        // All four matched neighbors keep the center interior, despite the sparse visibility mask.
        Assert.False(center.IsBoundaryRegular);
        Assert.Equal(36.0, center.OverlapArea, 6);
    }

    [AvaloniaFact]
    public async Task Compensation_TreatsTheCenterAsBoundaryWhenOneAxialNeighborIsUnmatchedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateSourceViewModelAsync(includeSouthNeighborPad: false);

        Assert.Equal(5, vm.CadPads.Count);
        Assert.Equal(_matchedIdsWithoutSouthNeighbor, vm.WorkflowDataForTests.LatestPadMatch.RegularToCad.Keys.Order());

        var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(101));

        var center = Assert.Single(resolved.Compensation.RegularDebugInfos);
        Assert.Equal(4, center.RegularPadId);
        // The south neighbor (regular 7) has no CAD pad, so the matched-neighbor protection no longer applies.
        Assert.True(center.IsBoundaryRegular);
    }

    private static async Task<FreeformHelperViewModel> CreateSourceViewModelAsync(bool includeSouthNeighborPad)
    {
        var vm = new FreeformHelperViewModel
        {
            NotchPreviewAutoPlayEnabled = false,
            EnableToFull = true,
        };
        await vm.WaitForGridRebuildIdleAsync();
        // Enable before installing the synthetic source snapshot so no indexing refresh rewrites it.
        vm.RegularVisibilityMaskUi.IsEnabled = true;
        var grid = TestGeometryFactory.CreateRegularGrid(3, 3, cellWidth: 10, cellHeight: 10);
        var pads = new List<CadPad>
        {
            TestGeometryFactory.CreateCadPad(101, "signal", 12, 12, 18, 18),
            TestGeometryFactory.CreateCadPad(102, "signal", 18, 12, 19, 18),
            TestGeometryFactory.CreateCadPad(103, "signal", 12, 2, 18, 8),
            TestGeometryFactory.CreateCadPad(104, "signal", 2, 12, 8, 18),
            TestGeometryFactory.CreateCadPad(105, "signal", 22, 12, 28, 18),
        };
        if (includeSouthNeighborPad)
        {
            pads.Add(TestGeometryFactory.CreateCadPad(106, "signal", 12, 22, 18, 28));
        }

        var cadPads = new CadPadSet(pads);
        FreeformHelperViewModelPrivateAccess.Cad(vm) = cadPads;
        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;
        vm.CadPads = new ObservableCollection<CadPad>(cadPads.Pads);
        vm.RegularPads = new ObservableCollection<RegularPad>(grid.Pads);
        vm.WorkflowDataForTests.LatestPadMatch = PadMatcher.Match(cadPads, grid);
        vm.RegularVisibilityMaskForTests.Mask = new RegularVisibilityMaskResult(
            RegularVisibilityMaskStatus.Loaded, "synthetic.csv", 1,
            _maskIds.ToHashSet(), grid.Pads.Select(static pad => pad.RegularPadId).Except(_maskIds).ToHashSet(), null);
        vm.InvalidateWorkflowDataSnapshot();
        return vm;
    }
}
