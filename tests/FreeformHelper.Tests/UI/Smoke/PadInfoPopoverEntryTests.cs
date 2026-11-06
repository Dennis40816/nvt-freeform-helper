// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoPopoverEntryTests
{
    [AvaloniaFact]
    public async Task OutsideClickWithPendingSizeEditShowsCloseConfirmationAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = CreateWorkspace();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var context = await OpenDirtyPadInfoAsync(vm, view);

            PressOutsidePopover(view);
            await HeadlessUiPump.FlushQueueAsync();

            AssertPendingEditPreserved(vm, view, context);
            Assert.True(GetPopover(view).IsCloseConfirmVisible);
            Assert.False(GetCanvas(view).IsHitTestVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task InsideClickDismissesCloseConfirmationAndPreservesPendingSizeEditAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = CreateWorkspace();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var context = await OpenDirtyPadInfoAsync(vm, view);
            PressOutsidePopover(view);
            await HeadlessUiPump.FlushQueueAsync();
            var popover = GetPopover(view);
            Assert.True(popover.IsCloseConfirmVisible);
            var dismissCount = 0;
            void OnCloseConfirmDismissed(object? sender, EventArgs e) => dismissCount++;
            popover.CloseConfirmDismissed += OnCloseConfirmDismissed;
            var scroll = Assert.Single(popover.GetVisualDescendants().OfType<ScrollViewer>(),
                static control => control.Name == "RegularPadInfoScroll");

            PressPointer(scroll, new Point(1, 1));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(1, dismissCount);
            Assert.False(popover.IsCloseConfirmVisible);
            AssertPendingEditPreserved(vm, view, context);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SelectionChangeWithPendingSizeEditShowsCloseConfirmationAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = CreateWorkspace();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var context = await OpenDirtyPadInfoAsync(vm, view);
            Assert.Equal(42, Assert.Single(vm.SelectedRegularPadIndices));

            vm.ApplyCanvasSelection([], []);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Empty(vm.SelectedRegularPadIndices);
            Assert.Empty(vm.InteractionState.SelectedRegularIndices);
            AssertPendingEditPreserved(vm, view, context);
            Assert.True(GetPopover(view).IsCloseConfirmVisible);
            Assert.False(GetCanvas(view).IsHitTestVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static FreeformHelperViewModel CreateWorkspace()
    {
        var pad = new RegularPad(0, 0, 42, new Polygon2(
        [
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 8),
            new Point2(0, 8),
        ]))
        {
            IcIndex = 0,
            DiffIndex = 42,
        };
        return new FreeformHelperViewModel
        {
            RegularPads = [pad],
            ShowRegular = true,
        };
    }

    private static async Task<PadInfoContext> OpenDirtyPadInfoAsync(
        FreeformHelperViewModel vm, FreeformHelperView view)
    {
        var canvas = GetCanvas(view);
        var pad = Assert.Single(vm.RegularPads);
        Assert.True(canvas.IsHitTestVisible);
        PressPointer(canvas, canvas.WorldToScreen(pad.Centroid), rightButton: true);
        await HeadlessUiPump.FlushQueueAsync();

        var context = Assert.IsType<PadInfoContext>(vm.InteractionState.ActivePadInfo);
        var info = Assert.IsType<RegularPadInfoViewModel>(context.ViewModel);
        Assert.Same(info, GetPopover(view).DataContext);
        Assert.False(GetPopover(view).IsCloseConfirmVisible);
        Assert.False(info.HasPendingChanges);
        Assert.Equal(10m, info.PadWidth);
        info.PadWidth = 12m;
        await HeadlessUiPump.FlushQueueAsync();

        AssertPendingEditPreserved(vm, view, context);
        Assert.False(canvas.IsHitTestVisible);
        return context;
    }

    private static void AssertPendingEditPreserved(
        FreeformHelperViewModel vm, FreeformHelperView view, PadInfoContext context)
    {
        Assert.Same(context, vm.InteractionState.ActivePadInfo);
        var info = Assert.IsType<RegularPadInfoViewModel>(context.ViewModel);
        Assert.Same(info, GetPopover(view).DataContext);
        Assert.True(Assert.IsType<Canvas>(view.FindControl<Canvas>("PadInfoOverlay")).IsVisible);
        Assert.True(info.HasPendingChanges);
        Assert.Equal(12m, info.PadWidth);
        Assert.Equal(10d, Assert.Single(vm.RegularPads).Bounds.Width);
    }

    private static PadInfoPopover GetPopover(FreeformHelperView view)
        => Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));

    private static PadCanvas GetCanvas(FreeformHelperView view)
        => Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));

    private static void PressOutsidePopover(FreeformHelperView view)
        => PressPointer(Assert.IsType<Grid>(view.FindControl<Grid>("LayoutGrid")), new Point(1, 1));

    private static void PressPointer(Control target, Point position, bool rightButton = false)
    {
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var root = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(target));
        var rootPosition = Assert.IsType<Point>(target.TranslatePoint(position, root));
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, root, rootPosition, 0,
            new PointerPointProperties(
                rightButton ? RawInputModifiers.RightMouseButton : RawInputModifiers.LeftMouseButton,
                rightButton ? PointerUpdateKind.RightButtonPressed : PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount: 1));
    }
}
