// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoCloseInspectorRefreshTests
{
    private const int OverrideIndex = 7;

    [AvaloniaFact]
    public async Task ClosingPadInfo_ShowsEditedValuesInInspectorAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var window = new Window { Content = new FreeformHelperView { DataContext = vm }, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var cadId = vm.CadPads[0].Id;
            vm.ApplyCanvasSelection([cadId], []);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Null(vm.PadInspectorUi.Snapshot?.Cad?.CadOutputFwDiffOverride);
            OpenDummyPadInfo(vm);
            await HeadlessUiPump.FlushQueueAsync();

            vm.SetCadOutputFwDiffIndexOverride(cadId, OverrideIndex);
            vm.InteractionState.ClosePadInfo();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Null(vm.InteractionState.ActivePadInfo);
            Assert.Equal(cadId, vm.PadInspectorUi.Snapshot?.Cad?.CadPadId);
            Assert.Equal(OverrideIndex, vm.PadInspectorUi.Snapshot?.Cad?.CadOutputFwDiffOverride);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ClosingPadInfo_WithMultiSelection_ShowsOnlyTheSummaryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var window = new Window { Content = new FreeformHelperView { DataContext = vm }, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var ids = vm.CadPads.Take(2).Select(static pad => pad.Id).ToArray();
            vm.ApplyCanvasSelection(ids, []);
            await HeadlessUiPump.FlushQueueAsync();
            // A right click on one pad of the range puts that pad into the Inspector while Pad Info is open.
            vm.PadInspectorUi.Snapshot = vm.BuildCadPadInspectorSnapshot(ids[0]);
            OpenDummyPadInfo(vm);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.NotNull(vm.PadInspectorUi.Snapshot);

            vm.InteractionState.ClosePadInfo();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Null(vm.PadInspectorUi.Snapshot);
            Assert.True(vm.HasPadInspectorMultiSelection);
            Assert.Equal("2 pads selected", vm.PadInspectorSelectionSummaryText);
        }
        finally
        {
            window.Close();
        }
    }

    private static void OpenDummyPadInfo(FreeformHelperViewModel vm)
    {
        var pad = vm.RegularPads[0];
        var info = new RegularPadInfoViewModel(
            pads: [pad],
            toDisplayRow: vm.ToDisplayRow,
            getDxfIndex: null,
            getMatchedCadPadIds: null,
            getMatchedCadLinks: null,
            applyPadSize: vm.ApplySizingToCells,
            resetPadSize: null,
            setFreeform: null,
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: null,
            focusRegularMatches: null,
            highlightRegularMatches: null,
            closePadInfo: null);
        vm.InteractionState.OpenPadInfo(new PadInfoContext(info, pad.Bounds, [pad.Bounds]));
    }
}
