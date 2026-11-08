// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfGeometryStaleRestoreBehaviorTests
{
    [AvaloniaFact]
    public async Task RestoreGeometryFromASecondOpenListAfterRestorationLeavesStateAndUndoUnchangedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        vm.RecalcBoundsOnLayerFilter = false;
        var original = Assert.Single(vm.CadPads);
        await SelectCadPadsAsync(vm, [original.Id]);
        vm.DxfEditRotationDegrees = 90m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var rotated = Assert.Single(vm.CadPads);
        Assert.Equal(1, vm.RotatedCadPadCount);
        var lists = new List<DxfEditChangeListViewModel>();
        vm.OpenDxfEditChangeListAsync = changes =>
        {
            lists.Add(changes);
            return Task.CompletedTask;
        };
        await vm.OpenRotatedDxfEditChangeListCommand.ExecuteAsync(null);
        await vm.OpenRotatedDxfEditChangeListCommand.ExecuteAsync(null);
        Assert.Equal(2, lists.Count);
        Assert.Equal(original.Id, Assert.Single(lists[0].Rows).CadPadId);
        Assert.Equal(original.Id, Assert.Single(lists[1].Rows).CadPadId);
        lists[0].ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.False(vm.HasRotatedCadPads);
        var regularId = vm.RegularPads[0].RegularPadId;
        vm.ApplyCanvasSelection([], [regularId]);
        vm.HasUnsavedChanges = false;
        var status = vm.StatusText;
        var revision = vm.SimulationWorkspaceSourceRevision;

        lists[1].ApplySelectedEntryCommand.Execute(null);

        Assert.Empty(lists[1].Rows);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        Assert.Equal(regularId, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(original.Polygon.Vertices.ToArray(), Assert.Single(vm.CadPads).Polygon.Vertices.ToArray());

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.Equal(rotated.Polygon.Vertices.ToArray(), Assert.Single(vm.CadPads).Polygon.Vertices.ToArray());
    }
}
