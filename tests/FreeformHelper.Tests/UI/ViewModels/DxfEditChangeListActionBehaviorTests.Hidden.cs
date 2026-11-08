// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

public sealed partial class DxfEditChangeListActionBehaviorTests
{
    [AvaloniaFact]
    public async Task RestoreHiddenRow_RestoresOnlyThatPadAndUndoHidesItAgainAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var originals = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        Assert.Equal(2, originals.Length);
        await SelectCadPadsAsync(vm, [originals[0].Id]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [originals[1].Id]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var changes = await OpenChangesAsync(vm, DxfEditChangeKind.Hidden);
        var target = Assert.Single(changes.Rows, row => row.CadPadId == originals[0].Id);
        changes.SelectEntryCommand.Execute(target);
        Assert.False(changes.FocusSelectedEntryCommand.CanExecute(null));

        changes.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        var restored = Assert.Single(vm.CadPads);
        Assert.Equal(originals[0].Id, restored.Id);
        Assert.Equal(originals[0].Layer, restored.Layer);
        Assert.Equal(originals[0].Polygon.Vertices.ToArray(), restored.Polygon.Vertices.ToArray());
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(originals[1].Id, Assert.Single(changes.Rows).CadPadId);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        changes.RefreshCommand.Execute(null);

        Assert.Empty(vm.CadPads);
        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.Equal(originals.Select(static pad => pad.Id), changes.Rows.Select(static row => row.CadPadId));
    }

    [AvaloniaFact]
    public async Task ClearGroup_FromHiddenSourceRow_PreservesOtherGroupAndManualHideAndCanUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var path = CreateTempDxfFile(
            ("signal", 0, 0, 10, 20), ("signal", 8, 0, 18, 20),
            ("signal", 40, 0, 50, 20), ("signal", 48, 0, 58, 20), ("signal", 80, 0, 90, 20));
        try
        {
            var vm = fixture.CreateViewModel();
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var ids = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).Select(static pad => pad.Id).ToArray();
            Assert.Equal(5, ids.Length);
            await SelectCadPadsAsync(vm, [ids[0], ids[1]]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [ids[2], ids[3]]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [ids[4]]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            var combinedIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
            Assert.Equal(2, combinedIds.Length);
            var remainingOutputId = Assert.Single(vm.CadPads, static pad => pad.Bounds.MinX > 30).Id;
            var changes = await OpenChangesAsync(vm, DxfEditChangeKind.Hidden);
            var source = Assert.Single(changes.Rows, row => row.CadPadId == ids[0]);
            Assert.Equal("Hidden by combine", source.StatusText);
            Assert.Equal("Clear group", source.ActionLabel);
            Assert.False(source.CanFocus);
            changes.SelectEntryCommand.Execute(source);

            changes.ApplySelectedEntryCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);

            Assert.Equal(1, vm.CombinedCadPadCount);
            Assert.Equal(3, vm.DeletedCadPadCount);
            Assert.Equal(new[] { ids[0], ids[1], remainingOutputId }.Order(),
                vm.CadPads.Select(static pad => pad.Id).Order());
            Assert.Equal(ids.Skip(2).Order(), changes.Rows.Select(static row => row.CadPadId).Order());
            changes.SetCombinedKindCommand.Execute(null);
            var remaining = Assert.Single(changes.Rows);
            Assert.Equal(remainingOutputId, remaining.CadPadId);
            Assert.True(remaining.CanApply);

            vm.UndoCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            changes.RefreshCommand.Execute(null);
            changes.SetHiddenKindCommand.Execute(null);

            Assert.Equal(combinedIds, vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
            Assert.Equal(2, vm.CombinedCadPadCount);
            Assert.Equal(5, vm.DeletedCadPadCount);
            Assert.Equal(ids.Order(), changes.Rows.Select(static row => row.CadPadId).Order());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
