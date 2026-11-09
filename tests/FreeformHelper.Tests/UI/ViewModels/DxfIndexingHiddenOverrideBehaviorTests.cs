// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfIndexingHiddenOverrideBehaviorTests
{
    [AvaloniaFact]
    public async Task SetVisibleOverride_WithHiddenCollision_PreservesHiddenOverrideAndUndoRestoresVisibleValueAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var hidden = vm.CadPads.Single(pad => pad.Layer == "left-a").Id;
        var visible = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        vm.SetCadOutputFwDiffIndexOverride(hidden, 41);
        vm.SetCadOutputFwDiffIndexOverride(visible, 42);
        vm.LayerToggles.Single(layer => layer.Name == "left-a").IsSelected = false;
        await vm.WaitForGridRebuildIdleAsync();
        Assert.False(vm.TryGetCadOutputFwDiffIndex(hidden, out _));

        vm.SetCadOutputFwDiffIndexOverride(visible, 41);

        Assert.Equal(41, vm.GetCadOutputFwDiffIndexOverride(hidden));
        Assert.Equal(41, vm.GetCadOutputFwDiffIndexOverride(visible));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(visible, out var assigned));
        Assert.Equal(41, assigned);
        Assert.Equal(visible, Assert.Single(vm.CadOutputFwDiffIndexOverrideCadIds));
        Assert.Equal($"CAD Output FW Diff override saved: IC 1, CAD {visible} -> 41.", vm.StatusText);

        vm.UndoCommand.Execute(null);

        Assert.Equal(41, vm.GetCadOutputFwDiffIndexOverride(hidden));
        Assert.Equal(42, vm.GetCadOutputFwDiffIndexOverride(visible));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(visible, out var restored));
        Assert.Equal(42, restored);
    }
}
