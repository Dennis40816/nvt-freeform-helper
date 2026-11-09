// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInspectorMatchOrderingBehaviorTests
{
    private static readonly int[] _expectedRegularIds = [0, 1];

    [AvaloniaFact]
    public async Task CadSnapshot_UnequalOverlaps_ShowsStrongestMatchBeforeTheLowestRegularIdAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(2, ("signal", 5, 0, 20, 10));
        var vm = workspace.ViewModel;
        var cad = Assert.Single(vm.CadPads);

        var snapshot = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(cad.Id,
            includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: false,
            includeMatchDetails: true, includeRuleTrace: false)?.Cad);

        Assert.Equal("IC1/diff1 (+1)", snapshot.MatchText);
        Assert.Equal(
            $"IC1/diff1 (regId 1): 66.7%{Environment.NewLine}IC1/diff0 (regId 0): 33.3%",
            snapshot.MatchDetailsText);
        Assert.Equal(_expectedRegularIds, snapshot.MatchedRegularPadIds);
    }

    [AvaloniaFact]
    public async Task RegularSnapshot_SharedOutputIndex_ListsTheIndexOnceAndRetainsBothCadCoveragesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1,
            ("wide", -10, 0, 10, 10), ("inner", 0, 0, 4, 10));
        var vm = workspace.ViewModel;
        var regular = Assert.Single(vm.RegularPads);
        var cadIds = vm.CadPads.Select(pad => pad.Id).Order().ToArray();

        var snapshot = Assert.IsType<RegularPadInspectorSnapshot>(vm.BuildRegularPadInspectorSnapshot(
            regular.RegularPadId, includeMatchDetails: true, includeRuleTrace: false)?.Regular);

        Assert.Equal($"CAD idx 0 (CAD id {cadIds[0]}, {cadIds[1]})", snapshot.MatchText);
        Assert.Equal($"CAD idx 0: 100%{Environment.NewLine}CAD idx 0: 50%", snapshot.MatchDetailsText);
        Assert.Equal("Strict", snapshot.DiffSource);
        Assert.Equal(cadIds, snapshot.MatchedCadPadIds);
    }
}
