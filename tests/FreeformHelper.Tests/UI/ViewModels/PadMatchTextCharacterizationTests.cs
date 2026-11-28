// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadMatchTextCharacterizationTests
{
    public static TheoryData<int[], int?[], string> RegularCases => new()
    {
        { [], [], "Unmatched" },
        { [7], [12], "CAD idx 12 (CAD id 7)" },
        { [7], [null], "CAD id 7" },
        { [1, 2, 3], [30, 10, 20], "CAD idx 10, 20, 30 (CAD id 1, 2, 3)" },
        { [1, 2, 3, 4], [40, 30, 20, 10], "CAD idx 10, 20, 30 (+1) (CAD id 1, 2, 3 (+1))" },
        { [1, 2, 3, 4, 5], [50, 40, 30, 20, 10], "CAD idx 10, 20, 30 (+2) (CAD id 1, 2, 3 (+2))" },
        { [1, 2, 3], [20, 20, 10], "CAD idx 10, 20 (CAD id 1, 2, 3)" },
        { [9, 3, 7], [30, 10, 20], "CAD idx 10, 20, 30 (CAD id 3, 7, 9)" },
    };

    public static TheoryData<int[], int[], bool, string> CadCases => new()
    {
        { [], [], true, "Unmatched" },
        { [7], [7], true, "IC2/diff107" },
        { [7], [7], false, "regId 7" },
        { [30, 10, 20], [20, 30, 10], true, "IC2/diff120 (+2)" },
        { [1, 2, 3, 4], [4, 3, 2, 1], true, "IC2/diff104 (+3)" },
        { [1, 2, 3, 4, 5], [5, 4, 3, 2, 1], true, "IC2/diff105 (+4)" },
        { [7, 7], [7, 7], true, "IC2/diff107" },
        { [9, 3, 7], [], true, "IC2/diff103 (+2)" },
    };

    [Theory]
    [MemberData(nameof(RegularCases))]
    public void RegularPadInfoPreservesMatchText(int[] cadIds, int?[] dxfIndices, string expected)
    {
        using var culture = TestCulture.UseEnUs();
        var indices = CreateDxfIndices(cadIds, dxfIndices);
        var pad = TestGeometryFactory.CreateRegularPad(0, 0, 42, 0, 0, 10, 10, diffIndex: 12);

        var vm = new RegularPadInfoViewModel(
            [pad], toDisplayRow: static row => row,
            getDxfIndex: id => indices.GetValueOrDefault(id),
            getMatchedCadPadIds: _ => cadIds,
            getMatchedCadLinks: null, applyPadSize: null, resetPadSize: null, setFreeform: null,
            getCrossIcOwnerShares: null, getNotchRowsByRegularPad: null, highlightCadOwnerPads: null,
            focusRegularMatches: null, highlightRegularMatches: null);

        Assert.Equal(expected, vm.MatchText);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [MemberData(nameof(RegularCases))]
    public async Task RegularInspectorPreservesMatchTextAsync(int[] cadIds, int?[] dxfIndices, string expected)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        await vm.WaitForGridRebuildIdleAsync();
        var pad = TestGeometryFactory.CreateRegularPad(0, 0, 42, 0, 0, 10, 10, diffIndex: 12);
        vm.CadPads = new ObservableCollection<CadPad>(cadIds.Select(id =>
            TestGeometryFactory.CreateCadPad(id, "signal", 0, 0, 10, 10)));
        vm.RegularPads = new ObservableCollection<RegularPad>([pad]);
        var links = cadIds.Select(id => new PadMatchLink(id, pad.RegularPadId, 10, 0.5, 0.5)).ToArray();
        SetMatches(vm, links);
        SetDxfIndices(vm, CreateDxfIndices(cadIds, dxfIndices));

        var snapshot = vm.BuildRegularPadInspectorSnapshot(
            pad.RegularPadId, includeMatchDetails: true, includeRuleTrace: false);

        Assert.Equal(expected, Assert.IsType<RegularPadInspectorSnapshot>(snapshot?.Regular).MatchText);
    }

    [Theory]
    [MemberData(nameof(CadCases))]
    public void CadPadInfoPreservesMatchText(int[] regularIds, int[] linkedIds, bool hasIdentity, string expected)
    {
        using var culture = TestCulture.UseEnUs();
        var pad = TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 10, 10);
        var links = CreateCadLinks(linkedIds);

        var vm = new CadPadInfoViewModel(
            [pad], getDxfIndex: static _ => null,
            getCadOutputFwDiffOverride: null, isDxfIndexAnchor: null, getCustomValue: static _ => 0,
            applyCustomValue: null, setCadOutputFwDiffOverride: null, clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null, clearDxfIndexAnchor: null,
            getMatchedRegularPadIds: _ => regularIds, getMatchedRegularLinks: _ => links,
            getRegularPadIcDiff: id => hasIdentity ? (1, id + 100) : null);

        Assert.Equal(expected, vm.MatchText);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [MemberData(nameof(CadCases))]
    public async Task CadInspectorPreservesMatchTextAsync(
        int[] regularIds, int[] linkedIds, bool hasIdentity, string expected)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        await vm.WaitForGridRebuildIdleAsync();
        var cad = TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 10, 10);
        vm.CadPads = new ObservableCollection<CadPad>([cad]);
        vm.RegularPads = new ObservableCollection<RegularPad>(CreateMatchedRegularPads(regularIds, hasIdentity));
        SetMatches(vm, CreateCadLinks(linkedIds));

        var snapshot = vm.BuildCadPadInspectorSnapshot(cad.Id,
            includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: false,
            includeMatchDetails: true, includeRuleTrace: false);

        Assert.Equal(expected, Assert.IsType<CadPadInspectorSnapshot>(snapshot?.Cad).MatchText);
    }

    private static Dictionary<int, int?> CreateDxfIndices(int[] cadIds, int?[] dxfIndices)
    {
        return cadIds.Select((id, index) => (Id: id, Index: dxfIndices[index]))
            .ToDictionary(item => item.Id, item => item.Index);
    }

    private static PadMatchLink[] CreateCadLinks(int[] linkedIds)
    {
        return linkedIds.Select((id, index) => new PadMatchLink(11, id, 10, 0.5, 1.0 / (index + 1))).ToArray();
    }

    private static IEnumerable<RegularPad> CreateMatchedRegularPads(int[] regularIds, bool hasIdentity)
    {
        return regularIds.Distinct().Where(_ => hasIdentity).Select(id =>
        {
            var pad = TestGeometryFactory.CreateRegularPad(0, 0, id, 0, 0, 10, 10, diffIndex: id + 100, icIndex: 1);
            pad.MatchedCadPadId = 11;
            return pad;
        });
    }

    private static void SetMatches(FreeformHelperViewModel vm, IReadOnlyList<PadMatchLink> links)
    {
        vm.WorkflowDataForTests.LatestPadMatch = new PadMatchResult(
            links.GroupBy(link => link.CadPadId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<PadMatchLink>)group.ToArray()),
            links.GroupBy(link => link.RegularPadId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<PadMatchLink>)group.ToArray()));
        vm.InvalidateWorkflowDataSnapshot();
    }

    private static void SetDxfIndices(FreeformHelperViewModel vm, IReadOnlyDictionary<int, int?> indices)
    {
        foreach (var (cadId, index) in indices)
        {
            if (index.HasValue)
            {
                vm.CadIndexMaps.OutputFwDiffIndexByCadId[cadId] = index.Value;
            }
        }

        vm.InvalidateWorkflowDataSnapshot();
    }
}
