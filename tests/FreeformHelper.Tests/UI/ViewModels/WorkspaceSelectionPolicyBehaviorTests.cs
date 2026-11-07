// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceSelectionPolicyBehaviorTests
{
    [AvaloniaFact]
    public async Task SoloLayerSelection_FiltersCadClearsMatchesAndUndoRestoresMixedVisibilityAsync()
    {
        var vm = await OpenWorkspaceAsync();
        var alpha = vm.LayerToggles.Single(toggle => toggle.Name == "alpha");
        var beta = vm.LayerToggles.Single(toggle => toggle.Name == "beta");
        vm.ApplyLayerSelection(beta, soloSelect: false);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result);
        vm.HasUnsavedChanges = false;

        vm.ApplyLayerSelection(beta, soloSelect: true);

        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
        Assert.False(alpha.IsSelected);
        Assert.True(beta.IsSelected);
        Assert.False(vm.LayerToggles.Single(toggle => toggle.Name == "gamma").IsSelected);
        Assert.Empty(vm.MatchLinksForCanvas);
        Assert.All(vm.RegularPads, pad => Assert.Null(pad.MatchedCadPadId));
        Assert.False(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.True(vm.HasUnsavedChanges);
        vm.Undo();
        Assert.Equal(["alpha", "gamma"], vm.CadPads.Select(pad => pad.Layer).Order(StringComparer.Ordinal));
        Assert.True(alpha.IsSelected);
        Assert.False(beta.IsSelected);
        Assert.True(vm.LayerToggles.Single(toggle => toggle.Name == "gamma").IsSelected);
    }

    [AvaloniaFact]
    public async Task ToggleLastLayerOff_LeavesNoCadOrMatchesAndUndoRestoresOnlyThatLayerAsync()
    {
        var vm = await OpenWorkspaceAsync();
        var alpha = vm.LayerToggles.Single(toggle => toggle.Name == "alpha");
        vm.ApplyLayerSelection(alpha, soloSelect: true);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        vm.HasUnsavedChanges = false;

        vm.ApplyLayerSelection(alpha, soloSelect: false);

        Assert.Empty(vm.CadPads);
        Assert.All(vm.LayerToggles, toggle => Assert.False(toggle.IsSelected));
        Assert.True(vm.AreNoCadLayersSelected);
        Assert.False(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.Empty(vm.MatchLinksForCanvas);
        Assert.True(vm.HasUnsavedChanges);
        vm.Undo();
        Assert.Equal("alpha", Assert.Single(vm.CadPads).Layer);
        Assert.True(alpha.IsSelected);
        Assert.All(vm.LayerToggles.Where(toggle => toggle != alpha), toggle => Assert.False(toggle.IsSelected));
    }

    [AvaloniaFact]
    public async Task LayerSelection_WhenPendingEditsRejectChange_PreservesCadMatchesAndCleanStateAsync()
    {
        var vm = await OpenWorkspaceAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        var pads = vm.CadPads.ToArray();
        var links = vm.MatchLinksForCanvas.ToArray();
        Assert.NotEmpty(links);
        vm.HasUnsavedChanges = false;
        vm.EnsureNoPendingEdits = () => false;
        var alpha = vm.LayerToggles.Single(toggle => toggle.Name == "alpha");

        vm.ApplyLayerSelection(alpha, soloSelect: true);
        vm.ApplyLayerSelection(alpha, soloSelect: false);

        Assert.Equal(pads, vm.CadPads);
        Assert.Equal(links, vm.MatchLinksForCanvas);
        Assert.True(vm.AreAllCadLayersSelected);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void HighlightCadGroup_KeepsDistinctVisibleTargetsAndReplacesRegularSelection()
    {
        var vm = CreateSelectionWorkspace();
        vm.ApplyCanvasSelection([1], [10]);

        vm.HighlightCadPadGroup([2, 999, 2, 1], "Owners");

        Assert.Equal([1, 2], vm.SelectedCadPadIds.Order());
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Equal("Owners: 2 pad(s).", vm.StatusText);
        vm.HighlightCadPadGroup([999]);
        Assert.Equal([1, 2], vm.SelectedCadPadIds.Order());
        Assert.Equal("Owner CAD highlight skipped: targets are not visible.", vm.StatusText);
    }

    [AvaloniaFact]
    public void HighlightRegularGroup_UsesPadIdentityAndPreservesSelectionWhenNoTargetsAreVisible()
    {
        var vm = CreateSelectionWorkspace();
        vm.ApplyCanvasSelection([1], []);

        vm.HighlightRegularPadGroup([20, 999, 20, 10]);

        Assert.Equal([10, 20], vm.SelectedRegularPadIndices.Order());
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal("Regular target highlighted: 2 pad(s).", vm.StatusText);
        vm.HighlightRegularPadGroup([]);
        Assert.Equal([10, 20], vm.SelectedRegularPadIndices.Order());
        Assert.Equal("Regular target highlight skipped: no target ids.", vm.StatusText);
        vm.HighlightRegularPadGroup([999]);
        Assert.Equal([10, 20], vm.SelectedRegularPadIndices.Order());
        Assert.Equal("Regular target highlight skipped: targets are not visible.", vm.StatusText);
    }

    private static async Task<FreeformHelperViewModel> OpenWorkspaceAsync()
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("alpha", 0, 0, 10, 10), ("beta", 10, 0, 20, 10), ("gamma", 20, 0, 30, 10));
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("settings-", ".json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            RecalcBoundsOnLayerFilter = false,
        };
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        Assert.Equal(3, vm.CadPads.Count);
        var grid = TestGeometryFactory.CreateRegularGrid(1, 3, cellWidth: 10, cellHeight: 10);
        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;
        vm.RegularPads = new ObservableCollection<RegularPad>(grid.Pads);
        vm.InvalidateWorkflowDataSnapshot();
        return vm;
    }

    private static FreeformHelperViewModel CreateSelectionWorkspace() => new()
    {
        CadPads = new ObservableCollection<CadPad>
        {
            TestGeometryFactory.CreateCadPad(1, "alpha", 0, 0, 10, 10),
            TestGeometryFactory.CreateCadPad(2, "beta", 10, 0, 20, 10),
        },
        RegularPads = new ObservableCollection<RegularPad>
        {
            TestGeometryFactory.CreateRegularPad(0, 0, 10, 0, 0, 10, 10),
            TestGeometryFactory.CreateRegularPad(0, 1, 20, 10, 0, 20, 10),
        },
    };
}
