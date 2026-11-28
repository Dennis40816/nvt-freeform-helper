// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Vector = Avalonia.Vector;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoViewportHeightTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TinyCanvasKeepsConfirmButtonsInsidePanelAndWindowWithScrollableContentAsync(bool regular)
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var window = workspace.Window;
        workspace.Helper.ShowCad = !regular;
        workspace.Helper.ShowRegular = regular;
        await HeadlessUiPump.FlushQueueAsync();
        var view = Assert.Single(window.GetVisualDescendants().OfType<FreeformHelperView>());
        var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
        var centroid = regular ? workspace.Helper.RegularPads[0].Centroid : workspace.Helper.CadPads[0].Centroid;
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var position = Avalonia.VisualExtensions.TranslatePoint(canvas, canvas.WorldToScreen(centroid), window)!.Value;
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount: 2));
        await HeadlessUiPump.FlushQueueAsync();
        var popover = Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));
        Assert.True(popover.IsEffectivelyVisible);
        if (regular)
            Assert.IsType<RegularPadInfoViewModel>(popover.DataContext).PadWidth += 1;
        var grid = Assert.IsType<Grid>(view.FindControl<Grid>("CanvasGrid"));
        grid.RowDefinitions[0].Height = new GridLength(100);
        grid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.InRange(canvas.Bounds.Height, 99, 101);
        if (regular) workspace.Helper.ClearSelectionCommand.Execute(null);
        else popover.ShowCloseConfirm();
        await HeadlessUiPump.FlushQueueAsync();

        var confirm = Assert.Single(popover.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("padInfoConfirm"));
        Assert.True(confirm.IsEffectivelyVisible);
        var buttons = confirm.GetVisualDescendants().OfType<Button>().ToArray();
        Assert.Contains(buttons, static button => button.Content is "Apply");
        Assert.Contains(buttons, static button => button.Content is "Discard");
        Assert.All(buttons, button =>
        {
            Assert.True(button.IsEffectivelyVisible);
            Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0);
            AssertFullyInside(button, popover);
            AssertFullyInside(button, window);
        });
        var scroll = Assert.Single(popover.GetVisualDescendants().OfType<ScrollViewer>(), static scroll =>
            scroll.Name is "CadPadInfoScroll" or "RegularPadInfoScroll");
        Assert.True(scroll.Viewport.Height > 0);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        var contentBottom = Avalonia.VisualExtensions.TranslatePoint(scroll, new Point(0, scroll.Bounds.Height), popover)!.Value;
        var footer = Avalonia.VisualExtensions.TranslatePoint(confirm, default, popover)!.Value;
        Assert.True(contentBottom.Y <= footer.Y);
        scroll.Offset = new Vector(0, scroll.Extent.Height);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(scroll.Offset.Y > 0);
    }

    private static void AssertFullyInside(Control child, Control parent)
    {
        var origin = Avalonia.VisualExtensions.TranslatePoint(child, default, parent)!.Value;
        var bounds = new Rect(origin, child.Bounds.Size);
        Assert.True(new Rect(parent.Bounds.Size).Contains(bounds),
            $"{child} bounds {bounds} exceed {parent} bounds {parent.Bounds.Size}.");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SmallViewportScrollsAboveConfirmBarAndGrowsOnResizeAsync(bool regular)
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var window = workspace.Window;
        window.RequestedThemeVariant = ThemeVariant.Dark;
        window.Width = 1366;
        window.Height = 768;
        workspace.Helper.ShowCad = !regular;
        workspace.Helper.ShowRegular = regular;
        await HeadlessUiPump.FlushQueueAsync();
        var view = Assert.Single(window.GetVisualDescendants().OfType<FreeformHelperView>());
        var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
        var centroid = regular ? workspace.Helper.RegularPads[0].Centroid : workspace.Helper.CadPads[0].Centroid;
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var position = Avalonia.VisualExtensions.TranslatePoint(canvas, canvas.WorldToScreen(centroid), window)!.Value;
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount: 2));
        await HeadlessUiPump.FlushQueueAsync();
        var popover = Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));
        Assert.True(popover.IsEffectivelyVisible);
        if (regular)
        {
            var info = Assert.IsType<RegularPadInfoViewModel>(popover.DataContext);
            info.PadWidth += 1;
            workspace.Helper.ClearSelectionCommand.Execute(null);
        }
        else popover.ShowCloseConfirm();
        foreach (var panel in popover.GetVisualDescendants().OfType<CollapsiblePanel>().ToArray())
            panel.IsExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();
        var scroll = Assert.Single(popover.GetVisualDescendants().OfType<ScrollViewer>(), static scroll =>
            scroll.Name is "CadPadInfoScroll" or "RegularPadInfoScroll");
        AssertInsideCanvas(canvas, popover, scroll);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        scroll.Offset = new Vector(0, scroll.Extent.Height);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(scroll.Offset.Y > 0);
        var smallHeight = popover.Bounds.Height;
        var smallViewport = scroll.Viewport.Height;
        window.Width = 1920;
        window.Height = 1080;
        await HeadlessUiPump.FlushQueueAsync();
        AssertInsideCanvas(canvas, popover, scroll);
        Assert.True(popover.Bounds.Height > smallHeight, $"Panel stayed at {smallHeight} after resize.");
        Assert.True(scroll.Viewport.Height > smallViewport);
    }

    private static void AssertInsideCanvas(PadCanvas canvas, PadInfoPopover popover, ScrollViewer scroll)
    {
        var top = Avalonia.VisualExtensions.TranslatePoint(popover, default, canvas)!.Value;
        var bottom = Avalonia.VisualExtensions.TranslatePoint(popover, new Point(0, popover.Bounds.Height), canvas)!.Value;
        Assert.True(top.Y >= 0, $"Panel top {top.Y} is above canvas.");
        Assert.True(bottom.Y <= canvas.Bounds.Height, $"Panel bottom {bottom.Y} exceeds canvas {canvas.Bounds.Height}.");
        var confirm = Assert.Single(popover.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("padInfoConfirm"));
        Assert.True(confirm.IsEffectivelyVisible);
        var footer = Avalonia.VisualExtensions.TranslatePoint(confirm, default, popover)!.Value;
        Assert.True(footer.Y + confirm.Bounds.Height <= popover.Bounds.Height);
        var contentBottom = Avalonia.VisualExtensions.TranslatePoint(scroll, new Point(0, scroll.Bounds.Height), popover)!.Value;
        Assert.True(contentBottom.Y <= footer.Y);
        Assert.All(confirm.GetVisualDescendants().OfType<Button>(), button =>
            Assert.True(new Rect(popover.Bounds.Size).Contains(Avalonia.VisualExtensions.TranslatePoint(button, new Rect(button.Bounds.Size).Center, popover)!.Value)));
    }
}
