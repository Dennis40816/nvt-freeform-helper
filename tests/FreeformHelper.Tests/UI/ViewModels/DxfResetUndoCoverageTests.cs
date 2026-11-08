// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfResetUndoCoverageTests
{
    [AvaloniaFact]
    public async Task ResetAllEdits_WithLayerBoundsRecalculationOff_UndoRestoresEditsAndLastHiddenBatchAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        vm.GridAlignmentMode = GridAlignmentMode.FromCadBounds;
        await WaitForGridRebuildAsync(vm);
        var originalGridTop = vm.RegularPads.Max(static pad => pad.Bounds.MaxY);
        var original = vm.CadPads.OrderBy(static pad => pad.Id).Select(static pad => (pad.Id, pad.Layer, pad.Bounds)).ToArray();
        var firstId = original[0].Id;
        var secondId = original[1].Id;
        vm.ApplyCanvasSelection([firstId], []);
        vm.DxfEditNewLayerName = "edited";
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        vm.ApplyCanvasSelection([secondId], []);
        vm.DxfEditRotationDegrees = 30m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var rotatedBounds = vm.CadPads.Single(pad => pad.Id == secondId).Bounds;
        Assert.NotEqual(original[1].Bounds, rotatedBounds);
        vm.ApplyCanvasSelection([firstId], []);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(secondId, Assert.Single(vm.CadPads).Id);
        var editedGridTop = vm.RegularPads.Max(static pad => pad.Bounds.MaxY);
        Assert.True(editedGridTop > originalGridTop);
        vm.HasUnsavedChanges = false;

        vm.ResetAllDxfEditsCommand.Execute(null);

        Assert.Equal("DXF edit: reset hidden (1), combined (0), layer edits (1), rotated (1).", vm.StatusText);
        Assert.True(vm.HasUnsavedChanges);
        await WaitForGridRebuildAsync(vm);
        Assert.False(vm.HasAnyCadEdits);
        Assert.Equal(original, vm.CadPads.OrderBy(static pad => pad.Id).Select(static pad => (pad.Id, pad.Layer, pad.Bounds)));
        Assert.Equal(originalGridTop, vm.RegularPads.Max(static pad => pad.Bounds.MaxY), 6);

        vm.Undo();
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.Equal(secondId, Assert.Single(vm.CadPads).Id);
        Assert.Equal(rotatedBounds, vm.CadPads[0].Bounds);
        Assert.Equal(editedGridTop, vm.RegularPads.Max(static pad => pad.Bounds.MaxY), 6);

        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal("edited", vm.CadPads.Single(pad => pad.Id == firstId).Layer);
        Assert.Equal(rotatedBounds, vm.CadPads.Single(pad => pad.Id == secondId).Bounds);
    }
}
