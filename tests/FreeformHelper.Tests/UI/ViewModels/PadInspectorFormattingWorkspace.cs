// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

internal sealed class PadInspectorFormattingWorkspace : IDisposable
{
    private readonly DirectoryInfo _settingsDirectory = TestFiles.CreateTempDirectory();
    private readonly FileInfo _dxfFile;

    private PadInspectorFormattingWorkspace(
        int columns,
        (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        _dxfFile = new FileInfo(FreeformHelperViewModelTests.CreateTempDxfFile(pads));
        ViewModel = new FreeformHelperViewModel(new AppGeneralSettingsStore(
            Path.Combine(_settingsDirectory.FullName, "app.json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(_dxfFile.FullName),
            ActiveAreaWidth = columns * 10,
            ActiveAreaHeight = 10,
            PanelBiasX = 0,
            PanelBiasY = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            SelectedScanOrder = ScanOrder.LeftToRight_TopToBottom,
            RecalcBoundsOnLayerFilter = false,
            CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
        };
        var cascade = Assert.Single(ViewModel.CascadeIcSettings);
        cascade.XChannels = columns;
        cascade.YChannels = 1;
    }

    public FreeformHelperViewModel ViewModel { get; }

    public static async Task<PadInspectorFormattingWorkspace> OpenAsync(
        int columns,
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var workspace = new PadInspectorFormattingWorkspace(columns, pads);
        var vm = workspace.ViewModel;
        await vm.WaitForGridRebuildIdleAsync();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.Equal(pads.Length, vm.CadPads.Count);
        Assert.Equal(columns, vm.RegularPads.Count);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        return workspace;
    }

    public void Dispose()
    {
        _dxfFile.Delete();
        _settingsDirectory.Delete(recursive: true);
    }
}
