// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Size = Avalonia.Size;

namespace FreeformHelper.UI.Services;

/// <summary>Renders pad-info placement and coalesces layout updates on the UI thread.</summary>
internal sealed class PadInfoOverlayRenderer
{
    private readonly Control _resourceOwner;
    private readonly Func<Canvas?> _getOverlay;
    private readonly Func<Control?> _getPopover;
    private readonly Func<ShapePath?> _getLink;
    private readonly Func<Ellipse?> _getAnchor;
    private readonly Func<Control?> _getCanvas;
    private readonly Func<Point2, Point> _worldToScreen;
    private readonly Func<Rect2?> _getBoundsWorld;
    private readonly Func<IReadOnlyList<Rect2>> _getSelectedBounds;
    private bool _isLayoutUpdateQueued; // UI thread only.

    public PadInfoOverlayRenderer(
        Control resourceOwner,
        Func<Canvas?> getOverlay,
        Func<Control?> getPopover,
        Func<ShapePath?> getLink,
        Func<Ellipse?> getAnchor,
        Func<Control?> getCanvas,
        Func<Point2, Point> worldToScreen,
        Func<Rect2?> getBoundsWorld,
        Func<IReadOnlyList<Rect2>> getSelectedBounds)
    {
        _resourceOwner = resourceOwner;
        _getOverlay = getOverlay;
        _getPopover = getPopover;
        _getLink = getLink;
        _getAnchor = getAnchor;
        _getCanvas = getCanvas;
        _worldToScreen = worldToScreen;
        _getBoundsWorld = getBoundsWorld;
        _getSelectedBounds = getSelectedBounds;
    }

    /// <summary>Posts one Background update that reads the current controls and bounds.</summary>
    public void QueueLayoutUpdate()
    {
        if (_isLayoutUpdateQueued)
        {
            return;
        }

        _isLayoutUpdateQueued = true;
        _resourceOwner.Dispatcher.Post(() =>
        {
            _isLayoutUpdateQueued = false;
            UpdateLayout();
        }, DispatcherPriority.Background);
    }

    /// <summary>Updates the panel, anchor and connector using the current viewport.</summary>
    public void UpdateLayout()
    {
        if (_getOverlay() is not { } overlay || _getPopover() is not { } popover ||
            _getLink() is not { } link || _getAnchor() is not { } anchor || _getCanvas() is not { } canvas)
        {
            return;
        }

        if (!overlay.IsVisible || _getBoundsWorld() is not { } bounds)
        {
            return;
        }

        var panelSize = MeasurePopover(popover, overlay.Bounds.Size);
        var canvasOrigin = Avalonia.VisualExtensions.TranslatePoint(canvas, new Point(0, 0), overlay) ?? new Point(0, 0);
        var canvasRect = new Rect(canvasOrigin, canvas.Bounds.Size);
        var selectionRect = BuildSelectionScreenRect(canvas, overlay, bounds);
        var selectionVisible = PadInfoLayoutCalculator.RectsOverlap(selectionRect, canvasRect);
        var anchorWorld = PadInfoLayoutCalculator.ChooseAnchor(bounds, _getSelectedBounds());
        var anchorCanvas = _worldToScreen(anchorWorld);
        var anchorScreen = ToOverlay(canvas, overlay, anchorCanvas);
        var anchorClampMargin = selectionVisible
            ? WorkspaceLayoutResourcePolicy.GetResourceDouble(_resourceOwner, "PadInfoAnchorClampMargin", 8.0)
            : 0.0;
        anchorScreen = PadInfoLayoutCalculator.ClampPointToRect(anchorScreen, canvasRect, anchorClampMargin);

        var gap = WorkspaceLayoutResourcePolicy.GetResourceDouble(_resourceOwner, "PadInfoGap", 20.0);
        var minLine = WorkspaceLayoutResourcePolicy.GetResourceDouble(_resourceOwner, "PadInfoMinLineLength", 20.0);
        var lineInset = WorkspaceLayoutResourcePolicy.GetResourceDouble(_resourceOwner, "PadInfoLineInset", 12.0);

        var placement = PadInfoLayoutCalculator.ChoosePlacement(selectionRect, panelSize, canvasRect, gap, minLine, lineInset);
        var panelPoint = placement.panelPoint;

        Canvas.SetLeft(popover, panelPoint.X);
        Canvas.SetTop(popover, panelPoint.Y);

        var panelRect = new Rect(panelPoint, panelSize);
        if (selectionVisible)
        {
            var anchorPanelMargin = WorkspaceLayoutResourcePolicy.GetResourceDouble(_resourceOwner, "PadInfoAnchorPanelMargin", 6.0);
            anchorScreen = PadInfoLayoutCalculator.PushAnchorOutsidePanel(panelRect, anchorScreen, anchorPanelMargin);

            var anchorPoint = new Point(anchorScreen.X - anchor.Width / 2.0, anchorScreen.Y - anchor.Height / 2.0);
            Canvas.SetLeft(anchor, anchorPoint.X);
            Canvas.SetTop(anchor, anchorPoint.Y);
            anchor.IsVisible = true;
        }
        else
        {
            anchor.IsVisible = false;
        }

        DrawLink(link, anchorScreen, placement.linePoint);
    }

    private static void DrawLink(ShapePath link, Point anchorScreen, Point lineEnd)
    {
        var geometry = new PathGeometry
        {
            Figures = new PathFigures
            {
                new PathFigure
                {
                    StartPoint = anchorScreen,
                    Segments = new PathSegments { new LineSegment { Point = lineEnd } }
                }
            }
        };
        link.Data = geometry;
    }

    private Rect BuildSelectionScreenRect(Control canvas, Canvas overlay, Rect2 worldBounds)
    {
        var topLeftCanvas = _worldToScreen(new Point2(worldBounds.MinX, worldBounds.MaxY));
        var bottomRightCanvas = _worldToScreen(new Point2(worldBounds.MaxX, worldBounds.MinY));
        var topLeft = ToOverlay(canvas, overlay, topLeftCanvas);
        var bottomRight = ToOverlay(canvas, overlay, bottomRightCanvas);
        return new Rect(topLeft, bottomRight).Normalize();
    }

    private static Point ToOverlay(Control canvas, Canvas overlay, Point canvasPoint)
    {
        var translated = Avalonia.VisualExtensions.TranslatePoint(canvas, canvasPoint, overlay);
        return translated ?? canvasPoint;
    }

    private static Size MeasurePopover(Control popover, Size available)
    {
        popover.Measure(available);
        var size = popover.DesiredSize;
        return new Size(Math.Max(10, size.Width), Math.Max(10, size.Height));
    }
}
