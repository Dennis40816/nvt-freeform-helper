// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfExportContentTests
{
    private static readonly (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] _overlappingPads =
        [("alpha", 0, 0, 10, 10), ("beta", 8, 0, 18, 10)];

    [Fact]
    public async Task ExportDxfVisibleCommand_DeletedRegularSourcePad_IsExcludedFromTextAndCountAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        await DeleteAlphaPadAsync(vm);
        vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(static option => option.Name == "alpha");
        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        await WaitForGridRebuildAsync(vm);

        var text = await ExportAsync(vm, visibleOnly: true);

        AssertPadCount(text, 1);
        Assert.Contains(LayerField("beta"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(LayerField("alpha"), text, StringComparison.Ordinal);
        Assert.Equal("DXF exported (visible). Pads=1.", vm.StatusText);
    }

    [Fact]
    public async Task ExportDxfAllCommand_DeletedPadAndDeselectedSurvivor_ExportsOnlySurvivorAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        await DeleteAlphaPadAsync(vm);
        vm.LayerToggles.Single(static layer => layer.Name == "beta").IsSelected = false;
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);

        var text = await ExportAsync(vm, visibleOnly: false);

        AssertPadCount(text, 1);
        Assert.Contains(LayerField("beta"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(LayerField("alpha"), text, StringComparison.Ordinal);
        Assert.Equal("DXF exported (all). Pads=1.", vm.StatusText);
    }

    [Fact]
    public async Task ExportDxfCommands_PadsCombinedAcrossLayers_WriteCombinedPadOnceAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await OpenOverlappingPadsAsync(fixture);
        await SelectCadPadsAsync(vm, vm.CadPads.Select(static pad => pad.Id).ToArray());
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.Single(vm.CadPads);
        var newLine = Environment.NewLine;

        var visibleText = await ExportAsync(vm, visibleOnly: true);

        AssertPadCount(visibleText, 1);
        Assert.Contains(LayerField("alpha"), visibleText, StringComparison.Ordinal);
        Assert.DoesNotContain(LayerField("beta"), visibleText, StringComparison.Ordinal);
        Assert.Contains($"10{newLine}18{newLine}20{newLine}", visibleText, StringComparison.Ordinal);
        Assert.Equal("DXF exported (visible). Pads=1.", vm.StatusText);

        var allText = await ExportAsync(vm, visibleOnly: false);

        AssertPadCount(allText, 1);
        Assert.Contains(LayerField("alpha"), allText, StringComparison.Ordinal);
        Assert.DoesNotContain(LayerField("beta"), allText, StringComparison.Ordinal);
        Assert.Contains($"10{newLine}18{newLine}20{newLine}", allText, StringComparison.Ordinal);
        Assert.Equal("DXF exported (all). Pads=1.", vm.StatusText);
    }

    [Fact]
    public async Task ExportDxfVisibleCommand_PadMovedToNewLayer_WritesNewLayerWithoutOldLayerAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var alphaId = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id;
        await SelectCadPadsAsync(vm, [alphaId]);
        vm.DxfEditNewLayerName = "edited";
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        var text = await ExportAsync(vm, visibleOnly: true);

        AssertPadCount(text, 2);
        Assert.Contains(LayerField("edited"), text, StringComparison.Ordinal);
        Assert.Contains(LayerField("beta"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(LayerField("alpha"), text, StringComparison.Ordinal);
        Assert.Equal("DXF exported (visible). Pads=2.", vm.StatusText);
    }

    [Fact]
    public async Task ExportDxfCommands_CancelledSaveDialog_AsksForTheNameAndKeepsStatusAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var pickerRequests = new List<(string SuggestedName, string Extension)>();
        vm.PickSaveExportPathAsync = (suggestedName, extension) =>
        {
            pickerRequests.Add((suggestedName, extension));
            return Task.FromResult<string?>(null);
        };
        var statusBefore = vm.StatusText;

        await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

        Assert.Equal(("cad_visible_layers", "dxf"), Assert.Single(pickerRequests));
        Assert.Equal(statusBefore, vm.StatusText);
        Assert.DoesNotContain("exported", vm.StatusText, StringComparison.OrdinalIgnoreCase);

        await vm.ExportDxfAllCommand.ExecuteAsync(null);

        Assert.Equal(2, pickerRequests.Count);
        Assert.Equal(("cad_all_layers", "dxf"), pickerRequests[1]);
        Assert.Equal(statusBefore, vm.StatusText);
        Assert.DoesNotContain("exported", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task DeleteAlphaPadAsync(FreeformHelperViewModel vm)
    {
        var alphaId = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id;
        await SelectCadPadsAsync(vm, [alphaId]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
    }

    private static async Task<FreeformHelperViewModel> OpenOverlappingPadsAsync(WorkspaceFixture fixture)
    {
        // Overlapping inputs produce one polygon; separated inputs produce one output polygon per island.
        var path = CreateTempDxfFile(_overlappingPads);
        try
        {
            var vm = fixture.CreateViewModel();
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            return vm;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> ExportAsync(FreeformHelperViewModel vm, bool visibleOnly)
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "export.dxf");
            vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(path);
            var command = visibleOnly ? vm.ExportDxfVisibleCommand : vm.ExportDxfAllCommand;

            await command.ExecuteAsync(null);

            return await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void AssertPadCount(string text, int expectedCount) =>
        Assert.Equal(expectedCount, text.Split("LWPOLYLINE", StringSplitOptions.None).Length - 1);

    private static string LayerField(string layer) => $"8{Environment.NewLine}{layer}{Environment.NewLine}";
}
