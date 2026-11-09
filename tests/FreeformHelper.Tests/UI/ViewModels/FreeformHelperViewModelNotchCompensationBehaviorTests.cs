// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class FreeformHelperViewModelNotchCompensationBehaviorTests
{
    [AvaloniaFact]
    public void CompensationQueries_WithoutGrid_ReturnNoDetailsEvenWithPrecomputedCompensation()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var cad = TestGeometryFactory.CreateCadPad(101, "signal", 0, 0, 5, 10);
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        var compensation = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>([cad]),
        };

        Assert.Null(vm.GetCadV22CompensationPreview(cad.Id));
        Assert.Null(vm.GetCadV22CompensationDiagnostics(cad.Id));
        Assert.Null(vm.GetCadV22CompensationResult(cad.Id));
        Assert.Null(vm.GetCadV22ResolvedResult(cad.Id));
        Assert.Null(vm.GetCadV22TargetAllocationSummary(cad.Id));
        Assert.Null(vm.GetCadV22StageOverlays(cad.Id));
        Assert.Empty(vm.GetCrossIcOwnerSharesForRegularPad(grid.Pads[0].RegularPadId));
        var inspector = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: false,
            includeRuleTrace: false,
            precomputedCompensation: compensation);

        var cadSnapshot = Assert.IsType<CadPadInspectorSnapshot>(inspector?.Cad);
        Assert.Null(cadSnapshot.Notch);
    }

    [AvaloniaFact]
    public async Task CompensationQueries_ForLoadedCad_ProjectRatiosDiagnosticsAndDistinctStages()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateDetailsViewModelAsync();
        var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");

        var preview = vm.GetCadV22CompensationPreview(cad.Id);
        var overlays = vm.GetCadV22StageOverlays(cad.Id);
        var diagnostics = vm.GetCadV22CompensationDiagnostics(cad.Id);

        Assert.NotNull(preview);
        Assert.Equal(0.36, preview.Value.ToRegularRatio, 6);
        Assert.Equal(100.0 / 36.0, preview.Value.ToFullRatio, 6);
        Assert.Equal(1.0, preview.Value.CombinedRatio, 6);
        Assert.True(preview.Value.IsToFullEnabled);
        Assert.NotNull(overlays);
        Assert.True(overlays.Value.IsToFullEnabled);
        Assert.Equal(36.0, Assert.Single(overlays.Value.Stage1Seed).Area(), 6);
        Assert.Equal(100.0, Assert.Single(overlays.Value.Stage2Candidate).Area(), 6);
        Assert.Equal(100.0, Assert.Single(overlays.Value.Stage3Final).Area(), 6);
        Assert.NotNull(diagnostics);
        Assert.Contains(" : ov=36, src=36, blk=0, reach=100, blocker=0, owners=", diagnostics, StringComparison.Ordinal);
        Assert.Contains("ownerCnt=1, ownerShare=-, boundary=N, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)",
            diagnostics, StringComparison.Ordinal);

        vm.EnableToFull = false;

        var disabled = vm.GetCadV22CompensationPreview(cad.Id);
        Assert.NotNull(disabled);
        Assert.Equal((0.36, 1.0, 0.36, false), disabled.Value);
        Assert.Contains("candidate=N, tofull=N", vm.GetCadV22CompensationDiagnostics(cad.Id), StringComparison.Ordinal);
        Assert.Null(vm.GetCadV22CompensationPreview(int.MaxValue));
        Assert.Null(vm.GetCadV22CompensationDiagnostics(int.MaxValue));
        Assert.Null(vm.GetCadV22StageOverlays(int.MaxValue));
        Assert.Empty(vm.GetCrossIcOwnerSharesForRegularPad(int.MaxValue));
    }

    [AvaloniaFact]
    public async Task CrossIcOwnerShares_RespectStrictThresholdOverridesAndHiddenPads()
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("signal-a", 0, 0, 6, 10),
            ("signal-b", 6, 0, 20, 10));
        var vm = await LoadCompensationViewModelAsync(path, width: 20, height: 10, cascadeCount: 2, channels: 1);
        await vm.MatchCommand.ExecuteAsync(null);
        var cadA = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
        var cadB = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-b");
        Assert.True(vm.TryGetCadIcIndex(cadA.Id, out var icA));
        Assert.True(vm.TryGetCadIcIndex(cadB.Id, out var icB));
        Assert.Equal((0, 1), (icA, icB));
        var regularId = Assert.Single(vm.RegularPads, static pad => pad.IcIndex == 0).RegularPadId;

        Assert.Equal([(cadA.Id, 60.0), (cadB.Id, 40.0)], vm.GetCrossIcOwnerSharesForRegularPad(regularId));
        vm.ToFullStrictOverlapPercent = 50m;
        Assert.Equal((cadA.Id, 60.0), Assert.Single(vm.GetCrossIcOwnerSharesForRegularPad(regularId)));
        Assert.Equal(2, vm.GetCrossIcOwnerSharesForRegularPad(regularId, strictOverlapPercentOverride: -10).Count);
        Assert.Equal((cadA.Id, 60.0),
            Assert.Single(vm.GetCrossIcOwnerSharesForRegularPad(regularId, strictOverlapPercentOverride: 40)));
        Assert.Empty(vm.GetCrossIcOwnerSharesForRegularPad(regularId, strictOverlapPercentOverride: 110));

        vm.ApplyCanvasSelection([cadB.Id], Array.Empty<int>());
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        var rebuiltRegularId = Assert.Single(vm.RegularPads, static pad => pad.IcIndex == 0).RegularPadId;

        Assert.True(vm.HasManualHiddenCadPads);
        Assert.Equal((cadA.Id, 60.0),
            Assert.Single(vm.GetCrossIcOwnerSharesForRegularPad(rebuiltRegularId, strictOverlapPercentOverride: 0)));
    }

    [AvaloniaFact]
    public async Task LightweightInspector_WithColdPrecomputedCompensation_PublishesTargetsForLaterQueries()
    {
        var vm = await CreateDetailsViewModelAsync();
        var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
        var grid = new RegularGrid(3, 3, [0, 10, 20, 30], [0, 10, 20, 30], vm.RegularPads.ToArray());
        var compensation = NotchV22CompensationService.Compute(
            cad, grid, enableToRegular: true, enableToFull: true, allCadPads: vm.CadPads);
        var cold = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: false,
            includeRuleTrace: false);
        var coldCad = Assert.IsType<CadPadInspectorSnapshot>(cold?.Cad);
        Assert.Null(coldCad.Notch);

        var supplied = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: false,
            includeRuleTrace: false,
            precomputedCompensation: compensation);

        var notch = Assert.IsType<PadInspectorNotchSnapshot>(supplied?.Cad?.Notch);
        Assert.Equal(0.36, notch.ToRegularRatio, 6);
        Assert.Equal(100.0, notch.Stage3Area, 6);
        Assert.True(notch.IsToFullEnabled);
        Assert.Null(notch.Diagnostics);
        Assert.Equal(100, Assert.Single(notch.Targets).RatioPercentRounded);
        Assert.Same(compensation, vm.GetCadV22CompensationResult(cad.Id));
        var warm = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: false,
            includeRuleTrace: false);
        var warmNotch = Assert.IsType<PadInspectorNotchSnapshot>(warm?.Cad?.Notch);
        Assert.Equal(notch.Stage3Area, warmNotch.Stage3Area);
        Assert.Equal(100, Assert.Single(warmNotch.Targets).RatioPercentRounded);
    }

    private static Task<FreeformHelperViewModel> CreateDetailsViewModelAsync()
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("frame", 0, 0, 1, 1),
            ("signal-a", 12, 12, 18, 18),
            ("signal-b", 22, 12, 28, 18),
            ("frame", 29, 29, 30, 30));
        return LoadCompensationViewModelAsync(path, width: 30, height: 30, cascadeCount: 1, channels: 3);
    }

    private static async Task<FreeformHelperViewModel> LoadCompensationViewModelAsync(
        string path, int width, int height, int cascadeCount, int channels)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            ActiveAreaWidth = width,
            ActiveAreaHeight = height,
            GridPaddingPercent = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            CascadeNum = cascadeCount,
            EnableToRegular = true,
            EnableToFull = true,
            EnableBoundaryVirtualAreaCap = false,
            ToFullStrictOverlapPercent = 0.1m,
            NotchPreviewAutoPlayEnabled = false,
            RecalcBoundsOnLayerFilter = false,
        };
        foreach (var cascade in vm.CascadeIcSettings)
        {
            cascade.XChannels = channels;
            cascade.YChannels = channels;
        }

        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Equal(cascadeCount * channels * channels, vm.RegularPads.Count);
        return vm;
    }
}
