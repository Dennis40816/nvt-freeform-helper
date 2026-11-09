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
public sealed class NotchCompensationDiagnosticsBehaviorTests
{
    [AvaloniaFact]
    public async Task Diagnostics_ThirteenOwners_ShowsFirstTwelveIdsAndRetainsTotalCountAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateGridAsync();
        vm.ToFullStrictOverlapPercent = 1m;
        vm.CadPads = new ObservableCollection<CadPad>(Enumerable.Range(1, 13).Reverse()
            .Select(id => TestGeometryFactory.CreateCadPad(id, "signal", (id - 1) * 0.5, 0, id * 0.5, 10)));

        var diagnostics = vm.GetCadV22CompensationDiagnostics(1);

        Assert.NotNull(diagnostics);
        Assert.Contains("owners=1;2;3;4;5;6;7;8;9;10;11;12;+1, ownerCnt=13", diagnostics, StringComparison.Ordinal);
        Assert.Contains("boundary=Y", diagnostics, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Diagnostics_OverlapAtStrictThreshold_ShowsNoOwnerOrToFullExpansionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateGridAsync();
        vm.ToFullStrictOverlapPercent = 20m;
        vm.CadPads = new ObservableCollection<CadPad>([TestGeometryFactory.CreateCadPad(7, "signal", 0, 0, 2, 10)]);

        var diagnostics = vm.GetCadV22CompensationDiagnostics(7);

        Assert.NotNull(diagnostics);
        Assert.Contains("ov=20, src=0, blk=0, reach=20, blocker=0, owners=-, ownerCnt=0", diagnostics, StringComparison.Ordinal);
        Assert.Contains("boundary=N, candidate=N, tofull=N", diagnostics, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Diagnostics_NineRegulars_ShowsEightRowsInDiffOrderAndReportsOverflowAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateGridAsync(channels: 3);
        for (var index = 0; index < vm.RegularPads.Count; index++)
        {
            vm.RegularPads[index].DiffIndex = 8 - index;
        }

        var expectedPrefixes = vm.RegularPads.Reverse().Take(8)
            .Select(pad => $"IC1/diff{pad.DiffIndex} reg{pad.RegularPadId} : ").ToArray();
        vm.CadPads = new ObservableCollection<CadPad>([TestGeometryFactory.CreateCadPad(7, "signal", 0, 0, 30, 30)]);

        var diagnostics = vm.GetCadV22CompensationDiagnostics(7);

        Assert.NotNull(diagnostics);
        var lines = diagnostics.Split(Environment.NewLine, StringSplitOptions.None);
        Assert.Equal(9, lines.Length);
        for (var index = 0; index < expectedPrefixes.Length; index++)
        {
            Assert.StartsWith(expectedPrefixes[index], lines[index], StringComparison.Ordinal);
        }

        Assert.Equal("+1 more regular(s)", lines[8]);
    }

    [AvaloniaFact]
    public async Task OwnerShares_ClipsGeometryExcludesThresholdAndOutsidePadsAndSortsTiesByIdAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateGridAsync();
        var regular = Assert.Single(vm.RegularPads);
        vm.CadPads = new ObservableCollection<CadPad>
        {
            TestGeometryFactory.CreateCadPad(30, "signal", 0, 0, 3, 10),
            TestGeometryFactory.CreateCadPad(20, "signal", 7, 0, 12, 10),
            TestGeometryFactory.CreateCadPad(10, "signal", -5, 0, 5, 10),
            TestGeometryFactory.CreateCadPad(5, "signal", 0, 0, 2, 10),
            TestGeometryFactory.CreateCadPad(99, "signal", 11, 0, 12, 10),
        };

        var owners = vm.GetCrossIcOwnerSharesForRegularPad(regular.RegularPadId, strictOverlapPercentOverride: 20);

        (int CadId, double RatioPercent)[] expected = [(10, 50.0), (20, 30.0), (30, 30.0)];
        Assert.Equal(expected, owners);
    }

    [AvaloniaFact]
    public async Task OwnerShares_GridBeforeCadImport_ReturnsEmptyAsync()
    {
        var vm = await CreateGridAsync();
        var regular = Assert.Single(vm.RegularPads);
        Assert.Empty(vm.CadPads);

        var owners = vm.GetCrossIcOwnerSharesForRegularPad(regular.RegularPadId);

        Assert.Empty(owners);
    }

    private static async Task<FreeformHelperViewModel> CreateGridAsync(int channels = 1)
    {
        var vm = new FreeformHelperViewModel
        {
            ActiveAreaWidth = channels * 10,
            ActiveAreaHeight = channels * 10,
            PanelBiasX = 0,
            PanelBiasY = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            CascadeNum = 1,
            EnableToRegular = true,
            EnableToFull = true,
            EnableBoundaryVirtualAreaCap = false,
            NotchPreviewAutoPlayEnabled = false,
            RecalcBoundsOnLayerFilter = false,
        };
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = channels;
        cascade.YChannels = channels;
        await vm.WaitForGridRebuildIdleAsync();
        Assert.Equal(channels * channels, vm.RegularPads.Count);
        Assert.All(vm.RegularPads, static pad => Assert.Equal(100.0, pad.Area, 6));
        return vm;
    }
}
