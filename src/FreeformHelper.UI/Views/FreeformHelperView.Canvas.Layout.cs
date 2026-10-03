using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private void QueuePadInfoLayoutUpdate()
    {
        if (_isPadInfoLayoutUpdateQueued)
        {
            return;
        }

        _isPadInfoLayoutUpdateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isPadInfoLayoutUpdateQueued = false;
            UpdatePadInfoLayout();
        }, DispatcherPriority.Background);
    }

    private void UpdatePadInfoLayout()
    {
        // Ensure all necessary controls and data are available.
        if (_padInfoOverlay is null || _padInfoPopover is null || _padInfoLink is null || _padInfoAnchor is null || _canvas is null)
        {
            return;
        }

        if (!_padInfoOverlay.IsVisible || _padInfoBoundsWorld is null)
        {
            return;
        }

        var bounds = _padInfoBoundsWorld.Value;
        var panelSize = MeasurePopover(_padInfoPopover, _padInfoOverlay.Bounds.Size); // Measure the popover's desired size.
        var canvasOrigin = _canvas.TranslatePoint(new Point(0, 0), _padInfoOverlay) ?? new Point(0, 0); // Canvas origin relative to overlay.
        var canvasRect = new Rect(canvasOrigin, _canvas.Bounds.Size); // Canvas bounds relative to overlay.
        var selectionRect = BuildSelectionScreenRect(bounds); // Screen bounds of selected pads.
        var selectionVisible = RectsOverlap(selectionRect, canvasRect);
        var anchorWorld = ChooseAnchor(bounds, _padInfoSelectedBounds); // Determine the best anchor point in world coordinates.
        var anchorCanvas = _canvas.WorldToScreen(anchorWorld); // Convert anchor to canvas screen coordinates.
        var anchorScreen = ToOverlay(anchorCanvas); // Convert anchor to overlay screen coordinates.
        var anchorClampMargin = selectionVisible
            ? GetResourceDouble("PadInfoAnchorClampMargin", 8.0)
            : 0.0;
        anchorScreen = ClampPointToRect(anchorScreen, canvasRect, anchorClampMargin);

        var gap = GetResourceDouble("PadInfoGap", 20.0); // Minimum gap between popover and selection.
        var minLine = GetResourceDouble("PadInfoMinLineLength", 20.0); // Minimum length of the link line.
        var lineInset = GetResourceDouble("PadInfoLineInset", 12.0); // Inset for the link line start/end points on the popover.

        // Choose the optimal placement for the popover relative to the selection.
        var placement = ChoosePlacement(selectionRect, panelSize, canvasRect, gap, minLine, lineInset);
        var panelPoint = placement.panelPoint;

        // Position the popover.
        Canvas.SetLeft(_padInfoPopover, panelPoint.X);
        Canvas.SetTop(_padInfoPopover, panelPoint.Y);

        var panelRect = new Rect(panelPoint, panelSize);
        if (selectionVisible)
        {
            // Adjust the anchor point to be outside the popover panel if it's inside.
            var anchorPanelMargin = GetResourceDouble("PadInfoAnchorPanelMargin", 6.0);
            anchorScreen = PushAnchorOutsidePanel(panelRect, anchorScreen, anchorPanelMargin);

            // Position the anchor visual element.
            var anchorPoint = new Point(anchorScreen.X - _padInfoAnchor.Width / 2.0, anchorScreen.Y - _padInfoAnchor.Height / 2.0);
            Canvas.SetLeft(_padInfoAnchor, anchorPoint.X);
            Canvas.SetTop(_padInfoAnchor, anchorPoint.Y);
            _padInfoAnchor.IsVisible = true;
        }
        else
        {
            _padInfoAnchor.IsVisible = false;
        }

        // Draw the link line from the anchor to the popover.
        var lineEnd = placement.linePoint;
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
        _padInfoLink.Data = geometry;
    }

    /// <summary>
    /// Builds a screen-coordinate rectangle representing the union of selected pad bounds.
    /// </summary>
    /// <param name="worldBounds">The world bounds of the selected pads.</param>
    /// <returns>A normalized <see cref="Rect"/> in screen coordinates.</returns>
    private Rect BuildSelectionScreenRect(Rect2 worldBounds)
    {
        if (_canvas is null)
        {
            return new Rect();
        }

        // Convert world bounds corners to canvas screen coordinates.
        var topLeftCanvas = _canvas.WorldToScreen(new Point2(worldBounds.MinX, worldBounds.MaxY));
        var bottomRightCanvas = _canvas.WorldToScreen(new Point2(worldBounds.MaxX, worldBounds.MinY));
        // Convert to overlay coordinates.
        var topLeft = ToOverlay(topLeftCanvas);
        var bottomRight = ToOverlay(bottomRightCanvas);
        return new Rect(topLeft, bottomRight).Normalize(); // Create and normalize the screen rect.
    }

    /// <summary>
    /// Translates a point from <see cref="PadCanvas"/> coordinates to <see cref="_padInfoOverlay"/> coordinates.
    /// </summary>
    /// <param name="canvasPoint">The point in canvas coordinates.</param>
    /// <returns>The translated point in overlay coordinates.</returns>
    private Point ToOverlay(Point canvasPoint)
    {
        if (_canvas is null || _padInfoOverlay is null)
        {
            return canvasPoint;
        }

        // Use Avalonia's TranslatePoint to convert coordinates between visual elements.
        var translated = _canvas.TranslatePoint(canvasPoint, _padInfoOverlay);
        return translated ?? canvasPoint; // Return translated point or original if translation fails.
    }

    /// <summary>
    /// Builds the union of multiple bounding boxes.
    /// </summary>
    private static Rect2 BuildBounds(IEnumerable<Rect2> rects)
    {
        Rect2? bounds = null;
        foreach (var r in rects)
        {
            bounds = bounds is null ? r : Rect2.Union(bounds.Value, r);
        }
        return bounds ?? new Rect2(0, 0, 0, 0);
    }

    /// <summary>
    /// Measures the desired size of a control within available space.
    /// </summary>
    private static Size MeasurePopover(Control popover, Size available)
    {
        popover.Measure(available);
        var size = popover.DesiredSize;
        return new Size(Math.Max(10, size.Width), Math.Max(10, size.Height));
    }

    /// <summary>
    /// Chooses an optimal anchor point for the popover link line from the selected pads.
    /// If multiple pads are selected, it aims for a central pad by area-weighted centroid.
    /// </summary>
    private static Point2 ChooseAnchor(Rect2 union, IReadOnlyList<Rect2> selected)
    {
        if (selected is { Count: > 0 })
        {
            var areas = selected
                .Select(r => new { Rect = r, Area = Math.Abs((r.MaxX - r.MinX) * (r.MaxY - r.MinY)) })
                .Where(x => x.Area > 0)
                .ToList();

            if (areas.Count > 0)
            {
                // Calculate area-weighted centroid of selected pads.
                var totalArea = areas.Sum(a => a.Area);
                var centroid = new Point2(
                    areas.Sum(a => a.Area * ((a.Rect.MinX + a.Rect.MaxX) * 0.5)) / totalArea,
                    areas.Sum(a => a.Area * ((a.Rect.MinY + a.Rect.MaxY) * 0.5)) / totalArea);

                // Find the selected pad whose center is closest to the area-weighted centroid.
                var nearest = areas
                    .OrderBy(a =>
                    {
                        var cx = (a.Rect.MinX + a.Rect.MaxX) * 0.5;
                        var cy = (a.Rect.MinY + a.Rect.MaxY) * 0.5;
                        var dx = cx - centroid.X;
                        var dy = cy - centroid.Y;
                        return (dx * dx) + (dy * dy); // Distance squared.
                    })
                    .First()
                    .Rect;

                return new Point2((nearest.MinX + nearest.MaxX) * 0.5, (nearest.MinY + nearest.MaxY) * 0.5);
            }
        }

        // Fallback: use the center of the union of all bounds.
        return new Point2((union.MinX + union.MaxX) * 0.5, (union.MinY + union.MaxY) * 0.5);
    }

    /// <summary>
    /// Determines the optimal placement for the popover panel and the start point of its link line.
    /// Considers available canvas space and avoids overlapping the selected region.
    /// </summary>
    private static (Point panelPoint, Point linePoint) ChoosePlacement(Rect selection, Size panelSize, Rect canvas, double gap, double minLine, double lineInset)
    {
        // Define clamping boundaries for the popover panel within the canvas.
        var minX = canvas.Left + gap;
        var minY = canvas.Top + gap;
        var maxX = Math.Max(minX, canvas.Right - panelSize.Width - gap);
        var maxY = Math.Max(minY, canvas.Bottom - panelSize.Height - gap);

        var anchor = selection.Center; // Central anchor point for the link line.

        // Candidate X positions for placing the panel to the right or left of the selection.
        var rightCandidate = Math.Max(selection.Right + gap, anchor.X + minLine);
        var leftCandidate = Math.Min(selection.Left - gap - panelSize.Width, anchor.X - minLine - panelSize.Width);

        // Check for overflow if placing panel right or left.
        var rightOverflow = Math.Max(0, rightCandidate + panelSize.Width - (canvas.Right - gap));
        var leftOverflow = Math.Max(0, (canvas.Left + gap) - leftCandidate);

        // Decide whether to place right or left based on which has less overflow.
        var placeRight = rightOverflow <= leftOverflow;

        var x = placeRight ? rightCandidate : leftCandidate;
        x = ClampToRange(x, minX, maxX); // Clamp panel X position to canvas boundaries.

        // Initial Y placement: center vertically with the anchor, then clamp.
        var y = ClampToRange(anchor.Y - panelSize.Height * 0.5, minY, maxY);
        var panelRect = new Rect(new Point(x, y), panelSize);

        // If the panel still overlaps the anchor after initial placement, try placing above or below.
        if (panelRect.Contains(anchor))
        {
            var above = anchor.Y - gap - panelSize.Height;
            var below = anchor.Y + gap;
            // Choose above or below based on which is closer to the center of the canvas.
            var candidate = Math.Abs(above - minY) < Math.Abs(maxY - below) ? above : below;
            y = ClampToRange(candidate, minY, maxY);
            panelRect = new Rect(new Point(x, y), panelSize);

            // Fallback if still overlapping after trying above/below.
            if (panelRect.Contains(anchor))
            {
                var fallback = anchor.Y < y ? anchor.Y + gap : anchor.Y - gap - panelSize.Height;
                y = ClampToRange(fallback, minY, maxY);
            }
        }

        // Determine the point on the panel's edge where the link line will connect.
        var lineX = placeRight ? x : x + panelSize.Width;
        var lineY = ClampToRange(anchor.Y, y + lineInset, y + panelSize.Height - lineInset);
        var linePoint = new Point(lineX, lineY);
        // Ensure the line has a minimum length.
        linePoint = EnsureMinLineLength(anchor, linePoint, y, panelSize.Height, lineInset, minLine);

        return (new Point(x, y), linePoint);
    }

    /// <summary>
    /// Adjusts the anchor point to be outside the panel rectangle if it currently lies inside.
    /// This ensures the link line always originates from outside the popover body.
    /// </summary>
    private static Point PushAnchorOutsidePanel(Rect panel, Point anchor, double margin)
    {
        if (!panel.Contains(anchor))
        {
            return anchor; // Anchor is already outside.
        }

        // Calculate distances from anchor to each panel edge.
        var left = Math.Abs(anchor.X - panel.Left);
        var right = Math.Abs(panel.Right - anchor.X);
        var top = Math.Abs(anchor.Y - panel.Top);
        var bottom = Math.Abs(panel.Bottom - anchor.Y);

        // Move anchor to the closest edge, plus a margin.
        var min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        if (min == left)
        {
            return new Point(panel.Left - margin, Clamp(anchor.Y, panel.Top, panel.Bottom));
        }
        if (min == right)
        {
            return new Point(panel.Right + margin, Clamp(anchor.Y, panel.Top, panel.Bottom));
        }
        if (min == top)
        {
            return new Point(Clamp(anchor.X, panel.Left, panel.Right), panel.Top - margin);
        }

        return new Point(Clamp(anchor.X, panel.Left, panel.Right), panel.Bottom + margin);
    }

    /// <summary>
    /// Ensures that the link line has a minimum visual length.
    /// If the line is too short, it adjusts the end point to meet the minimum length.
    /// </summary>
    private static Point EnsureMinLineLength(Point anchor, Point line, double panelTop, double panelHeight, double inset, double minLength)
    {
        var dx = line.X - anchor.X;
        var dy = line.Y - anchor.Y;
        var dist = Math.Sqrt((dx * dx) + (dy * dy)); // Current distance.
        if (dist >= minLength)
        {
            return line; // Already long enough.
        }

        // Calculate boundaries for the line end point along the panel's edge.
        var minY = panelTop + inset;
        var maxY = panelTop + panelHeight - inset;
        // Calculate needed vertical distance for the minimum length.
        var needed = Math.Sqrt(Math.Max(0, (minLength * minLength) - (dx * dx)));

        // Try adjusting the line end point up or down to meet minLength.
        var up = anchor.Y - needed;
        if (up >= minY)
        {
            return new Point(line.X, up);
        }

        var down = anchor.Y + needed;
        if (down <= maxY)
        {
            return new Point(line.X, down);
        }

        return line; // Cannot extend line within constraints.
    }

    /// <summary>
    /// Clamps a value within a specified range, handling cases where max is less than min.
    /// </summary>
    private static double ClampToRange(double value, double min, double max)
    {
        if (max < min)
        {
            return min; // If range is invalid, clamp to min.
        }
        return Clamp(value, min, max); // Use standard clamp.
    }

    /// <summary>
    /// Clamps a value between a minimum and maximum.
    /// </summary>
    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static Point ClampPointToRect(Point point, Rect rect, double margin)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return point;
        }

        var safeMarginX = Math.Max(0, Math.Min(margin, rect.Width * 0.5));
        var safeMarginY = Math.Max(0, Math.Min(margin, rect.Height * 0.5));
        var minX = rect.Left + safeMarginX;
        var maxX = rect.Right - safeMarginX;
        var minY = rect.Top + safeMarginY;
        var maxY = rect.Bottom - safeMarginY;

        if (maxX < minX)
        {
            maxX = minX;
        }

        if (maxY < minY)
        {
            maxY = minY;
        }

        return new Point(
            Clamp(point.X, minX, maxX),
            Clamp(point.Y, minY, maxY));
    }

    private static bool RectsOverlap(Rect first, Rect second)
    {
        if (first.Width <= 0 || first.Height <= 0 || second.Width <= 0 || second.Height <= 0)
        {
            return false;
        }

        return first.Right > second.Left &&
               first.Left < second.Right &&
               first.Bottom > second.Top &&
               first.Top < second.Bottom;
    }
}
