// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoPopoverApplyDiscardEntryTests
{
    [AvaloniaFact]
    public async Task ApplyButtonAppliesPendingSizeAndClosesPopoverAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var originalWidth = vm.RegularPads[0].Bounds.Width;
            var targetWidth = (decimal)originalWidth + 1m;
            var info = await OpenPadInfoAsync(view, vm);
            var popover = GetPopover(view);
            var canvas = GetCanvas(view);
            info.PadWidth = targetWidth;
            Assert.True(info.HasPendingChanges);
            Assert.False(canvas.IsHitTestVisible);
            await ShowCloseConfirmationAsync(view, window, popover);
            Assert.Same(info, vm.InteractionState.ActivePadInfo?.ViewModel);

            UiEventProbe.Click(FindButton(popover, "Apply"));
            await HeadlessUiPump.WaitUntilAsync(
                () => Math.Abs(vm.RegularPads[0].Bounds.Width - (double)targetWidth) < 0.001d,
                "Applying the pending pad width did not update the grid.");
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal((double)targetWidth, vm.RegularPads[0].Bounds.Width, 2);
            Assert.False(info.HasPendingChanges);
            AssertPopoverClosed(view, vm, canvas, popover);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DiscardButtonRestoresPendingSizeAndClosesPopoverAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var originalWidth = vm.RegularPads[0].Bounds.Width;
            var info = await OpenPadInfoAsync(view, vm);
            var originalPadWidth = info.PadWidth;
            var popover = GetPopover(view);
            var canvas = GetCanvas(view);
            info.PadWidth = originalPadWidth + 1m;
            Assert.True(info.HasPendingChanges);
            Assert.False(canvas.IsHitTestVisible);
            await ShowCloseConfirmationAsync(view, window, popover);
            Assert.Same(info, vm.InteractionState.ActivePadInfo?.ViewModel);

            UiEventProbe.Click(FindButton(popover, "Discard"));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(originalWidth, vm.RegularPads[0].Bounds.Width, 2);
            Assert.Equal(originalPadWidth, info.PadWidth);
            Assert.False(info.HasPendingChanges);
            AssertPopoverClosed(view, vm, canvas, popover);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<RegularPadInfoViewModel> OpenPadInfoAsync(
        FreeformHelperView view, FreeformHelperViewModel vm)
    {
        var pad = vm.RegularPads[0];
        // Leave dismissal to the popover button so its close event is exercised.
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
        await HeadlessUiPump.FlushQueueAsync();

        var popover = GetPopover(view);
        Assert.Same(info, popover.DataContext);
        Assert.True(popover.IsEffectivelyVisible);
        Assert.False(popover.IsCloseConfirmVisible);
        Assert.True(GetCanvas(view).IsHitTestVisible);
        return info;
    }

    private static PadInfoPopover GetPopover(FreeformHelperView view)
        => Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));

    private static PadCanvas GetCanvas(FreeformHelperView view)
        => Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));

    private static Button FindButton(PadInfoPopover popover, string content)
    {
        var button = Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
            candidate => candidate.Content is string text && text == content);
        Assert.True(button.IsEffectivelyVisible);
        return button;
    }

    private static async Task ShowCloseConfirmationAsync(
        FreeformHelperView view, Window window, PadInfoPopover popover)
    {
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        view.RaiseEvent(new PointerPressedEventArgs(view, pointer, window, new Point(0, 0), 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount: 1));
        await HeadlessUiPump.FlushQueueAsync();

        Assert.True(popover.IsCloseConfirmVisible);
        Assert.True(popover.IsEffectivelyVisible);
        Assert.False(GetCanvas(view).IsHitTestVisible);
    }

    private static void AssertPopoverClosed(
        FreeformHelperView view, FreeformHelperViewModel vm, PadCanvas canvas, PadInfoPopover popover)
    {
        Assert.Null(vm.InteractionState.ActivePadInfo);
        Assert.False(Assert.IsType<Canvas>(view.FindControl<Canvas>("PadInfoOverlay")).IsVisible);
        Assert.Null(popover.DataContext);
        Assert.False(popover.IsCloseConfirmVisible);
        Assert.True(canvas.IsHitTestVisible);
    }
}
