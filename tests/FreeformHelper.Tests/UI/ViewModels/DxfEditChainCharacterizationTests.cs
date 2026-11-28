// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditChainCharacterizationTests
{
    [AvaloniaFact]
    public async Task HideUndoRotateSaveLoadExport_PreservesVisiblePadsAndEditsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var original = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        var selected = Assert.Single(original, static pad => pad.Layer == "alpha");
        AssertEditState(vm, original, hidden: 0, rotated: 0, canUndo: false);
        await SelectCadPadsAsync(vm, [selected.Id]);
        Assert.Equal(selected.Id, Assert.Single(vm.SelectedCadPadIds));

        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        AssertEditState(vm, original.Where(pad => pad.Id != selected.Id), hidden: 1, rotated: 0, canUndo: true);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertEditState(vm, original, hidden: 0, rotated: 0, canUndo: false);
        await SelectCadPadsAsync(vm, [selected.Id]);
        vm.DxfEditRotationDegrees = 90m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var edited = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        AssertEditState(vm, edited, hidden: 0, rotated: 1, canUndo: true);
        var rotated = Assert.Single(edited, pad => pad.Id == selected.Id);
        Assert.Equal(20, rotated.Bounds.Width, 6);
        Assert.Equal(10, rotated.Bounds.Height, 6);
        Assert.NotEqual(selected.Polygon.Vertices.ToArray(), rotated.Polygon.Vertices.ToArray());

        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        AssertEditState(vm, edited, hidden: 0, rotated: 1, canUndo: true);
        var project = JsonProjectStore.Load(files.PathFor("project.json"));
        Assert.Empty(project.HiddenCadPadIds);
        Assert.Equal(selected.Id, Assert.Single(project.DxfCadGeometryOverrides).Key);
        var loaded = files.CreateViewModel();
        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);
        AssertEditState(loaded, edited, hidden: 0, rotated: 1, canUndo: false);

        var exportPath = files.PathFor("edited.dxf");
        loaded.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(exportPath);
        await loaded.ExportDxfVisibleCommand.ExecuteAsync(null);
        var exported = new DxfImportService().ImportFromPath(exportPath, new DxfImportOptions());
        Assert.Equal(2, exported.Pads.Count);
        foreach (var pad in edited)
        {
            var output = Assert.Single(exported.Pads, candidate => candidate.Layer == pad.Layer);
            Assert.Equal(pad.Polygon.Vertices.Length, output.Polygon.Vertices.Length);
            foreach (var (expected, actual) in pad.Polygon.Vertices.Zip(output.Polygon.Vertices))
            {
                Assert.Equal(expected.X, actual.X, 6);
                Assert.Equal(expected.Y, actual.Y, 6);
            }
        }

        AssertEditState(loaded, edited, hidden: 0, rotated: 1, canUndo: false);
    }

    private static void AssertEditState(
        FreeformHelperViewModel vm, IEnumerable<CadPad> expected, int hidden, int rotated, bool canUndo)
    {
        var pads = expected.OrderBy(static pad => pad.Id).ToArray();
        Assert.Equal(pads.Select(static pad => (pad.Id, pad.Layer)),
            vm.CadPads.OrderBy(static pad => pad.Id).Select(static pad => (pad.Id, pad.Layer)));
        foreach (var pad in pads)
        {
            Assert.Equal(pad.Polygon.Vertices, Assert.Single(vm.CadPads, candidate => candidate.Id == pad.Id).Polygon.Vertices);
        }

        Assert.Equal(hidden, vm.DeletedCadPadCount);
        Assert.Equal(rotated, vm.RotatedCadPadCount);
        Assert.Equal(canUndo, vm.CanUndo);
        Assert.Equal(canUndo, vm.UndoCommand.CanExecute(null));
        Assert.Empty(vm.SelectedCadPadIds);
    }
}
