// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class DxfEditBoundaryBehaviorTests
{
    private static readonly string[] _expectedLayerNames = ["alpha", "beta"];

    [AvaloniaFact]
    public async Task CombineSelectedCadPads_HiddenSelectionLeavesOneVisiblePad_RejectsWithoutAddingUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenPadsAsync(fixture, ("signal", 0, 0, 10, 10), ("signal", 8, 0, 18, 10));
        var originals = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        var ids = originals.Select(static pad => pad.Id).ToArray();
        await SelectCadPadsAsync(vm, [ids[1]]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, ids);
        vm.HasUnsavedChanges = false;

        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF combine: select at least 2 CAD pads.", vm.StatusText);
        Assert.False(vm.HasCombinedCadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(ids[0], Assert.Single(vm.CadPads).Id);
        Assert.Equal(ids, vm.SelectedCadPadIds.Order().ToArray());
        Assert.False(vm.HasUnsavedChanges);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.False(vm.CanUndo);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(ids, vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
        foreach (var original in originals)
        {
            Assert.Equal(original.Polygon.Vertices.ToArray(),
                vm.CadPads.Single(pad => pad.Id == original.Id).Polygon.Vertices.ToArray());
        }
    }

    [AvaloniaFact]
    public async Task ClearCombinedCadPads_OnlyOrdinaryPadSelected_PreservesGroupAndUndoHistoryAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenPadsAsync(fixture,
            ("signal", 0, 0, 10, 10), ("signal", 8, 0, 18, 10), ("signal", 40, 0, 50, 10));
        var ids = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).Select(static pad => pad.Id).ToArray();
        await SelectCadPadsAsync(vm, [ids[0], ids[1]]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [ids[2]]);
        var combined = Assert.Single(vm.CadPads, pad => pad.Id != ids[2]);
        vm.HasUnsavedChanges = false;

        vm.ClearCombinedCadPadsCommand.Execute(null);

        Assert.Equal("DXF combine: select combined CAD pads first.", vm.StatusText);
        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal(combined.Polygon.Vertices.ToArray(),
            vm.CadPads.Single(pad => pad.Id == combined.Id).Polygon.Vertices.ToArray());
        Assert.Equal(ids[2], Assert.Single(vm.SelectedCadPadIds));
        Assert.False(vm.HasUnsavedChanges);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.False(vm.CanUndo);
        Assert.False(vm.HasCombinedCadPads);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(ids.Order().ToArray(), vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
    }

    [AvaloniaFact]
    public async Task LayerMove_MissingSelectionOrBlankNames_LeavesPadsSelectionAndUndoUnchangedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var unloaded = fixture.CreateViewModel();
        unloaded.DxfEditTargetLayerName = "beta";
        unloaded.MoveSelectedCadPadsToLayerCommand.Execute(null);
        Assert.Equal("DXF edit: import DXF first.", unloaded.StatusText);
        Assert.False(unloaded.CanUndo);
        var vm = await fixture.OpenAsync();
        var originals = vm.CadPads.ToArray();
        vm.HasUnsavedChanges = false;
        vm.DxfEditTargetLayerName = "beta";

        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);

        Assert.Equal("DXF edit: select CAD pads first.", vm.StatusText);
        Assert.False(vm.CanUndo);
        await SelectCadPadsAsync(vm, [originals[0].Id]);
        vm.DxfEditTargetLayerName = " \t ";
        Assert.False(vm.CanMoveSelectedCadPadsToLayer);

        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);

        Assert.Equal("DXF edit: select target layer first.", vm.StatusText);
        vm.DxfEditNewLayerName = " \t ";
        Assert.False(vm.CanCreateLayerAndMoveSelectedCadPads);

        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF edit: input new layer name first.", vm.StatusText);
        Assert.Equal(originals[0].Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(0, vm.RelayeredCadPadCount);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        Assert.Equal(_expectedLayerNames, vm.LayerToggles.Select(static layer => layer.Name).Order().ToArray());
        foreach (var original in originals)
        {
            var current = Assert.Single(vm.CadPads, pad => pad.Id == original.Id);
            Assert.Equal(original.Layer, current.Layer);
            Assert.Equal(original.Polygon.Vertices.ToArray(), current.Polygon.Vertices.ToArray());
        }
    }
}
