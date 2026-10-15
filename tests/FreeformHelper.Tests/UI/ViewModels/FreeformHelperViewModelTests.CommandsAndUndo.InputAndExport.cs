using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{


    [Fact]
    public async Task OpenDxfCommand_WhenDialogHandlerMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        await vm.OpenDxfCommand.ExecuteAsync(null);

        Assert.Equal("Open DXF: dialog handler not wired.", vm.StatusText);
    }


    [Fact]
    public async Task LoadProjectCommand_WhenDialogHandlerMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal("Load project: dialog handler not wired.", vm.StatusText);
    }


    [Fact]
    public async Task ExportDxfLayerImageCommand_WhenDxfMissing_ShowsWarningDialog()
    {
        var vm = new FreeformHelperViewModel();
        var warningShown = false;
        string? warningTitle = null;
        string? warningMessage = null;

        vm.ShowWarningAsync = (title, message) =>
        {
            warningShown = true;
            warningTitle = title;
            warningMessage = message;
            return Task.CompletedTask;
        };

        await vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.Equal("Export DXF image: import DXF first.", vm.StatusText);
        Assert.True(warningShown);
        Assert.Equal("DXF not loaded", warningTitle);
        Assert.Contains("requires an imported DXF", warningMessage, StringComparison.Ordinal);
    }


    [Fact]
    public async Task ExportDxfVisibleCommand_WhenDxfMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

        Assert.Equal("Export DXF: import DXF first.", vm.StatusText);
    }


    [Fact]
    public async Task ExportDxfAllCommand_WhenDxfMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        await vm.ExportDxfAllCommand.ExecuteAsync(null);

        Assert.Equal("Export DXF: import DXF first.", vm.StatusText);
    }


    [Fact]
    public async Task ExportDxfVisibleCommand_WhenDxfRegularSourceLayerSelected_IncludesRegularLayerPads()
    {
        var vm = new FreeformHelperViewModel();
        var signalPad = CreateCad(id: 1, layer: "signal", minX: 0, minY: 0, maxX: 10, maxY: 10);
        var regularLayerPad = CreateCad(id: 2, layer: "regular", minX: 20, minY: 0, maxX: 30, maxY: 10);
        var importedPads = new[] { signalPad, regularLayerPad };
        var visiblePads = new System.Collections.ObjectModel.ObservableCollection<CadPad> { signalPad };
        var cad = new CadPadSet(importedPads);

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        cadField!.SetValue(vm, cad);

        vm.CadPads = visiblePads;
        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        vm.SelectedRegularSourceLayerOption = new FreeformHelperViewModel.RegularSourceLayerOption("regular", "regular");

        var outputPath = Path.Combine(Path.GetTempPath(), $"freeformhelper-visible-{Guid.NewGuid():N}.dxf");
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(outputPath);

        try
        {
            await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

            var text = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
            var polylineCount = text
                .Split("LWPOLYLINE", StringSplitOptions.None)
                .Length - 1;

            Assert.Equal(2, polylineCount);
            Assert.Contains("8\r\nsignal\r\n", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("8\r\nregular\r\n", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }


    [Fact]
    public async Task ExportNotchCommand_WhenPrerequisitesMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("Export notch: import DXF and build grid first.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportDxfVisible_WhenTheSaveDialogFails_ReportsTheFailureAndKeepsRunning()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            vm.PickSaveExportPathAsync = static (_, _) => throw new IOException("disk gone");

            await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

            Assert.Contains("Export DXF failed", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains("disk gone", vm.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task ExportDxfVisible_WhenTheSaveDialogRunsOutOfMemory_Propagates()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var failure = CreateBoundaryOutOfMemory();
            vm.PickSaveExportPathAsync = (_, _) => throw failure;

            await Assert.ThrowsAsync<OutOfMemoryException>(() => vm.ExportDxfVisibleCommand.ExecuteAsync(null));
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task ExportDxfLayerImage_WhenTheRequestDialogFails_ReportsTheFailureAndKeepsRunning()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            vm.PickSaveExportPathAsync = static (_, _) => Task.FromResult<string?>(null);
            vm.OpenDxfLayerImageExportAsync = static _ => throw new IOException("dialog gone");

            await vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

            Assert.Contains("Export DXF image failed", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains("dialog gone", vm.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task ExportDxfLayerImage_WhenTheRequestDialogRunsOutOfMemory_Propagates()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var failure = CreateBoundaryOutOfMemory();
            vm.PickSaveExportPathAsync = static (_, _) => Task.FromResult<string?>(null);
            vm.OpenDxfLayerImageExportAsync = _ => throw failure;

            await Assert.ThrowsAsync<OutOfMemoryException>(() => vm.ExportDxfLayerImageCommand.ExecuteAsync(null));
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }
}
