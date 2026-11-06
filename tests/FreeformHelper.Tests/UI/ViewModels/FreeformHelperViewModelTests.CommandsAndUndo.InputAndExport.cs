using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
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

        FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;

        vm.CadPads = visiblePads;
        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        vm.SelectedRegularSourceLayerOption = new FreeformHelperViewModel.RegularSourceLayerOption("regular", "regular");

        var outputPath = TestFiles.NewTempFilePath("freeformhelper-visible-", ".dxf");
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(outputPath);

        try
        {
            await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

            var text = await TestFiles.ReadContentAsync(outputPath, TestContext.Current.CancellationToken);
            var polylineCount = text
                .Split("LWPOLYLINE", StringSplitOptions.None)
                .Length - 1;

            // DxfExportService writes each line with StringBuilder.AppendLine, so the line break is Environment.NewLine.
            var newLine = Environment.NewLine;
            Assert.Equal(2, polylineCount);
            Assert.Contains($"8{newLine}signal{newLine}", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"8{newLine}regular{newLine}", text, StringComparison.OrdinalIgnoreCase);
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
    public async Task ExportDxfCommands_WithEdits_PreserveGeometryAcrossVisibleAndAllLayersAsync()
    {
        var dxfPath = CreateTempDxfFile(
            ("hidden", 0, 0, 10, 10),
            ("signal", 20, 0, 30, 10),
            ("signal", 28, 0, 38, 10),
            ("signal", 50, 0, 60, 20),
            ("aux", 80, 0, 90, 10));
        var outputPath = TestFiles.NewTempFilePath("freeformhelper-edited-export-", ".dxf");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
                PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(outputPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var ids = vm.CadPads.OrderBy(static pad => pad.Id).Select(static pad => pad.Id).ToArray();
            Assert.Equal(5, ids.Length);
            await SelectCadPadsAsync(vm, [ids[0]]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [ids[1], ids[2]]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [ids[3]]);
            vm.DxfEditNewLayerName = "edited";
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [ids[3]]);
            vm.DxfEditRotationDegrees = 90m;
            vm.RotateSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            vm.LayerToggles.Single(static layer => layer.Name == "aux").IsSelected = false;
            await WaitForGridRebuildAsync(vm);

            await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

            var visibleText = await TestFiles.ReadContentAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Equal(2, visibleText.Split("LWPOLYLINE", StringSplitOptions.None).Length - 1);
            var newLine = Environment.NewLine;
            Assert.DoesNotContain($"8{newLine}hidden{newLine}", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain($"8{newLine}aux{newLine}", visibleText, StringComparison.Ordinal);
            Assert.Contains($"8{newLine}edited{newLine}", visibleText, StringComparison.Ordinal);
            var visiblePads = DxfPadImporter.Import(outputPath).Pads;
            var combined = Assert.Single(visiblePads, static pad => pad.Layer == "signal");
            Assert.Equal(20d, combined.Bounds.MinX, 6);
            Assert.Equal(38d, combined.Bounds.MaxX, 6);
            Assert.Equal(180d, combined.Area, 6);
            var rotated = Assert.Single(visiblePads, static pad => pad.Layer == "edited");
            Assert.Equal(45d, rotated.Bounds.MinX, 6);
            Assert.Equal(5d, rotated.Bounds.MinY, 6);
            Assert.Equal(65d, rotated.Bounds.MaxX, 6);
            Assert.Equal(15d, rotated.Bounds.MaxY, 6);

            await vm.ExportDxfAllCommand.ExecuteAsync(null);

            // Hidden pads stay excluded on every export (refactor-test-plan A02, A04); All only widens the layer scope.
            var allText = await TestFiles.ReadContentAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Equal(3, allText.Split("LWPOLYLINE", StringSplitOptions.None).Length - 1);
            Assert.Contains($"8{newLine}aux{newLine}", allText, StringComparison.Ordinal);
            Assert.Contains($"8{newLine}edited{newLine}", allText, StringComparison.Ordinal);
            var allPads = DxfPadImporter.Import(outputPath).Pads;
            Assert.Equal(3, allPads.Count);
            Assert.All(visiblePads, pad => Assert.Contains(allPads, output =>
                output.Layer == pad.Layer && output.Bounds == pad.Bounds));
            var auxiliary = Assert.Single(allPads, static pad => pad.Layer == "aux");
            Assert.Equal(80d, auxiliary.Bounds.MinX, 6);
            Assert.Equal(90d, auxiliary.Bounds.MaxX, 6);
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(outputPath);
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
