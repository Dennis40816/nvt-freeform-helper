// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfIndexAnchorReselectBehaviorTests
{
    [AvaloniaFact]
    public async Task SetAnchorAfterClear_SameCad_RestoresTheAnchorAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync();
        var left = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        var right = vm.CadPads.Single(pad => pad.Layer == "right-a").Id;
        vm.SetCadOutputFwDiffIndexAnchorCadPad(right);
        vm.SetCadOutputFwDiffIndexAnchorCadPad(left);
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(left));
        vm.ClearCadOutputFwDiffIndexAnchorCadPad(left);
        Assert.False(vm.IsCadOutputFwDiffIndexAnchorCadPad(left));

        vm.SetCadOutputFwDiffIndexAnchorCadPad(left);

        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(left));
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(right));
        Assert.Equal(2, vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas.Count);
        Assert.Equal($"CAD Output FW Diff anchor saved: IC 1 -> CAD {left}.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task SetAnchorAfterClear_OtherCad_StillReplacesTheAnchorAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync();
        var leftA = vm.CadPads.Single(pad => pad.Layer == "left-a").Id;
        var leftB = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        vm.SetCadOutputFwDiffIndexAnchorCadPad(leftB);
        vm.ClearCadOutputFwDiffIndexAnchorCadPad(leftB);

        vm.SetCadOutputFwDiffIndexAnchorCadPad(leftA);

        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(leftA));
        Assert.False(vm.IsCadOutputFwDiffIndexAnchorCadPad(leftB));
    }

    private static async Task<FreeformHelperViewModel> OpenWorkspaceAsync()
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("left-a", 0, 0, 10, 10), ("left-b", 10, 0, 20, 10),
            ("right-a", 20, 0, 30, 10), ("right-b", 30, 0, 40, 10));
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("anchor-reselect-app-", ".json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            ActiveAreaWidth = 40,
            ActiveAreaHeight = 10,
            PanelBiasX = 0,
            PanelBiasY = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            CascadeNum = 2,
            SelectedScanOrder = ScanOrder.LeftToRight_TopToBottom,
            RecalcBoundsOnLayerFilter = false,
            CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
        };
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
        return vm;
    }
}
