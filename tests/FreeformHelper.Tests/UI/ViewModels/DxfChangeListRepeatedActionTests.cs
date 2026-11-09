// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class DxfChangeListRepeatedActionTests
{
    [AvaloniaTheory]
    [InlineData(DxfEditChangeKind.Hidden)]
    [InlineData(DxfEditChangeKind.Combined)]
    public async Task BulkClearCombineRows_FromSourcesOrOutputs_ClearsGroupOnceAndUndoRestoresItAsync(DxfEditChangeKind kind)
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var originals = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        await SelectCadPadsAsync(vm, originals.Select(static pad => pad.Id).ToArray());
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var outputIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        Assert.Equal(2, outputIds.Length);
        var changes = await OpenChangesAsync(vm, kind);
        Assert.Equal(2, changes.Rows.Count);
        changes.AreAllVisibleRowsSelected = true;

        changes.ApplySelectedEntriesCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(changes.Rows);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Equal(originals.Select(static pad => (pad.Id, pad.Layer, pad.Bounds)),
            vm.CadPads.OrderBy(static pad => pad.Id).Select(static pad => (pad.Id, pad.Layer, pad.Bounds)));

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        changes.RefreshCommand.Execute(null);

        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.Equal(2, vm.CombinedCadPadCount);
        Assert.Equal(outputIds, vm.CadPads.Select(static pad => pad.Id).Order().ToArray());
        Assert.Equal(2, changes.Rows.Count);
    }

    [AvaloniaFact]
    public async Task RestoreLayer_FromSecondListAfterRestoration_PreservesSelectionStatusAndUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var original = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha");
        await SelectCadPadsAsync(vm, [original.Id]);
        vm.DxfEditTargetLayerName = "beta";
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var first = await OpenChangesAsync(vm, DxfEditChangeKind.Moved);
        var second = await OpenChangesAsync(vm, DxfEditChangeKind.Moved);
        Assert.Equal(original.Id, Assert.Single(first.Rows).CadPadId);
        Assert.Equal(original.Id, Assert.Single(second.Rows).CadPadId);
        first.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal("alpha", vm.CadPads.Single(pad => pad.Id == original.Id).Layer);
        var regularId = vm.RegularPads[0].RegularPadId;
        vm.ApplyCanvasSelection([], [regularId]);
        vm.HasUnsavedChanges = false;
        var status = vm.StatusText;
        var revision = vm.SimulationWorkspaceSourceRevision;

        second.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(second.Rows);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        Assert.Equal(regularId, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(original.Polygon.Vertices.ToArray(),
            vm.CadPads.Single(pad => pad.Id == original.Id).Polygon.Vertices.ToArray());

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal("beta", vm.CadPads.Single(pad => pad.Id == original.Id).Layer);
        Assert.Equal(1, vm.RelayeredCadPadCount);
    }
}
