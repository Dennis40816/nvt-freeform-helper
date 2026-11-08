// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadEditUndoBehaviorTests
{
    [AvaloniaFact]
    public async Task SetCadPadCustomValues_MixedExistingAndNewValues_UndoRestoresEachPriorValueAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var ids = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        Assert.Equal(2, ids.Length);
        Assert.Equal(0d, vm.GetCadPadCustomValue(ids[0]));
        Assert.Equal(0d, vm.GetCadPadCustomValue(ids[1]));
        vm.SetCadPadCustomValues([ids[0]], 1.25m);
        Assert.Equal($"CAD pad #{ids[0]} custom value set to 1.25.", vm.StatusText);
        vm.HasUnsavedChanges = false;

        vm.SetCadPadCustomValues([], 99m);

        Assert.Equal($"CAD pad #{ids[0]} custom value set to 1.25.", vm.StatusText);
        Assert.False(vm.HasUnsavedChanges);

        vm.SetCadPadCustomValues(ids, 7.5m);

        Assert.Equal("CAD pads updated: 2 items.", vm.StatusText);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(7.5, vm.GetCadPadCustomValue(ids[0]));
        Assert.Equal(7.5, vm.GetCadPadCustomValue(ids[1]));

        vm.UndoCommand.Execute(null);

        Assert.Equal(1.25, vm.GetCadPadCustomValue(ids[0]));
        Assert.Equal(0d, vm.GetCadPadCustomValue(ids[1]));
        vm.UndoCommand.Execute(null);
        Assert.Equal(0d, vm.GetCadPadCustomValue(ids[0]));
        Assert.Equal(0d, vm.GetCadPadCustomValue(ids[1]));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task ResetSizingForCells_GlobalDimensions_RestoresAutomaticGridAndUndoRestoresManualSizesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGridAsync(fixture);
        var originalBounds = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var selected = Assert.Single(vm.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        vm.ApplyCanvasSelection([], [selected.Index]);
        vm.ApplySizingToCells([0], [0], 6m, 7m);
        Assert.Equal("Manual sizing applied.", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
        var manualBounds = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var resized = Assert.Single(vm.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        Assert.Equal(6d, resized.Bounds.Width, 6);
        Assert.Equal(7d, resized.Bounds.Height, 6);
        Assert.Equal(selected.Index, Assert.Single(vm.SelectedRegularPadIndices));
        vm.HasUnsavedChanges = false;

        vm.ResetSizingForCells([0], [0]);

        Assert.Equal("Manual sizing reset to default.", vm.StatusText);
        Assert.True(vm.HasUnsavedChanges);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(originalBounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.Equal(selected.Index, Assert.Single(vm.SelectedRegularPadIndices));

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(manualBounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(originalBounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
    }

    [AvaloniaFact]
    public async Task ManualSizing_InvalidRequests_DoNotChangeGeometryOrAddUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var unloaded = fixture.CreateViewModel();
        unloaded.HasUnsavedChanges = false;

        unloaded.ApplySizingToCells([0], [0], 6m, 7m);

        Assert.Equal("Manual sizing: build grid first.", unloaded.StatusText);
        unloaded.ResetSizingForCells([0], [0]);
        Assert.Equal("Manual sizing: build grid first.", unloaded.StatusText);
        Assert.False(unloaded.CanUndo);
        Assert.False(unloaded.HasUnsavedChanges);
        var vm = await OpenGridAsync(fixture);
        var before = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var status = vm.StatusText;
        vm.HasUnsavedChanges = false;
        var canUndoBefore = vm.CanUndo;

        vm.ApplySizingToCells([0], [0], null, null);
        vm.ApplySizingToCells([0], [0], -1m, 0m);

        Assert.Equal(status, vm.StatusText);
        Assert.Equal(canUndoBefore, vm.CanUndo);

        vm.ApplySizingToCells([0], [0], 6m, 21m);

        Assert.Equal("Manual sizing: Manual sizes exceed available total.", vm.StatusText);
        Assert.Equal(canUndoBefore, vm.CanUndo);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(before, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());

        vm.ApplySizingToCells([0], [0], 6m, null);

        Assert.Equal("Manual sizing applied.", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
        var resized = Assert.Single(vm.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        Assert.Equal(6d, resized.Bounds.Width, 6);
        Assert.Equal(10d, resized.Bounds.Height, 6);

        vm.ApplySizingToCells([0], [0], null, 8m);
        await WaitForGridRebuildAsync(vm);

        var heightOnly = Assert.Single(vm.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        Assert.Equal(6d, heightOnly.Bounds.Width, 6);
        Assert.Equal(8d, heightOnly.Bounds.Height, 6);
    }

    private static async Task<FreeformHelperViewModel> OpenGridAsync(WorkspaceFixture fixture)
    {
        var vm = fixture.CreateViewModel();
        vm.ActiveAreaWidth = 40m;
        vm.ActiveAreaHeight = 20m;
        vm.GridPaddingPercent = 0m;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.IsWidthRowLocal = false;
        vm.IsHeightColumnLocal = false;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 2;
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(4, vm.RegularPads.Count);
        return vm;
    }
}
