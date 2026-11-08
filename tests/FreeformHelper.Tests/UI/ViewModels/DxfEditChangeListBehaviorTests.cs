// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditChangeListBehaviorTests
{
    [AvaloniaFact]
    public async Task ApplyMovedEntry_RestoresOnlyItsLayerPreservesRotationAndCanUndo()
    {
        using var culture = TestCulture.UseEnUs();
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 30, 0, 40, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var ids = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
            await SelectCadPadsAsync(vm, [ids[0]]);
            vm.DxfEditRotationDegrees = 90m;
            vm.RotateSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, ids);
            var rotatedBounds = vm.CadPads.Single(pad => pad.Id == ids[0]).Bounds;
            vm.DxfEditTargetLayerName = "edited";
            vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            DxfEditChangeListViewModel? opened = null;
            vm.OpenDxfEditChangeListAsync = list =>
            {
                opened = list;
                return Task.CompletedTask;
            };
            await vm.OpenMovedDxfEditChangeListCommand.ExecuteAsync(null);
            var changes = Assert.IsType<DxfEditChangeListViewModel>(opened);
            changes.SelectedEntry = Assert.Single(changes.Rows, row => row.CadPadId == ids[0]);
            changes.FocusSelectedEntryCommand.Execute(null);
            Assert.Equal(ids[0], Assert.Single(vm.SelectedCadPadIds));

            changes.ApplySelectedEntryCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);

            var restored = Assert.Single(vm.CadPads, pad => pad.Id == ids[0]);
            Assert.Equal("signal", restored.Layer);
            Assert.Equal(rotatedBounds, restored.Bounds);
            Assert.Equal("edited", Assert.Single(vm.CadPads, pad => pad.Id == ids[1]).Layer);
            Assert.Equal(1, vm.RotatedCadPadCount);
            Assert.Empty(vm.SelectedCadPadIds);
            Assert.Equal(ids[1], Assert.Single(changes.Rows).CadPadId);

            vm.UndoCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            changes.RefreshCommand.Execute(null);
            Assert.Equal(2, changes.Rows.Count);
            Assert.All(vm.CadPads, pad => Assert.Equal("edited", pad.Layer));
            Assert.Equal(rotatedBounds, vm.CadPads.Single(pad => pad.Id == ids[0]).Bounds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task ApplyHiddenSourceEntry_ClearsItsCombineGroupAndKeepsManualHideIndependent()
    {
        using var culture = TestCulture.UseEnUs();
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 8, 0, 18, 20), ("signal", 30, 0, 40, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var ids = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
            await SelectCadPadsAsync(vm, [ids[0], ids[1]]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            var combinedId = Assert.Single(vm.CadPads, pad => pad.Id != ids[2]).Id;
            await SelectCadPadsAsync(vm, [ids[2]]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            DxfEditChangeListViewModel? opened = null;
            vm.OpenDxfEditChangeListAsync = list =>
            {
                opened = list;
                return Task.CompletedTask;
            };
            await vm.OpenHiddenDxfEditChangeListCommand.ExecuteAsync(null);
            var changes = Assert.IsType<DxfEditChangeListViewModel>(opened);
            changes.SelectedEntry = Assert.Single(changes.Rows, row => row.CadPadId == ids[0]);
            Assert.Equal("Clear group", changes.SelectedEntry.ActionLabel);

            changes.ApplySelectedEntryCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);

            Assert.Equal(0, vm.CombinedCadPadCount);
            Assert.Equal(new[] { ids[0], ids[1] }, vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
            Assert.DoesNotContain(vm.CadPads, pad => pad.Id == combinedId);
            var manualHide = Assert.Single(changes.Rows);
            Assert.Equal(ids[2], manualHide.CadPadId);
            Assert.Equal("Restore", manualHide.ActionLabel);

            changes.ApplySelectedEntryCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            Assert.Equal(ids, vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
            Assert.Empty(changes.Rows);
            vm.UndoCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            Assert.Equal(new[] { ids[0], ids[1] }, vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
