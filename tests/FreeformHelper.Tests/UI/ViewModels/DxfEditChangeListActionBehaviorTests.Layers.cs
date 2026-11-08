// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class DxfEditChangeListActionBehaviorTests
{
    [AvaloniaFact]
    public async Task RestoreLayer_FromMovedRow_PreservesGeometryAndOtherMovesAndCanUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var firstId = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id;
        var secondId = Assert.Single(vm.CadPads, static pad => pad.Layer == "beta").Id;
        await SelectCadPadsAsync(vm, [firstId]);
        vm.DxfEditRotationDegrees = 30m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var rotated = Assert.Single(vm.CadPads, pad => pad.Id == firstId);
        await SelectCadPadsAsync(vm, [firstId]);
        vm.DxfEditTargetLayerName = "beta";
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        await SelectCadPadsAsync(vm, [secondId]);
        vm.DxfEditTargetLayerName = "gamma";
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var changes = await OpenChangesAsync(vm, DxfEditChangeKind.Moved);
        changes.SelectEntryCommand.Execute(Assert.Single(changes.Rows, row => row.CadPadId == firstId));
        Assert.Equal(2, changes.MovedCount);
        await SelectCadPadsAsync(vm, [firstId]);

        changes.ApplySelectedEntryCommand.Execute(null);

        Assert.Equal($"DXF edit: restored CAD {firstId} to layer 'alpha'.", vm.StatusText);
        Assert.Empty(vm.SelectedCadPadIds);
        await WaitForGridRebuildAsync(vm);
        var restored = Assert.Single(vm.CadPads, pad => pad.Id == firstId);
        Assert.Equal("alpha", restored.Layer);
        Assert.Equal(rotated.Polygon.Vertices.ToArray(), restored.Polygon.Vertices.ToArray());
        Assert.Equal("gamma", Assert.Single(vm.CadPads, pad => pad.Id == secondId).Layer);
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.Equal(secondId, Assert.Single(changes.Rows).CadPadId);
        Assert.True(vm.LayerToggles.Single(static layer => layer.Name == "alpha").IsSelected);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        changes.RefreshCommand.Execute(null);

        var undone = Assert.Single(vm.CadPads, pad => pad.Id == firstId);
        Assert.Equal("beta", undone.Layer);
        Assert.Equal(rotated.Polygon.Vertices.ToArray(), undone.Polygon.Vertices.ToArray());
        Assert.Equal("gamma", Assert.Single(vm.CadPads, pad => pad.Id == secondId).Layer);
        Assert.Equal(2, changes.MovedCount);
    }

    [AvaloniaFact]
    public async Task RestoreLayer_FromHiddenMovedRow_RestoresLayerWithoutUnhidingPadAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var firstId = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id;
        var otherId = Assert.Single(vm.CadPads, static pad => pad.Layer == "beta").Id;
        await SelectCadPadsAsync(vm, [firstId]);
        vm.DxfEditTargetLayerName = "beta";
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        await SelectCadPadsAsync(vm, [firstId]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var changes = await OpenChangesAsync(vm, DxfEditChangeKind.Moved);
        var moved = Assert.Single(changes.Rows);
        Assert.Equal(firstId, moved.CadPadId);
        Assert.False(moved.CanFocus);
        Assert.False(changes.FocusSelectedEntryCommand.CanExecute(null));

        changes.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(changes.Rows);
        Assert.Equal(0, vm.RelayeredCadPadCount);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(otherId, Assert.Single(vm.CadPads).Id);
        changes.SetHiddenKindCommand.Execute(null);
        var hidden = Assert.Single(changes.Rows);
        Assert.Equal(firstId, hidden.CadPadId);
        Assert.Equal("Layer alpha", hidden.CurrentLayerText);
        Assert.Equal("Restore", hidden.ActionLabel);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        changes.RefreshCommand.Execute(null);
        changes.SetMovedKindCommand.Execute(null);

        Assert.Equal("Layer beta", Assert.Single(changes.Rows).CurrentLayerText);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(otherId, Assert.Single(vm.CadPads).Id);
    }

    private static async Task<DxfEditChangeListViewModel> OpenChangesAsync(
        FreeformHelperViewModel vm, DxfEditChangeKind kind)
    {
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = changes =>
        {
            opened = changes;
            return Task.CompletedTask;
        };
        var command = kind switch
        {
            DxfEditChangeKind.Hidden => vm.OpenHiddenDxfEditChangeListCommand,
            DxfEditChangeKind.Moved => vm.OpenMovedDxfEditChangeListCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        await command.ExecuteAsync(null);
        var result = Assert.IsType<DxfEditChangeListViewModel>(opened);
        Assert.Equal(kind, result.SelectedKind);
        return result;
    }
}
