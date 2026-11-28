// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfIndexingAnchorBoundaryBehaviorTests
{
    [AvaloniaFact]
    public async Task AnchorRequestsForHiddenCad_PreserveBothIcAnchorsAndCurrentSelectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var hidden = vm.CadPads.Single(pad => pad.Layer == "left-a").Id;
        var leftAnchor = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        var rightAnchor = vm.CadPads.Single(pad => pad.Layer == "right-b").Id;
        vm.SetCadOutputFwDiffIndexAnchorCadPad(rightAnchor);
        vm.SetCadOutputFwDiffIndexAnchorCadPad(leftAnchor);
        vm.LayerToggles.Single(layer => layer.Name == "left-a").IsSelected = false;
        await vm.WaitForGridRebuildIdleAsync();
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == hidden);
        vm.HasUnsavedChanges = false;
        var revision = vm.SimulationWorkspaceSourceRevision;

        vm.SetCadOutputFwDiffIndexAnchorCadPad(hidden);

        Assert.Equal($"CAD Output FW Diff anchor ignored: CAD {hidden} is not visible.", vm.StatusText);
        Assert.False(vm.IsCadOutputFwDiffIndexAnchorCadPad(hidden));
        Assert.Equal((decimal)leftAnchor, vm.CadOutputFwDiffIndexAnchorCadId);

        vm.ClearCadOutputFwDiffIndexAnchorCadPad(hidden);

        Assert.Equal($"CAD Output FW Diff anchor clear ignored: CAD {hidden} is not visible.", vm.StatusText);
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(leftAnchor));
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(rightAnchor));
        Assert.Equal((decimal)leftAnchor, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task SetAlreadySavedAnchorInAnotherIc_KeepsCurrentSelectionAndDoesNotInvalidateAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var left = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        var right = vm.CadPads.Single(pad => pad.Layer == "right-b").Id;
        vm.SetCadOutputFwDiffIndexAnchorCadPad(right);
        vm.SetCadOutputFwDiffIndexAnchorCadPad(left);
        vm.HasUnsavedChanges = false;
        var revision = vm.SimulationWorkspaceSourceRevision;
        var status = vm.StatusText;

        vm.SetCadOutputFwDiffIndexAnchorCadPad(right);

        Assert.Equal((decimal)left, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(left));
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(right));
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task ClearAnchorViaSiblingCad_RemovesOnlyItsIcAndRepeatedClearDoesNotInvalidateAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var left = vm.CadPads.Single(pad => pad.Layer == "left-a").Id;
        var leftAnchor = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        var rightAnchor = vm.CadPads.Single(pad => pad.Layer == "right-b").Id;
        vm.SetCadOutputFwDiffIndexAnchorCadPad(leftAnchor);
        vm.SetCadOutputFwDiffIndexAnchorCadPad(rightAnchor);
        vm.HasUnsavedChanges = false;
        var revision = vm.SimulationWorkspaceSourceRevision;

        vm.ClearCadOutputFwDiffIndexAnchorCadPad(left);

        Assert.False(vm.IsCadOutputFwDiffIndexAnchorCadPad(leftAnchor));
        Assert.Equal(rightAnchor, Assert.Single(vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas));
        Assert.Equal((decimal)rightAnchor, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.Equal("CAD Output FW Diff anchor cleared: IC 1 uses scan-order first CAD.", vm.StatusText);
        Assert.True(vm.SimulationWorkspaceSourceRevision > revision);
        Assert.True(vm.HasUnsavedChanges);
        vm.HasUnsavedChanges = false;
        revision = vm.SimulationWorkspaceSourceRevision;

        vm.ClearCadOutputFwDiffIndexAnchorCadPad(leftAnchor);

        Assert.Equal("CAD Output FW Diff anchor clear: IC 1 has no anchor.", vm.StatusText);
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(rightAnchor));
        Assert.Equal((decimal)rightAnchor, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        Assert.False(vm.HasUnsavedChanges);
    }
}
