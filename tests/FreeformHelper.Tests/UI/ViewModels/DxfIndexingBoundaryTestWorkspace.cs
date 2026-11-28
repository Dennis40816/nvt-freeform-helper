// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

internal static class DxfIndexingBoundaryTestWorkspace
{
    public static async Task<FreeformHelperViewModel> OpenAsync(OperationProgressLifecycleTestScope files)
    {
        var vm = await files.CreateWithoutDxfAsync();
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("left-a", 0, 0, 10, 10), ("left-b", 10, 0, 20, 10),
            ("right-a", 20, 0, 30, 10), ("right-b", 30, 0, 40, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        vm.ActiveAreaWidth = 40;
        vm.ActiveAreaHeight = 10;
        vm.PanelBiasX = 0;
        vm.PanelBiasY = 0;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.GridPaddingPercent = 0;
        vm.CascadeNum = 2;
        vm.SelectedScanOrder = ScanOrder.LeftToRight_TopToBottom;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
        Assert.Equal(2, vm.CascadeIcSettings.Count);
        foreach (var cascade in vm.CascadeIcSettings)
        {
            cascade.XChannels = 2;
            cascade.YChannels = 1;
        }

        await vm.WaitForGridRebuildIdleAsync();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.Equal(4, vm.CadPads.Count);
        Assert.Equal(2, vm.RegularPads.Count(pad => pad.IcIndex == 0));
        Assert.Equal(2, vm.RegularPads.Count(pad => pad.IcIndex == 1));
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        return vm;
    }
}
