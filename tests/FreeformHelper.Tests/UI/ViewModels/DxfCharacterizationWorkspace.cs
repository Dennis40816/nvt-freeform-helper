// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

internal static class DxfCharacterizationWorkspace
{
    private static readonly (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] _defaultPads =
        [("alpha", 0, 0, 10, 20), ("beta", 30, 0, 40, 20)];

    public static async Task<FreeformHelperViewModel> OpenAsync(
        LoadingModeTestScope files,
        bool recalcBounds = false,
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var generated = FreeformHelperViewModelTests.CreateTempDxfFile(pads.Length > 0 ? pads : _defaultPads);
        try
        {
            TestFiles.CopyOverwriting(generated, files.PathFor("input.dxf"));
        }
        finally
        {
            File.Delete(generated);
        }

        var vm = files.CreateViewModel();
        using (FreeformHelperViewModelPrivateAccess.UndoSuppression(vm).Enter())
        {
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(files.PathFor("input.dxf"));
            vm.ActiveAreaWidth = 40;
            vm.ActiveAreaHeight = 20;
            vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
            vm.GridPaddingPercent = 0;
            vm.RecalcBoundsOnLayerFilter = recalcBounds;
            vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
            vm.AutoReplayStep2AfterProjectLoad = false;
            vm.NotchPreviewAutoPlayEnabled = false;
            var cascade = Assert.Single(vm.CascadeIcSettings);
            cascade.XChannels = 2;
            cascade.YChannels = 1;
        }

        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        return vm;
    }
}
