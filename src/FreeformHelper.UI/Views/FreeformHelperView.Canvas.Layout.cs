using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Services;

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
        Dispatcher.Post(() =>
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
        var selectionVisible = PadInfoLayoutCalculator.RectsOverlap(selectionRect, canvasRect);
        var anchorWorld = PadInfoLayoutCalculator.ChooseAnchor(bounds, _padInfoSelectedBounds); // Determine the best anchor point in world coordinates.
        var anchorCanvas = _canvas.WorldToScreen(anchorWorld); // Convert anchor to canvas screen coordinates.
        var anchorScreen = ToOverlay(anchorCanvas); // Convert anchor to overlay screen coordinates.
        var anchorClampMargin = selectionVisible
            ? GetResourceDouble("PadInfoAnchorClampMargin", 8.0)
            : 0.0;
        anchorScreen = PadInfoLayoutCalculator.ClampPointToRect(anchorScreen, canvasRect, anchorClampMargin);

        var gap = GetResourceDouble("PadInfoGap", 20.0); // Minimum gap between popover and selection.
        var minLine = GetResourceDouble("PadInfoMinLineLength", 20.0); // Minimum length of the link line.
        var lineInset = GetResourceDouble("PadInfoLineInset", 12.0); // Inset for the link line start/end points on the popover.

        // Choose the optimal placement for the popover relative to the selection.
        var placement = PadInfoLayoutCalculator.ChoosePlacement(selectionRect, panelSize, canvasRect, gap, minLine, lineInset);
        var panelPoint = placement.panelPoint;

        // Position the popover.
        Canvas.SetLeft(_padInfoPopover, panelPoint.X);
        Canvas.SetTop(_padInfoPopover, panelPoint.Y);

        var panelRect = new Rect(panelPoint, panelSize);
        if (selectionVisible)
        {
            // Adjust the anchor point to be outside the popover panel if it's inside.
            var anchorPanelMargin = GetResourceDouble("PadInfoAnchorPanelMargin", 6.0);
            anchorScreen = PadInfoLayoutCalculator.PushAnchorOutsidePanel(panelRect, anchorScreen, anchorPanelMargin);

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
    /// Measures the desired size of a control within available space.
    /// </summary>
    private static Size MeasurePopover(Control popover, Size available)
    {
        popover.Measure(available);
        var size = popover.DesiredSize;
        return new Size(Math.Max(10, size.Width), Math.Max(10, size.Height));
    }
}
