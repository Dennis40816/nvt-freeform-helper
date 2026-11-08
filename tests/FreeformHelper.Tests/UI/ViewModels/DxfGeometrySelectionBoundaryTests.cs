// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfGeometrySelectionBoundaryTests
{
    [AvaloniaFact]
    public async Task LayerRotation_NormalizesAnglesAndExcludesHiddenSourcesAndSyntheticOutputsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(
            ("signal", 0, 0, 10, 20), ("signal", 40, 0, 50, 20),
            ("signal", 80, 0, 90, 20), ("signal", 88, 0, 98, 20), ("aux", 120, 0, 130, 20));
        var originals = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).ToArray();
        Assert.Equal(5, originals.Length);
        await SelectCadPadsAsync(vm, [originals[1].Id]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [originals[2].Id, originals[3].Id]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var combined = Assert.Single(vm.CadPads, pad => originals.All(original => original.Id != pad.Id));
        Assert.Equal("signal", combined.Layer);
        vm.SelectedDxfEditRotationScopeOption = vm.DxfEditRotationScopeOptions.Single(
            option => option.Value == FreeformHelperViewModel.DxfEditRotationScope.TargetLayer);
        vm.DxfEditRotationLayerName = " SIGNAL ";
        vm.DxfEditRotationDegrees = -270m;
        Assert.True(vm.CanRotateSelectedCadPads);

        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF rotate: rotated 1 CAD pad(s) by 90°.", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(1, vm.RotatedCadPadCount);
        var rotated = Assert.Single(vm.CadPads, pad => pad.Id == originals[0].Id);
        Assert.Equal(-5d, rotated.Bounds.MinX, 6);
        Assert.Equal(5d, rotated.Bounds.MinY, 6);
        Assert.Equal(15d, rotated.Bounds.MaxX, 6);
        Assert.Equal(15d, rotated.Bounds.MaxY, 6);
        Assert.Equal(combined.Polygon.Vertices.ToArray(),
            vm.CadPads.Single(pad => pad.Id == combined.Id).Polygon.Vertices.ToArray());
        Assert.Equal(originals[4].Polygon.Vertices.ToArray(),
            vm.CadPads.Single(pad => pad.Id == originals[4].Id).Polygon.Vertices.ToArray());
        Assert.Equal(3, vm.DeletedCadPadCount);

        vm.DxfEditRotationDegrees = 270m;
        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF rotate: rotated 1 CAD pad(s) by -90°.", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
        Assert.False(vm.HasRotatedCadPads);
        Assert.Equal(originals[0].Polygon.Vertices.ToArray(),
            vm.CadPads.Single(pad => pad.Id == originals[0].Id).Polygon.Vertices.ToArray());
        vm.RestoreAllHiddenCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        await SelectCadPadsAsync(vm, [combined.Id]);
        vm.ClearCombinedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        foreach (var original in originals.Skip(1))
        {
            Assert.Equal(original.Polygon.Vertices.ToArray(),
                vm.CadPads.Single(pad => pad.Id == original.Id).Polygon.Vertices.ToArray());
        }
    }

    [AvaloniaFact]
    public async Task RestoreGeometry_FromRotatedDetails_PreservesMovedLayerAndOtherRotationAndCanUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(("signal", 0, 0, 10, 20), ("signal", 30, 0, 40, 20));
        var originals = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).ToArray();
        await SelectCadPadsAsync(vm, originals.Select(static pad => pad.Id).ToArray());
        vm.DxfEditRotationDegrees = 30m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var rotated = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).ToArray();
        await SelectCadPadsAsync(vm, [originals[0].Id]);
        vm.DxfEditNewLayerName = "edited";
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = changes =>
        {
            opened = changes;
            return Task.CompletedTask;
        };
        await vm.OpenRotatedDxfEditChangeListCommand.ExecuteAsync(null);
        var changes = Assert.IsType<DxfEditChangeListViewModel>(opened);
        Assert.Equal(2, changes.Rows.Count);
        changes.SelectedEntry = Assert.Single(changes.Rows, row => row.CadPadId == originals[0].Id);
        await SelectCadPadsAsync(vm, [originals[0].Id]);
        vm.HasUnsavedChanges = false;

        changes.ApplySelectedEntryCommand.Execute(null);

        Assert.Equal($"DXF edit: restored CAD {originals[0].Id} geometry.", vm.StatusText);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.True(vm.HasUnsavedChanges);
        await WaitForGridRebuildAsync(vm);
        var restored = Assert.Single(vm.CadPads, pad => pad.Id == originals[0].Id);
        Assert.Equal("edited", restored.Layer);
        Assert.Equal(originals[0].Polygon.Vertices.ToArray(), restored.Polygon.Vertices.ToArray());
        Assert.Equal(rotated[1].Polygon.Vertices.ToArray(),
            vm.CadPads.Single(pad => pad.Id == originals[1].Id).Polygon.Vertices.ToArray());
        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Equal(originals[1].Id, Assert.Single(changes.Rows).CadPadId);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        changes.RefreshCommand.Execute(null);

        Assert.Equal(2, changes.Rows.Count);
        Assert.Equal(2, vm.RotatedCadPadCount);
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Equal("edited", vm.CadPads.Single(pad => pad.Id == originals[0].Id).Layer);
        foreach (var expected in rotated)
        {
            Assert.Equal(expected.Polygon.Vertices.ToArray(),
                vm.CadPads.Single(pad => pad.Id == expected.Id).Polygon.Vertices.ToArray());
        }
    }

    private static async Task<FreeformHelperViewModel> OpenWorkspaceAsync(
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var path = CreateTempDxfFile(pads);
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("cov30-settings-", ".json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            RecalcBoundsOnLayerFilter = false,
        };
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        return vm;
    }
}
