using Avalonia;
using Avalonia.Controls;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.UI.Services;

public static class CanvasFocusViewportService
{
    public static bool TryGetFocusTarget(
        PadCanvas canvas,
        IEnumerable<Window?> floatingWindows,
        out Point targetPoint)
    {
        targetPoint = default;
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(floatingWindows);

        var orderedWindows = floatingWindows
            .Where(static window => window is { IsVisible: true })
            .OrderByDescending(static window => window!.IsActive)
            .ToList();
        foreach (var window in orderedWindows)
        {
            if (TryGetWindowAwareFocusTarget(canvas, window!, out targetPoint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetWindowAwareFocusTarget(
        PadCanvas canvas,
        Window floatingWindow,
        out Point targetPoint)
    {
        targetPoint = default;
        if (canvas.Bounds.Width <= 1 || canvas.Bounds.Height <= 1)
        {
            return false;
        }

        var canvasTopLeft = canvas.PointToScreen(new Point(0, 0));
        var canvasBottomRight = canvas.PointToScreen(new Point(canvas.Bounds.Width, canvas.Bounds.Height));
        var canvasLeft = (double)canvasTopLeft.X;
        var canvasTop = (double)canvasTopLeft.Y;
        var canvasRight = (double)canvasBottomRight.X;
        var canvasBottom = (double)canvasBottomRight.Y;
        if (canvasRight <= canvasLeft || canvasBottom <= canvasTop)
        {
            return false;
        }

        var floatingTopLeft = floatingWindow.PointToScreen(new Point(0, 0));
        var floatingBottomRight = floatingWindow.PointToScreen(
            new Point(floatingWindow.Bounds.Width, floatingWindow.Bounds.Height));
        var floatingLeft = (double)floatingTopLeft.X;
        var floatingTop = (double)floatingTopLeft.Y;
        var floatingRight = (double)floatingBottomRight.X;
        var floatingBottom = (double)floatingBottomRight.Y;

        var overlapLeft = Math.Max(canvasLeft, floatingLeft);
        var overlapRight = Math.Min(canvasRight, floatingRight);
        var overlapTop = Math.Max(canvasTop, floatingTop);
        var overlapBottom = Math.Min(canvasBottom, floatingBottom);
        if (overlapLeft >= overlapRight || overlapTop >= overlapBottom)
        {
            return false;
        }

        var leftVisibleWidth = Math.Max(0.0, overlapLeft - canvasLeft);
        var rightVisibleWidth = Math.Max(0.0, canvasRight - overlapRight);
        if (leftVisibleWidth <= 1 && rightVisibleWidth <= 1)
        {
            return false;
        }

        double visibleLeft;
        double visibleRight;
        if (leftVisibleWidth >= rightVisibleWidth)
        {
            visibleLeft = canvasLeft;
            visibleRight = overlapLeft;
        }
        else
        {
            visibleLeft = overlapRight;
            visibleRight = canvasRight;
        }

        var targetXScreen = (visibleLeft + visibleRight) / 2.0;
        var targetYScreen = (canvasTop + canvasBottom) / 2.0;
        var localX = Math.Clamp(targetXScreen - canvasLeft, 0.0, canvas.Bounds.Width);
        var localY = Math.Clamp(targetYScreen - canvasTop, 0.0, canvas.Bounds.Height);
        targetPoint = new Point(localX, localY);
        return true;
    }
}
