using System.Globalization;
using Avalonia;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// This partial class of <see cref="PadCanvas"/> manages the view-related functionalities,
/// such as zooming, panning (implicitly handled by <see cref="_pan"/> and <see cref="_zoom"/>
/// and exposed via <see cref="WorldToScreen"/> and <see cref="ScreenToWorld"/>),
/// fitting content to view, resetting view, and drawing axis labels.
/// </summary>
public sealed partial class PadCanvas
{
    // Typeface for rendering axis labels.
    private static Typeface AxisLabelTypeface => new(
        (FontFamily)Avalonia.Application.Current!.Resources["FontFamilyUi"]!, FontStyle.Normal, FontWeight.SemiBold);

    // Flags for managing delayed FitToContent calls.
    private bool _pendingFitToContent;
    private bool _fitToContentSubscribed;

    /// <summary>
    /// Adjusts the canvas view (zoom and pan) so that all loaded CAD and regular pads
    /// are visible and fit within the current canvas bounds, considering margins.
    /// </summary>
    public void FitToContent()
    {
        // Defer the fit operation to ensure the layout system has fully propagated changes (e.g. window maximization).
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var wb = GetWorldBounds(); // Get the bounding box of all content in world coordinates.
            if (wb is null || wb.Value.IsEmpty)
            {
                return; // No content to fit.
            }

            // If canvas is not yet laid out (width/height <= 1), queue the fit operation.
            if (Bounds.Width <= 1 || Bounds.Height <= 1)
            {
                QueueFitToContent();
                return;
            }

            ApplyFit(wb.Value); // Apply the fit transformation.
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Queues a FitToContent operation to be executed after the control's layout is updated.
    /// This is necessary if FitToContent is called before the control has valid dimensions.
    /// </summary>
    private void QueueFitToContent()
    {
        _pendingFitToContent = true;
        if (_fitToContentSubscribed)
        {
            return; // Already subscribed, no need to subscribe again.
        }

        _fitToContentSubscribed = true;
        LayoutUpdated += OnLayoutUpdatedFitToContent; // Subscribe to LayoutUpdated event.
    }

    /// <summary>
    /// Event handler for the LayoutUpdated event, used to execute queued FitToContent operations.
    /// </summary>
    private void OnLayoutUpdatedFitToContent(object? sender, EventArgs e)
    {
        // If control dimensions are still invalid, wait for another layout update.
        if (Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        // If FitToContent is no longer pending, unsubscribe.
        if (!_pendingFitToContent)
        {
            LayoutUpdated -= OnLayoutUpdatedFitToContent;
            _fitToContentSubscribed = false;
            return;
        }

        var wb = GetWorldBounds();
        // If no content or content is empty after layout, clean up and exit.
        if (wb is null || wb.Value.IsEmpty)
        {
            _pendingFitToContent = false;
            LayoutUpdated -= OnLayoutUpdatedFitToContent;
            _fitToContentSubscribed = false;
            return;
        }

        // Execute the fit operation, then clean up.
        _pendingFitToContent = false;
        LayoutUpdated -= OnLayoutUpdatedFitToContent;
        _fitToContentSubscribed = false;
        ApplyFit(wb.Value);
    }

    /// <summary>
    /// Applies the calculated zoom and pan values to fit the given world bounds into the view.
    /// </summary>
    /// <param name="bounds">The world bounding box to fit.</param>
    private void ApplyFit(Rect2 bounds)
    {
        var margins = GetFitMargins(); // Get dynamic margins for labels and UI elements.
        // Calculate the effective drawable area in screen coordinates.
        var viewW = Math.Max(1.0, Bounds.Width - (margins.Left + margins.Right));
        var viewH = Math.Max(1.0, Bounds.Height - (margins.Top + margins.Bottom));

        // Calculate zoom factor: We use world->screen: x = worldX*zoom + panX, y = -worldY*zoom + panY.
        // The Y is inverted in screen space, so we use -_zoom for the transform.
        var scaleX = viewW / bounds.Width;
        var scaleY = viewH / bounds.Height;
        _zoom = Math.Max(1e-6, Math.Min(scaleX, scaleY)); // Use the smaller scale to ensure everything fits.

        // Calculate pan to center the content.
        var worldCenter = new Point2((bounds.MinX + bounds.MaxX) / 2.0, (bounds.MinY + bounds.MaxY) / 2.0);
        var screenCenter = new Point(margins.Left + viewW / 2.0, margins.Top + viewH / 2.0);

        _pan = new Vector(
            screenCenter.X - worldCenter.X * _zoom,
            screenCenter.Y + worldCenter.Y * _zoom); // Adjust Y for inverted screen coordinates.

        InvalidateViewFrameSnapshot();
        RequestViewRefresh(); // Request redraw and notify view observers on the next frame.
    }

    /// <summary>
    /// Resets the canvas view to its default zoom (1.0) and pan (0,0).
    /// </summary>
    public void ResetView()
    {
        _zoom = 1.0;
        _pan = new Vector(0, 0);
        InvalidateViewFrameSnapshot();
        RequestViewRefresh();
    }

    /// <summary>
    /// Centers the viewport on the current selection without changing zoom.
    /// </summary>
    public void FocusSelection()
    {
        FocusSelectionInternal(minZoom: null);
    }

    /// <summary>
    /// Centers the viewport on the current selection and applies a minimum zoom level.
    /// </summary>
    public void FocusSelectionWithMinZoom(double minZoom)
    {
        FocusSelectionInternal(minZoom <= 0 ? null : minZoom);
    }

    /// <summary>
    /// Centers the viewport on the current selection using a caller-provided viewport target point.
    /// </summary>
    /// <param name="minZoom">Minimum zoom to apply after focusing.</param>
    /// <param name="targetViewportPoint">Target point in canvas viewport coordinates.</param>
    public void FocusSelectionWithMinZoomAt(double minZoom, Point targetViewportPoint)
    {
        FocusSelectionInternal(
            minZoom <= 0 ? null : minZoom,
            targetViewportPoint);
    }

    private void FocusSelectionInternal(double? minZoom, Point? targetViewportPoint = null)
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        var selectedBounds = GetSelectionBounds();
        if (selectedBounds is null || selectedBounds.Value.IsEmpty)
        {
            return;
        }

        var center = GetBoundsCenter(selectedBounds.Value);
        var targetX = targetViewportPoint?.X ?? (Bounds.Width / 2.0);
        var targetY = targetViewportPoint?.Y ?? (Bounds.Height / 2.0);
        if (minZoom.HasValue && _zoom < minZoom.Value)
        {
            _zoom = minZoom.Value;
        }

        _pan = new Vector(
            targetX - center.X * _zoom,
            targetY + center.Y * _zoom);

        InvalidateViewFrameSnapshot();
        RequestViewRefresh();
    }

    /// <summary>
    /// Zooms the canvas in or out around a specific screen pivot point.
    /// </summary>
    /// <param name="screenPivot">The screen coordinates around which to zoom.</param>
    /// <param name="factor">The zoom factor (e.g., 1.1 for zoom in, 0.9 for zoom out).</param>
    private void ZoomAt(Point screenPivot, double factor)
    {
        factor = Math.Clamp(factor, 0.25, 4.0); // Clamp factor to prevent extreme zooming.

        var worldPivot = ScreenToWorld(screenPivot); // Convert screen pivot to world coordinates.

        _zoom *= factor;
        _zoom = Math.Clamp(_zoom, 1e-6, 1e6); // Clamp zoom level to prevent extreme values.

        // Adjust pan to keep the world pivot point stable on the screen after zooming.
        _pan = new Vector(
            screenPivot.X - worldPivot.X * _zoom,
            screenPivot.Y + worldPivot.Y * _zoom);

        InvalidateViewFrameSnapshot();
        BeginTransientLowDetailNavigation();
        RequestViewRefresh();
    }

    /// <summary>
    /// Calculates dynamic margins needed for fitting content, especially to accommodate axis labels.
    /// </summary>
    private FitMargins GetFitMargins()
    {
        var baseMargin = GetResourceDouble("Space20", 20.0); // Base margin around content.

        if (RegularPads is null || RegularPads.Count == 0)
        {
            return new FitMargins(baseMargin, baseMargin, baseMargin, baseMargin);
        }

        var axisTopMargin = GetResourceDouble("CanvasAxisLabelTopMargin", GetResourceDouble("Space8", 8.0));
        var axisSideMargin = GetResourceDouble("CanvasAxisLabelSideMargin", GetResourceDouble("Space8", 8.0));
        var labelHeight = GetResourceDouble("CanvasAxisLabelBaseFontSize", GetResourceDouble("FontSizeBase", 12.0)); // Approximate height of a label.
        var charWidth = Math.Max(1.0, labelHeight * 0.65); // Approximate width of a character.
        var labelMarginTop = Math.Max(axisTopMargin, labelHeight * 1.1);
        var labelMarginSide = Math.Max(axisSideMargin, labelHeight * 0.8);
        var fitSafetyPadding = GetResourceDouble("Space2", 2.0);
        var blockHeight = Math.Max(
            GetResourceDouble("CanvasIcBlockMinHeight", 18.0),
            labelHeight + GetResourceDouble("CanvasIcBlockHeightExtra", 8.0)); // Height of an IC group block.
        var blockPadding = GetResourceDouble("CanvasIcBlockPadding", GetResourceDouble("Space4", 4.0)); // Padding for IC group block.

        // Calculate left margin based on row labels.
        var minRow = RegularPads.Min(p => p.Row);
        var maxRow = RegularPads.Max(p => p.Row);
        var maxDisplayRow = maxRow - minRow;
        var rowDigits = Math.Max(1, maxDisplayRow.ToString(CultureInfo.InvariantCulture).Length);
        var rowLabelWidth = Math.Max(labelHeight, rowDigits * charWidth);
        var leftExtra = labelMarginSide + rowLabelWidth + fitSafetyPadding;

        // Calculate top margin based on column labels and potential IC group blocks.
        var topExtra = labelMarginTop + labelHeight + fitSafetyPadding;
        var firstRowPads = RegularPads.Where(p => p.Row == minRow).ToList();
        if (firstRowPads.Count > 0)
        {
            var icGroups = firstRowPads.Select(p => p.IcIndex).Distinct().Count();
            if (icGroups > 1)
            {
                topExtra += blockPadding + blockHeight; // Add space for IC group blocks if present.
            }
        }

        return new FitMargins(baseMargin + leftExtra, baseMargin + topExtra, baseMargin, baseMargin);
    }

    /// <summary>
    /// A private record struct to hold margin values for fitting content.
    /// </summary>
    private readonly record struct FitMargins(double Left, double Top, double Right, double Bottom);
}
