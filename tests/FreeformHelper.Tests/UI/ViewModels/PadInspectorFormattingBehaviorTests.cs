// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInspectorFormattingBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(8, "")]
    [InlineData(10, ", ... (+2 more)")]
    public async Task CadSnapshot_AssignmentsWithoutLinks_LimitsDetailsAndKeepsTheFullMatchCountAsync(
        int count, string overflowText)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        await vm.WaitForGridRebuildIdleAsync();
        var cad = TestGeometryFactory.CreateCadPad(7, "PAD", 0, 0, count * 10, 10);
        var grid = TestGeometryFactory.CreateRegularGrid(1, count, 10, 10,
            diffIndexSelector: (_, col) => 20 + col, icIndexSelector: (_, _) => 1);
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = cad.Id;
        }

        vm.CadPads = new ObservableCollection<CadPad>([cad]);
        vm.RegularPads = new ObservableCollection<RegularPad>(grid.Pads.Reverse());

        var snapshot = ReadCad(vm, cad.Id);

        Assert.Empty(vm.GetMatchedRegularLinks(cad.Id));
        Assert.Equal($"IC2/diff20 (+{count - 1})", snapshot.MatchText);
        var expectedDetails = Enumerable.Range(0, 8).Select(id => $"IC2/diff{20 + id} (regId {id})");
        Assert.Equal($"Regular: {string.Join(", ", expectedDetails)}{overflowText}", snapshot.MatchDetailsText);
        Assert.Equal(Enumerable.Range(0, count), snapshot.MatchedRegularPadIds);
    }

    [AvaloniaFact]
    public async Task RegularSnapshot_AssignmentWithoutLinksOrOutputIndex_ShowsCadIdentityAndAutomaticSourceAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        await vm.WaitForGridRebuildIdleAsync();
        var cad = TestGeometryFactory.CreateCadPad(7, "PAD", 0, 0, 10, 10);
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 4, 0, 0, 10, 10, diffIndex: 12);
        regular.MatchedCadPadId = cad.Id;
        vm.CadPads = new ObservableCollection<CadPad>([cad]);
        vm.RegularPads = new ObservableCollection<RegularPad>([regular]);

        var snapshot = ReadRegular(vm, regular.RegularPadId);

        Assert.Empty(vm.GetMatchedCadLinks(regular.RegularPadId));
        Assert.Equal("CAD id 7", snapshot.MatchText);
        Assert.Equal("CAD ids: 7", snapshot.MatchDetailsText);
        Assert.Equal("Auto", snapshot.DiffSource);
        Assert.Null(Assert.Single(snapshot.MatchedCadDetails).DxfIndex);
    }

    [AvaloniaTheory]
    [InlineData(8, "13.3%", "6.7%", "")]
    [InlineData(10, "10.5%", "10.5%", "... (+2 more)")]
    public async Task CadSnapshot_OverlapLinks_FormatsCoverageAndAddsOverflowOnlyBeyondEightAsync(
        int count, string coverageText, string eighthCoverageText, string overflowText)
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(
            count, ("signal", 0, 0, count * 10 - 5, 10));
        var vm = workspace.ViewModel;
        var cad = Assert.Single(vm.CadPads);

        var snapshot = ReadCad(vm, cad.Id);

        Assert.Equal($"IC1/diff0 (+{count - 1})", snapshot.MatchText);
        var lines = Enumerable.Range(0, 8)
            .Select(id => $"IC1/diff{id} (regId {id}): {(id == 7 ? eighthCoverageText : coverageText)}").ToList();
        if (overflowText.Length > 0)
        {
            lines.Add(overflowText);
        }

        Assert.Equal(string.Join(Environment.NewLine, lines), snapshot.MatchDetailsText);
        Assert.Equal(count, snapshot.MatchedRegularDetails.Count);
    }

    [AvaloniaFact]
    public async Task RegularSnapshot_ManyCadLinks_CompactsSortedIndicesAndRefreshesDiffSourceAfterOverrideAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var pads = Enumerable.Range(0, 10)
            .Select(index => ("signal", (double)index, 0d, index + 1d, 10d)).ToArray();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, pads);
        var vm = workspace.ViewModel;
        var cadIds = vm.CadPads.Select(pad => pad.Id).Order().ToArray();
        for (var index = 0; index < cadIds.Length; index++)
        {
            vm.SetCadOutputFwDiffIndexOverride(cadIds[index], 109 - index);
        }

        var regular = Assert.Single(vm.RegularPads);
        var snapshot = ReadRegular(vm, regular.RegularPadId);

        Assert.Equal($"CAD idx 100, 101, 102 (+7) (CAD id {cadIds[0]}, {cadIds[1]}, {cadIds[2]} (+7))", snapshot.MatchText);
        var lines = Enumerable.Range(0, 8).Select(index => $"CAD idx {109 - index}: 100%")
            .Append("... (+2 more)");
        Assert.Equal(string.Join(Environment.NewLine, lines), snapshot.MatchDetailsText);
        Assert.Equal("Probable", snapshot.DiffSource);
        Assert.Equal(10, snapshot.MatchedCadDetails.Count);

        vm.SetCadOutputFwDiffIndexOverride(cadIds[0], regular.DiffIndex);

        var refreshed = ReadRegular(vm, regular.RegularPadId);
        Assert.Equal("Strict", refreshed.DiffSource);
        Assert.StartsWith($"CAD idx {regular.DiffIndex}: 100%", refreshed.MatchDetailsText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task RegularSnapshot_StrictUniqueConflict_UsesCadIdentityForTheUnresolvedLinkedPadAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1,
            ("left", 0, 0, 4, 10), ("right", 6, 0, 10, 10));
        var vm = workspace.ViewModel;
        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.StrictUnique;
        var regular = Assert.Single(vm.RegularPads);

        var snapshot = ReadRegular(vm, regular.RegularPadId);

        var assigned = Assert.Single(snapshot.MatchedCadDetails, detail => detail.DxfIndex.HasValue);
        var unresolved = Assert.Single(snapshot.MatchedCadDetails, detail => !detail.DxfIndex.HasValue);
        Assert.Equal(0, assigned.DxfIndex);
        var cadIds = vm.CadPads.Select(pad => pad.Id).Order().ToArray();
        Assert.Equal($"CAD idx 0 (CAD id {cadIds[0]}, {cadIds[1]})", snapshot.MatchText);
        string[] expectedLines = ["CAD idx 0: 100%", $"CAD id {unresolved.CadPadId}: 100%"];
        Assert.Equal(expectedLines.Order(StringComparer.Ordinal),
            snapshot.MatchDetailsText.Split(Environment.NewLine).Order(StringComparer.Ordinal));
        Assert.Equal("Strict", snapshot.DiffSource);
    }

    [AvaloniaFact]
    public async Task CadSnapshot_AssignmentMode_PrioritizesManualOverrideThenSavedAnchorThenAutomaticModeAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 10, 10));
        var vm = workspace.ViewModel;
        var cad = Assert.Single(vm.CadPads);
        Assert.Equal("Auto (geometry seed)", ReadCad(vm, cad.Id).CadOutputFwDiffAssignmentModeText);

        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.StrictUnique;

        Assert.Equal("Auto (geometry seed, unique per IC)", ReadCad(vm, cad.Id).CadOutputFwDiffAssignmentModeText);

        vm.SetCadOutputFwDiffIndexAnchorCadPad(cad.Id);

        var anchored = ReadCad(vm, cad.Id);
        Assert.True(anchored.IsDxfIndexAnchor);
        Assert.Equal("Anchor saved (inactive in current mode)", anchored.CadOutputFwDiffAssignmentModeText);

        vm.SetCadOutputFwDiffIndexOverride(cad.Id, 42);

        var manual = ReadCad(vm, cad.Id);
        Assert.True(manual.IsDxfIndexAnchor);
        Assert.Equal("Manual override", manual.CadOutputFwDiffAssignmentModeText);
        Assert.Equal("42", manual.DxfIndexDisplayText);

        vm.ClearCadOutputFwDiffIndexOverride(cad.Id);

        Assert.Equal("Anchor saved (inactive in current mode)", ReadCad(vm, cad.Id).CadOutputFwDiffAssignmentModeText);
    }

    private static CadPadInspectorSnapshot ReadCad(FreeformHelperViewModel vm, int cadId)
    {
        return Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(cadId,
            includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: false,
            includeMatchDetails: true, includeRuleTrace: false)?.Cad);
    }

    private static RegularPadInspectorSnapshot ReadRegular(FreeformHelperViewModel vm, int regularId)
    {
        return Assert.IsType<RegularPadInspectorSnapshot>(vm.BuildRegularPadInspectorSnapshot(regularId,
            includeMatchDetails: true, includeRuleTrace: false)?.Regular);
    }
}
