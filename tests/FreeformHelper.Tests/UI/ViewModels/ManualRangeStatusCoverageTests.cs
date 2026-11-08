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
public sealed class ManualRangeStatusCoverageTests
{
    [AvaloniaFact]
    public async Task ManualRange_InvalidRowsWithValidColumn_PreservesGeometryAndSelectionUntilCorrectedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGridAsync(fixture);
        var selected = Assert.Single(vm.RegularPads, pad => vm.ToDisplayRow(pad.Row) == 1 && pad.Col == 0);
        vm.ApplyCanvasSelection([], [selected.Index]);
        var bounds = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        vm.HasUnsavedChanges = false;

        vm.ManualRowsRange = "999999";

        Assert.Equal("Rows: invalid.", vm.ManualRangeStatus);
        Assert.NotEmpty(vm.ManualRangeError);
        Assert.Equal(selected.Index, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(bounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.False(vm.HasUnsavedChanges);

        vm.PendingColumnWidth = 0m;
        vm.PendingRowHeight = 0m;
        vm.ManualRowsRange = "1";
        vm.ManualColsRange = "0";

        Assert.Empty(vm.ManualRangeError);
        Assert.Equal("Manual range: Rows 1, Cols 0.", vm.ManualRangeStatus);
        Assert.Equal(selected.Index, Assert.Single(vm.SelectedRegularPadIndices));
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(bounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.False(vm.HasUnsavedChanges);
    }

    internal static async Task<FreeformHelperViewModel> OpenGridAsync(WorkspaceFixture fixture)
    {
        var vm = fixture.CreateViewModel();
        vm.ActiveAreaWidth = 40m;
        vm.ActiveAreaHeight = 20m;
        vm.PanelBiasX = 0m;
        vm.PanelBiasY = 0m;
        vm.GridPaddingPercent = 0m;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.SelectedScanOrder = ScanOrder.LeftToRight_TopToBottom;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2m;
        cascade.YChannels = 2m;
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(4, vm.RegularPads.Count);
        Assert.Equal(2, vm.CadPads.Count);
        return vm;
    }
}
