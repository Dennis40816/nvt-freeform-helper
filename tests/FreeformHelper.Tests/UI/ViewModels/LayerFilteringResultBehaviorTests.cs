// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerFilteringResultBehaviorTests
{
    [AvaloniaFact]
    public async Task AnchorSummary_FiveIcAnchors_IndicatesOverflowWhileCanvasUsesOnlyVisibleAnchorsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(
            [("ic-a", 0, 0, 10, 10), ("ic-b", 10, 0, 20, 10), ("ic-c", 20, 0, 30, 10),
             ("ic-d", 30, 0, 40, 10), ("ic-e", 40, 0, 50, 10)], cascadeCount: 5);
        var pads = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).ToArray();
        foreach (var pad in pads)
        {
            vm.SetCadOutputFwDiffIndexAnchorCadPad(pad.Id);
        }

        var firstFour = $"IC1->CAD {pads[0].Id}, IC2->CAD {pads[1].Id}, IC3->CAD {pads[2].Id}, IC4->CAD {pads[3].Id}";
        Assert.Contains($"anchor=stored (inactive in current modes: {firstFour} (+))",
            vm.CadOutputFwDiffAssignmentSummary, StringComparison.Ordinal);
        Assert.Contains("IC groups=5", vm.CadOutputFwDiffAssignmentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain($"IC5->CAD {pads[4].Id}", vm.CadOutputFwDiffAssignmentSummary, StringComparison.Ordinal);
        Assert.Equal(pads.Select(static pad => pad.Id).Order(), vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas.Order());

        vm.ApplyLayerSelection(vm.LayerToggles.Single(static toggle => toggle.Name == "ic-e"), soloSelect: false);

        Assert.Equal(4, vm.CadPads.Count);
        Assert.DoesNotContain(pads[4].Id, vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas);
        Assert.Equal(4, vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas.Count);
        Assert.Contains($"{firstFour} (+)", vm.CadOutputFwDiffAssignmentSummary, StringComparison.Ordinal);

        vm.ClearCadOutputFwDiffIndexAnchorCadPad(pads[0].Id);

        Assert.DoesNotContain("(+)", vm.CadOutputFwDiffAssignmentSummary, StringComparison.Ordinal);
        Assert.Contains($"IC5->CAD {pads[4].Id}", vm.CadOutputFwDiffAssignmentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain($"IC1->CAD {pads[0].Id}", vm.CadOutputFwDiffAssignmentSummary, StringComparison.Ordinal);
        Assert.Equal(3, vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas.Count);
    }

    [AvaloniaFact]
    public async Task ImportAllZeroMask_LeavesCadWithoutOutputIndexAndDoesNotRepairOntoInactiveCellsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync([("fixed", 0, 0, 10, 10), ("repair", 0, 0, 19, 10)], cascadeCount: 1);
        var fixedId = vm.CadPads.Single(static pad => pad.Layer == "fixed").Id;
        var repairId = vm.CadPads.Single(static pad => pad.Layer == "repair").Id;
        Assert.True(vm.TryGetCadOutputFwDiffIndex(fixedId, out var fixedDiff));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var repairDiff));
        Assert.Equal(fixedDiff, repairDiff);
        var path = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,0,0\n");
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(path);

        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.Contains("Active=0, inactive=2, frames=1.", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.False(vm.TryGetCadOutputFwDiffIndex(fixedId, out _));
        Assert.False(vm.TryGetCadOutputFwDiffIndex(repairId, out _));
        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(repairId));
        var summary = vm.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();
        Assert.Equal("csv-constrained", summary.Mode);
        Assert.Equal(2, summary.DecisionCount);
        Assert.Equal(2, summary.DecisionSourceCounts["seed"]);
        Assert.False(summary.DecisionSourceCounts.ContainsKey("auto-repaired"));
    }

    [AvaloniaFact]
    public async Task ImportMask_RepairMayUseDiffAlreadyAssignedInAnotherIcAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(
            [("fixed", 0, 0, 10, 10), ("repair", 0, 0, 19, 10),
             ("other-ic-first", 20, 0, 30, 10), ("other-ic-second", 30, 0, 40, 10)], cascadeCount: 2);
        var fixedId = vm.CadPads.Single(static pad => pad.Layer == "fixed").Id;
        var repairId = vm.CadPads.Single(static pad => pad.Layer == "repair").Id;
        var otherIcId = vm.CadPads.Single(static pad => pad.Layer == "other-ic-second").Id;
        Assert.True(vm.TryGetCadOutputFwDiffIndex(fixedId, out var fixedDiff));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var originalRepair));
        Assert.Equal(fixedDiff, originalRepair);
        var targetDiff = vm.RegularPads.Single(static pad => pad.Col == 1).DiffIndex;
        Assert.NotEqual(originalRepair, targetDiff);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(otherIcId, out var otherIcDiff));
        Assert.Equal(targetDiff, otherIcDiff);
        var path = TestFiles.WriteTempCsv("Xch:\"4\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,10,10,10,10\n");
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(path);

        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var repaired));
        Assert.Equal(targetDiff, repaired);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(otherIcId, out var unchangedOtherIc));
        Assert.Equal(otherIcDiff, unchangedOtherIc);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(fixedId, out var unchangedFixed));
        Assert.Equal(fixedDiff, unchangedFixed);
        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(repairId));
        var summary = vm.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();
        Assert.Equal("csv-constrained", summary.Mode);
        Assert.Equal(1, summary.DecisionSourceCounts["auto-repaired"]);
        Assert.Equal(3, summary.DecisionSourceCounts["seed"]);
    }

    private static async Task<FreeformHelperViewModel> OpenWorkspaceAsync(
        (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] rectangles,
        int cascadeCount)
    {
        var columnsPerIc = cascadeCount == 5 ? 1 : 2;
        var totalColumns = cascadeCount * columnsPerIc;
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(rectangles);
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("layer-result-app-", ".json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            ActiveAreaWidth = totalColumns * 10,
            ActiveAreaHeight = 10,
            PanelBiasX = 0,
            PanelBiasY = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            CascadeNum = cascadeCount,
            SelectedScanOrder = ScanOrder.LeftToRight_TopToBottom,
            RecalcBoundsOnLayerFilter = false,
            CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
        };
        foreach (var cascade in vm.CascadeIcSettings)
        {
            cascade.XChannels = columnsPerIc;
            cascade.YChannels = 1;
        }

        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.Equal(rectangles.Length, vm.CadPads.Count);
        Assert.Equal(totalColumns, vm.RegularPads.Count);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        return vm;
    }
}
