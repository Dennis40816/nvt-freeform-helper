using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    /// <summary>
    /// Draws the axis labels (row and column numbers) and IC group blocks on the canvas.
    /// These labels are drawn in screen coordinates and their size adjusts with zoom.
    /// </summary>
    private void DrawAxisLabels(DrawingContext context)
    {
        _axisLabelHits.Clear();
        var lowDetailZoomThreshold = GetResourceDouble("CanvasLowDetailZoomThreshold", 0.3);
        if (_isMiddlePanning || _isSpacePanning || _zoom <= lowDetailZoomThreshold)
        {
            return;
        }

        if (RegularPads is null || RegularPads.Count == 0)
        {
            return;
        }

        var baseFontSize = GetResourceDouble("CanvasAxisLabelBaseFontSize", GetResourceDouble("FontSizeBase", 12.0));
        var minFontSize = GetResourceDouble("CanvasAxisLabelMinFontSize", GetResourceDouble("FontSizeMin", 2.0));
        var maxFontSize = GetResourceDouble("CanvasAxisLabelMaxFontSize", GetResourceDouble("FontSizeMax", 16.0));
        var hitPadding = Math.Max(GetResourceDouble("Space2", 2.0), 1.0);
        var painter = new PadCanvasAxisLabelPainter(
            new PadCanvasAxisLabelPainter.Services(
                WorldToScreen,
                GetBoundsCenter,
                GetResourceDouble,
                key => GetResourceColor(key),
                GetResourceBrush,
                GetBrush,
                GetLabelFontSize,
                GetAxisLabelLayout,
                GetAxisLabelStride),
            _axisLabelHits);
        var labelStyle = new PadCanvasAxisLabelPainter.Style(baseFontSize, minFontSize, maxFontSize, hitPadding);

        var regBounds = GetRegularBounds(); // Get bounding box of regular pads.
        if (regBounds is null)
        {
            return;
        }

        var minRow = RegularPads.Min(p => p.Row);
        var minCol = RegularPads.Min(p => p.Col);
        // Get the screen Y-coordinate of the top edge of the regular grid.
        var gridTop = WorldToScreen(new Point2(regBounds.Value.MinX, regBounds.Value.MaxY)).Y;
        var maxRow = RegularPads.Max(p => p.Row);

        var firstRowPads = RegularPads
            .Where(p => p.Row == minRow)
            .OrderBy(p => p.Col)
            .ToList();
        var columnMetrics = painter.DrawColumnLabels(context, firstRowPads, gridTop, labelStyle);

        var firstColPads = RegularPads
            .Where(p => p.Col == minCol)
            .OrderBy(p => p.Row)
            .ToList();
        painter.DrawRowLabels(context, firstColPads, minRow, maxRow, regBounds.Value, labelStyle);

        painter.DrawIcGroupBlocks(context, firstRowPads, regBounds.Value, gridTop, columnMetrics, hitPadding);
    }

    /// <summary>
    /// Calculates an appropriate font size for axis labels based on zoom level and available space.
    /// The font size scales with zoom but is clamped to a min/max and constrained by step size.
    /// </summary>
    /// <param name="step">The screen distance between two adjacent pads along the axis.</param>
    /// <param name="baseSize">The base font size at default zoom.</param>
    /// <param name="minSize">The minimum allowed font size.</param>
    /// <param name="maxSize">The maximum allowed font size.</param>
    /// <returns>The calculated font size.</returns>
    private double GetLabelFontSize(double step, double baseSize, double minSize, double maxSize)
    {
        return AxisLabelMetricsPolicy.FontSize(_zoom, step, baseSize, minSize, maxSize);
    }

    /// <summary>
    /// Retrieves or creates a cached <see cref="TextLayout"/> for axis labels to optimize text rendering.
    /// </summary>
    /// <param name="text">The text content of the label.</param>
    /// <param name="fontSize">The font size for the label.</param>
    /// <returns>A <see cref="TextLayout"/> object.</returns>
    private TextLayout GetAxisLabelLayout(string text, double fontSize)
    {
        var labelColor = GetResourceColor("ColorCanvasAxisLabel", Color.FromRgb(210, 220, 240));
        var colorKey = ((uint)labelColor.A << 24) | ((uint)labelColor.R << 16) | ((uint)labelColor.G << 8) | labelColor.B;

        // Create a cache key based on text and rounded font size.
        var keySize = (int)Math.Round(fontSize * 10);
        var cacheKey = $"{text}|{keySize}|{colorKey}";
        if (_labelLayoutCache.TryGetValue(cacheKey, out var layout))
        {
            return layout;
        }

        var resolvedSize = Math.Max(GetResourceDouble("Space1", 1.0), keySize / 10.0); // Ensure minimum font size.
        // Create a new TextLayout with the specified text, typeface, size, and color.
        layout = new TextLayout(text, AxisLabelTypeface, resolvedSize, GetBrush(labelColor));
        _labelLayoutCache[cacheKey] = layout;
        return layout;
    }

    private int GetAxisLabelStride(double step, double fontSize, int count, double maxLabelWidth)
    {
        if (count <= 0 || step <= 0)
        {
            return 1;
        }

        var minGap = GetResourceDouble("CanvasAxisLabelMinGap", GetResourceDouble("Space8", 8.0));
        var space20 = GetResourceDouble("Space20", 20.0);
        return AxisLabelMetricsPolicy.Stride(step, fontSize, count, maxLabelWidth, minGap, space20);
    }
}
