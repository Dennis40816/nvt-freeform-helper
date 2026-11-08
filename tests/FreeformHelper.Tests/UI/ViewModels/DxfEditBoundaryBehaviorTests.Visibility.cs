// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

public sealed partial class DxfEditBoundaryBehaviorTests
{
    [AvaloniaFact]
    public async Task RestoreLastDeletedCadPads_ClearedHiddenCombine_SkipsStaleBatchAndRestoresEarlierHideAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenPadsAsync(fixture,
            ("signal", 0, 0, 10, 10), ("signal", 20, 0, 30, 10), ("signal", 28, 0, 38, 10));
        var originals = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        var ids = originals.Select(static pad => pad.Id).ToArray();
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        Assert.Equal("DXF edit: select CAD pads first.", vm.StatusText);
        Assert.False(vm.CanUndo);
        await SelectCadPadsAsync(vm, [ids[0]]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [ids[1], ids[2]]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var combinedId = Assert.Single(vm.CadPads).Id;
        await SelectCadPadsAsync(vm, [combinedId]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = list =>
        {
            opened = list;
            return Task.CompletedTask;
        };
        await vm.OpenHiddenDxfEditChangeListCommand.ExecuteAsync(null);
        var changes = Assert.IsType<DxfEditChangeListViewModel>(opened);
        changes.SelectedEntry = Assert.Single(changes.Rows, row => row.CadPadId == ids[1]);
        changes.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.False(vm.HasCombinedCadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(ids.Skip(1).ToArray(), vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
        await SelectCadPadsAsync(vm, [ids[1]]);

        vm.RestoreLastDeletedCadPadsCommand.Execute(null);

        Assert.Equal("DXF edit: restored 1 CAD pad(s).", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(ids, vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
        Assert.Empty(vm.SelectedCadPadIds);
        vm.HasUnsavedChanges = false;

        vm.RestoreLastDeletedCadPadsCommand.Execute(null);

        Assert.Equal("DXF edit: no deleted CAD pads to restore.", vm.StatusText);
        Assert.False(vm.HasUnsavedChanges);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(ids.Skip(1).ToArray(), vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
        Assert.Equal(originals[2].Polygon.Vertices.ToArray(),
            vm.CadPads.Single(pad => pad.Id == ids[2]).Polygon.Vertices.ToArray());
    }

    [AvaloniaFact]
    public async Task RehideDuplicate_FromDetails_RemovesOnlyDuplicateAndUndoRestoresItsGeometryAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenPadsAsync(fixture,
            ("signal", 0, 0, 10, 10), ("signal", 0, 0, 10, 10), ("aux", 20, 0, 30, 10));
        var canonical = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal");
        var other = Assert.Single(vm.CadPads, static pad => pad.Layer == "aux");
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = list =>
        {
            opened = list;
            return Task.CompletedTask;
        };
        await vm.OpenDuplicateDxfEditChangeListCommand.ExecuteAsync(null);
        var changes = Assert.IsType<DxfEditChangeListViewModel>(opened);
        var duplicateId = Assert.Single(changes.Rows).CadPadId;
        changes.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var restored = Assert.Single(vm.CadPads, pad => pad.Id == duplicateId);
        Assert.Equal("Hide again", Assert.Single(changes.Rows).ActionLabel);
        await SelectCadPadsAsync(vm, [duplicateId]);
        vm.HasUnsavedChanges = false;

        changes.ApplySelectedEntryCommand.Execute(null);

        Assert.Equal("DXF duplicate: auto-hidden 1 CAD pad(s) again.", vm.StatusText);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Empty(vm.SelectedCadPadIds);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(new[] { canonical.Id, other.Id }.Order().ToArray(),
            vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(1, vm.DuplicateCadPadCount);
        Assert.Equal("Auto-hidden duplicate", Assert.Single(changes.Rows).StatusText);
        Assert.Equal("Restore", Assert.Single(changes.Rows).ActionLabel);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        changes.RefreshCommand.Execute(null);

        Assert.Equal(3, vm.CadPads.Count);
        var undone = Assert.Single(vm.CadPads, pad => pad.Id == duplicateId);
        Assert.Equal(restored.Layer, undone.Layer);
        Assert.Equal(restored.Polygon.Vertices.ToArray(), undone.Polygon.Vertices.ToArray());
        Assert.Equal("Restored duplicate", Assert.Single(changes.Rows).StatusText);
        Assert.Equal("Hide again", Assert.Single(changes.Rows).ActionLabel);
    }

    private static async Task<FreeformHelperViewModel> OpenPadsAsync(WorkspaceFixture fixture,
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var path = CreateTempDxfFile(pads);
        var vm = fixture.CreateViewModel();
        vm.RecalcBoundsOnLayerFilter = false;
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        return vm;
    }
}
