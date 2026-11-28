// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerSelectionCallerBehaviorTests
{
    [AvaloniaFact]
    public async Task BulkLayerCheckbox_RepeatedValues_DoNotPromptDirtyOrAddUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var checks = 0;
        vm.EnsureNoPendingEdits = () =>
        {
            checks++;
            return true;
        };
        vm.HasUnsavedChanges = false;

        vm.IsCadLayerBulkToggleChecked = true;

        Assert.Equal(0, checks);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        vm.IsCadLayerBulkToggleChecked = false;
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(1, checks);
        Assert.Empty(vm.CadPads);
        vm.HasUnsavedChanges = false;

        vm.IsCadLayerBulkToggleChecked = false;
        vm.DeselectAllLayers();

        Assert.Equal(2, checks);
        Assert.True(vm.AreNoCadLayersSelected);
        Assert.Equal("All off", vm.CadLayerBulkToggleText);
        Assert.Equal("Layer visibility is not fully on. Click to show every layer.", vm.CadLayerBulkToggleToolTip);
        Assert.False(vm.HasUnsavedChanges);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.True(vm.AreAllCadLayersSelected);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task BulkLayerCommands_PendingEditsRejected_PreserveVisibilityAndPreviousUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var beta = vm.LayerToggles.Single(static toggle => toggle.Name == "beta");
        vm.ApplyLayerSelection(beta, soloSelect: false);
        await WaitForGridRebuildAsync(vm);
        var visiblePad = Assert.Single(vm.CadPads);
        vm.HasUnsavedChanges = false;
        var checks = 0;
        vm.EnsureNoPendingEdits = () =>
        {
            checks++;
            return false;
        };

        vm.SelectAllLayers();

        Assert.Equal(1, checks);
        Assert.Same(visiblePad, Assert.Single(vm.CadPads));
        Assert.False(beta.IsSelected);
        Assert.True(vm.AreSomeCadLayersSelected);
        Assert.Equal("Partly on", vm.CadLayerBulkToggleText);
        Assert.False(vm.HasUnsavedChanges);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.True(vm.AreAllCadLayersSelected);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.False(vm.CanUndo);
        var visiblePads = vm.CadPads.ToArray();
        vm.HasUnsavedChanges = false;

        vm.DeselectAllLayers();

        Assert.Equal(2, checks);
        Assert.Equal(visiblePads, vm.CadPads);
        Assert.All(vm.LayerToggles, static toggle => Assert.True(toggle.IsSelected));
        Assert.True(vm.AreAllCadLayersSelected);
        Assert.Equal("All on", vm.CadLayerBulkToggleText);
        Assert.Equal("All layers are visible. Click to hide every layer.", vm.CadLayerBulkToggleToolTip);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task RegularSourceLayerToggle_RevertsVisibilityWithoutAddingUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 1;
        cascade.YChannels = 1;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(static option => option.Name == "alpha");
        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        await WaitForGridRebuildAsync(vm);
        var source = vm.LayerToggles.Single(static toggle => toggle.Name == "alpha");
        var beta = vm.LayerToggles.Single(static toggle => toggle.Name == "beta");
        Assert.False(source.IsSelected);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
        var regular = Assert.Single(vm.RegularPads);
        beta.IsSelected = false;
        Assert.Empty(vm.CadPads);

        source.IsSelected = true;

        Assert.False(source.IsSelected);
        Assert.True(vm.AreNoCadLayersSelected);
        Assert.Empty(vm.CadPads);
        Assert.Same(regular, Assert.Single(vm.RegularPads));
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.False(source.IsSelected);
        Assert.True(beta.IsSelected);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
        Assert.Equal("alpha", vm.SelectedRegularSourceLayerOption.Name);
    }
}
