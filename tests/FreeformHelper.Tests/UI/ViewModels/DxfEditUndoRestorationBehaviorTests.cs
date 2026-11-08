// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditUndoRestorationBehaviorTests
{
    private static readonly string[] _expectedLayerNames = ["alpha", "beta"];

    [AvaloniaFact]
    public async Task UndoMoveToNewLayer_RebuildsLayerChoicesAndRestoresGeometryAndDependentSelectionsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var source = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        vm.SelectedBoundLayerOption = vm.BoundLayerOptions.Single(static option => option.Name == "beta");
        vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(static option => option.Name == "alpha");
        await SelectCadPadsAsync(vm, [source.Id]);
        vm.DxfEditNewLayerName = "edited";
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Contains(vm.LayerToggles, static layer => layer.Name == "edited");
        Assert.Equal("edited", Assert.Single(vm.CadPads, pad => pad.Id == source.Id).Layer);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(_expectedLayerNames, vm.LayerToggles.Select(static layer => layer.Name).Order());
        Assert.DoesNotContain(vm.BoundLayerOptions, static option => option.Name == "edited");
        Assert.DoesNotContain(vm.RegularSourceLayerOptions, static option => option.Name == "edited");
        Assert.Equal("beta", vm.SelectedBoundLayerOption.Name);
        Assert.Equal("alpha", vm.SelectedRegularSourceLayerOption.Name);
        var restored = Assert.Single(vm.CadPads, pad => pad.Id == source.Id);
        Assert.Equal(source.Layer, restored.Layer);
        Assert.Equal(source.Polygon.Vertices, restored.Polygon.Vertices);
        Assert.Equal(0, vm.RelayeredCadPadCount);
        Assert.Empty(vm.SelectedCadPadIds);
    }

    [AvaloniaFact]
    public async Task RestoreAllHidden_AfterReloadWithoutHideHistory_RestoresPadsAndCanUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var saved = await fixture.OpenAsync();
        var hidden = Assert.Single(saved.CadPads, static pad => pad.Layer == "alpha");
        await SelectCadPadsAsync(saved, [hidden.Id]);
        saved.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(saved);
        Assert.True(await saved.SaveProjectAsync(), saved.StatusText);
        var loaded = fixture.CreateViewModel();
        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);
        Assert.Equal(1, loaded.DeletedCadPadCount);
        Assert.DoesNotContain(loaded.CadPads, pad => pad.Id == hidden.Id);
        Assert.False(loaded.CanUndo);

        loaded.RestoreAllHiddenCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(loaded);

        var restored = Assert.Single(loaded.CadPads, pad => pad.Id == hidden.Id);
        Assert.Equal(hidden.Layer, restored.Layer);
        Assert.Equal(hidden.Polygon.Vertices, restored.Polygon.Vertices);
        Assert.Equal(2, loaded.CadPads.Count);
        Assert.Equal(0, loaded.DeletedCadPadCount);
        Assert.True(loaded.HasUnsavedChanges);
        loaded.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(loaded);
        Assert.Equal(1, loaded.DeletedCadPadCount);
        Assert.Equal("beta", Assert.Single(loaded.CadPads).Layer);
    }

    [AvaloniaFact]
    public async Task ExportLayerImage_SavePickerReceivesSafeUnicodeLayerNameAndReturnsCancellationAsync()
    {
        using var fixture = new WorkspaceFixture();
        var path = CreateTempDxfFile(("訊號/Tx", 0, 0, 10, 10));
        try
        {
            var vm = fixture.CreateViewModel();
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            var suggestions = new List<(string Name, string Extension)>();
            vm.PickSaveExportPathAsync = (name, extension) =>
            {
                suggestions.Add((name, extension));
                return Task.FromResult<string?>(null);
            };
            vm.OpenDxfLayerImageExportAsync = async context =>
            {
                var layer = Assert.Single(context.Entries);
                Assert.Equal("訊號/Tx", layer.Name);
                Assert.Null(await context.PickSavePathAsync("dxf_" + layer.Name, "png"));
                return null;
            };

            await vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

            Assert.Equal(("dxf_訊號_Tx", "png"), Assert.Single(suggestions));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
