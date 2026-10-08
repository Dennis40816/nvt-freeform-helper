using System.Reflection;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{


    [Fact]
    public void DeleteSelectedCadPadsCommand_WhenDxfMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        vm.DeleteSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF edit: import DXF first.", vm.StatusText);
    }


    [Fact]
    public void RestoreLastDeletedCadPadsCommand_WhenDxfMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        vm.RestoreLastDeletedCadPadsCommand.Execute(null);

        Assert.Equal("DXF edit: import DXF first.", vm.StatusText);
    }


    [Fact]
    public void RestoreAllHiddenCadPadsCommand_WhenDxfMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        vm.RestoreAllHiddenCadPadsCommand.Execute(null);

        Assert.Equal("DXF edit: import DXF first.", vm.StatusText);
    }


    [Fact]
    public void CombineSelectedCadPadsCommand_WhenDxfMissing_SetsStatus()
    {
        var vm = new FreeformHelperViewModel();

        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF combine: import DXF first.", vm.StatusText);
    }


    [Fact]
    public void Undo_RevertsDisplayToggleChange()
    {
        var vm = new FreeformHelperViewModel();
        var original = vm.ShowCad;

        vm.ShowCad = !original;

        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);

        Assert.Equal(original, vm.ShowCad);
        LifecycleCharacterization.AssertUndoSequence();
    }


    [Fact]
    public async Task CombineSelectedCadPadsCommand_CreatesSyntheticPadAndCanClear()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 8, 0, 18, 10),
            CreateRectCad(3, 40, 0, 48, 8),
        };
        var selectedCadIds = new[] { 1, 2 };

        InitializeDxfEditCadState(vm, source);

        await SelectCadPadsAsync(vm, selectedCadIds);
        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.True(vm.HasCombinedCadPads);
        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.True(vm.DeletedCadPadCount >= 2);
        await WaitForGridRebuildAsync(vm);

        var combinedCadId = Assert.Single(GetCombinedCadPadIds(vm));
        await SelectCadPadsAsync(vm, [combinedCadId]);

        vm.ClearCombinedCadPadsCommand.Execute(null);

        Assert.False(vm.HasCombinedCadPads);
        Assert.Equal(0, vm.CombinedCadPadCount);
    }


    [Fact]
    public async Task DeleteSelectedCadPadsCommand_CanUndoToRestoreHiddenPads()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 12, 0, 22, 10),
        };
        var selectedCadIds = new[] { 1 };

        InitializeDxfEditCadState(vm, source);

        await SelectCadPadsAsync(vm, selectedCadIds);
        vm.DeleteSelectedCadPadsCommand.Execute(null);

        Assert.True(vm.HasDeletedCadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.True(vm.CanUndo);

        vm.UndoCommand.Execute(null);

        Assert.False(vm.HasDeletedCadPads);
        Assert.Equal(0, vm.DeletedCadPadCount);
    }


    [Fact]
    public async Task ClearCombinedCadPadsCommand_CanUndoBackToCombinedState()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 8, 0, 18, 10),
            CreateRectCad(3, 40, 0, 48, 8),
        };
        var selectedCadIds = new[] { 1, 2 };

        InitializeDxfEditCadState(vm, source);

        await SelectCadPadsAsync(vm, selectedCadIds);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        Assert.True(vm.HasCombinedCadPads);

        var combinedCadId = Assert.Single(GetCombinedCadPadIds(vm));
        await SelectCadPadsAsync(vm, [combinedCadId]);
        vm.ClearCombinedCadPadsCommand.Execute(null);
        Assert.False(vm.HasCombinedCadPads);
        Assert.True(vm.CanUndo);

        vm.UndoCommand.Execute(null);

        Assert.True(vm.HasCombinedCadPads);
        Assert.Equal(1, vm.CombinedCadPadCount);
    }


    [Fact]
    public async Task ClearCombinedCadPadsCommand_OnlyClearsSelectedCombinedGroups()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 8, 0, 18, 10),
            CreateRectCad(3, 40, 0, 50, 10),
            CreateRectCad(4, 48, 0, 58, 10),
        };

        InitializeDxfEditCadState(vm, source);

        await SelectCadPadsAsync(vm, [1, 2]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [3, 4]);
        vm.CombineSelectedCadPadsCommand.Execute(null);

        var combinedIds = GetCombinedCadPadIds(vm).OrderBy(static id => id).ToArray();
        Assert.Equal(2, combinedIds.Length);
        Assert.Equal(2, vm.CombinedCadPadCount);
        Assert.Equal(4, vm.DeletedCadPadCount);

        await SelectCadPadsAsync(vm, [combinedIds[0]]);
        Assert.True(vm.HasSelectedCombinedCadPadsForDxfEdit);

        vm.ClearCombinedCadPadsCommand.Execute(null);

        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.Contains(combinedIds[1], GetCombinedCadPadIds(vm));
        Assert.DoesNotContain(combinedIds[0], GetCombinedCadPadIds(vm));
    }


    [Fact]
    public async Task CombineSelectedCadPadsCommand_WhenDisjoint_CreatesMultipleSyntheticPads()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 30, 0, 40, 10),
        };
        var selectedCadIds = new[] { 1, 2 };

        InitializeDxfEditCadState(vm, source);

        await SelectCadPadsAsync(vm, selectedCadIds);
        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.True(vm.HasCombinedCadPads);
        Assert.Equal(2, vm.CombinedCadPadCount);
        Assert.True(vm.HasDeletedCadPads);
    }


    [Fact]
    public void LayerSelectionAggregateStates_FollowAllOnOffCommands()
    {
        var vm = new FreeformHelperViewModel();
        var cad = new CadPadSet(new[]
        {
            CreateCad(1, "L1", 0, 0, 10, 10),
            CreateCad(2, "L2", 12, 0, 22, 10),
        });

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var updateLayerTogglesMethod = typeof(FreeformHelperViewModel).GetMethod("UpdateLayerToggles", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(updateLayerTogglesMethod);

        cadField!.SetValue(vm, cad);
        updateLayerTogglesMethod!.Invoke(vm, new object?[] { cad });

        Assert.True(vm.AreAllCadLayersSelected);
        Assert.False(vm.AreNoCadLayersSelected);
        Assert.False(vm.AreSomeCadLayersSelected);
        Assert.False(vm.IsCadLayersOffSegmentActive);
        Assert.True(vm.IsCadLayerBulkToggleChecked);
        Assert.Equal("All on", vm.CadLayerBulkToggleText);

        vm.IsCadLayerBulkToggleChecked = false;

        Assert.False(vm.AreAllCadLayersSelected);
        Assert.True(vm.AreNoCadLayersSelected);
        Assert.False(vm.AreSomeCadLayersSelected);
        Assert.True(vm.IsCadLayersOffSegmentActive);
        Assert.False(vm.IsCadLayerBulkToggleChecked);
        Assert.Equal("All off", vm.CadLayerBulkToggleText);

        vm.LayerToggles[0].IsSelected = true;

        Assert.False(vm.AreAllCadLayersSelected);
        Assert.False(vm.AreNoCadLayersSelected);
        Assert.True(vm.AreSomeCadLayersSelected);
        Assert.True(vm.IsCadLayersOffSegmentActive);
        Assert.False(vm.IsCadLayerBulkToggleChecked);
        Assert.Equal("All off", vm.CadLayerBulkToggleText);

        vm.IsCadLayerBulkToggleChecked = true;

        Assert.True(vm.AreAllCadLayersSelected);
        Assert.False(vm.AreNoCadLayersSelected);
        Assert.False(vm.AreSomeCadLayersSelected);
        Assert.False(vm.IsCadLayersOffSegmentActive);
        Assert.True(vm.IsCadLayerBulkToggleChecked);
        Assert.Equal("All on", vm.CadLayerBulkToggleText);
    }


    [Fact]
    public async Task RestoreAllHiddenCadPadsCommand_PreservesRegularAndBoundsLayerSelections()
    {
        var vm = new FreeformHelperViewModel
        {
            RegularSourceMode = RegularSourceMode.FromDxfLayer,
        };
        var cad = new CadPadSet(new[]
        {
            CreateCad(1, "signal", 0, 0, 10, 10),
            CreateCad(2, "regular", 12, 0, 22, 10),
        });

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var updateLayerTogglesMethod = typeof(FreeformHelperViewModel).GetMethod("UpdateLayerToggles", BindingFlags.Instance | BindingFlags.NonPublic);
        var resetBaselineMethod = typeof(FreeformHelperViewModel).GetMethod("ResetDxfEditBaseline", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(updateLayerTogglesMethod);
        Assert.NotNull(resetBaselineMethod);

        cadField!.SetValue(vm, cad);
        resetBaselineMethod!.Invoke(vm, new object?[] { cad });
        updateLayerTogglesMethod!.Invoke(vm, new object?[] { cad });
        vm.SelectedRegularSourceLayerOption = new FreeformHelperViewModel.RegularSourceLayerOption("regular", "regular");
        vm.SelectedBoundLayerOption = new FreeformHelperViewModel.BoundLayerOption("signal", "signal");

        await SelectCadPadsAsync(vm, SingleCadSelection);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        vm.RestoreAllHiddenCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal("regular", vm.SelectedRegularSourceLayerOption.Name);
        Assert.Equal("signal", vm.SelectedBoundLayerOption.Name);
    }


    [Fact]
    public async Task RestoreAllHiddenCadPadsCommand_RestoresManualHiddenPadsWithoutClearingCombinedOrLayerEdits()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateCad(1, "signal", 0, 0, 10, 10),
            CreateCad(2, "signal", 12, 0, 22, 10),
            CreateCad(3, "signal", 20, 0, 30, 10),
        };
        InitializeDxfEditCadState(vm, source);

        vm.DxfEditTargetLayerName = "moved";
        await SelectCadPadsAsync(vm, [1]);
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        await SelectCadPadsAsync(vm, [1]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [2, 3]);
        vm.CombineSelectedCadPadsCommand.Execute(null);

        var combinedCadId = Assert.Single(GetCombinedCadPadIds(vm));
        Assert.True(vm.HasManualHiddenCadPads);
        Assert.True(vm.HasCombinedCadPads);
        Assert.Equal(3, vm.DeletedCadPadCount);
        Assert.Equal(1, vm.RelayeredCadPadCount);

        vm.RestoreAllHiddenCadPadsCommand.Execute(null);

        Assert.False(vm.HasManualHiddenCadPads);
        Assert.True(vm.HasCombinedCadPads);
        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Contains(combinedCadId, GetCombinedCadPadIds(vm));

        var currentCad = GetCurrentCadPadSet(vm);
        var restoredPad = Assert.Single(currentCad.Pads, pad => pad.Id == 1);
        Assert.Equal("moved", restoredPad.Layer);
    }
}
