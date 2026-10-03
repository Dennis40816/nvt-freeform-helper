using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
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

        // --- Column Labels ---
        var firstRowPads = RegularPads
            .Where(p => p.Row == minRow)
            .OrderBy(p => p.Col)
            .ToList();

        var stepX = 0.0;
        if (firstRowPads.Count >= 2)
        {
            // Calculate screen distance between centers of adjacent pads for font scaling.
            stepX = Math.Abs(WorldToScreen(GetBoundsCenter(firstRowPads[1].Bounds)).X -
                             WorldToScreen(GetBoundsCenter(firstRowPads[0].Bounds)).X);
        }

        var fontSizeX = GetLabelFontSize(stepX, baseFontSize, minFontSize, maxFontSize);
        var labelMarginX = Math.Max(
            GetResourceDouble("CanvasAxisLabelTopMargin", GetResourceDouble("Space8", 8.0)),
            fontSizeX * 1.1);
        var labelHeightX = fontSizeX;
        var charWidthX = fontSizeX * 0.65; // Approximate char width for layout.
        var maxColLabelText = firstRowPads
            .Select(p => p.Col.ToString(CultureInfo.InvariantCulture))
            .OrderByDescending(text => text.Length)
            .ThenByDescending(text => text, StringComparer.Ordinal)
            .FirstOrDefault() ?? "0";
        var maxColLabelWidth = GetAxisLabelLayout(maxColLabelText, fontSizeX).Width;
        var strideX = GetAxisLabelStride(stepX, fontSizeX, firstRowPads.Count, maxColLabelWidth);

        for (var i = 0; i < firstRowPads.Count; i++)
        {
            if (!ShouldRenderAxisLabel(i, firstRowPads.Count, strideX))
            {
                continue;
            }

            var pad = firstRowPads[i];
            var screen = WorldToScreen(GetBoundsCenter(pad.Bounds)); // Screen center of the pad.
            var text = pad.Col.ToString(CultureInfo.InvariantCulture);
            var layout = GetAxisLabelLayout(text, fontSizeX);

            var width = Math.Max(layout.Width, Math.Max(labelHeightX, text.Length * charWidthX));
            var height = Math.Max(layout.Height, labelHeightX);
            // Position label above the grid, centered on the pad's X, offset by margins.
            var labelY = gridTop - labelMarginX - height;
            if (labelY < 0)
            {
                continue;
            }

            // Keep axis labels outside regular-pad area.
            if (labelY + height > gridTop)
            {
                continue;
            }

            var offsetPoint = new Point(screen.X - width / 2.0, labelY);
            layout.Draw(context, offsetPoint);
            var hitRect = new Rect(
                offsetPoint.X - hitPadding,
                offsetPoint.Y - hitPadding,
                width + hitPadding * 2,
                height + hitPadding * 2);
            _axisLabelHits.Add(new AxisLabelHit(AxisLabelKind.Column, pad.Col, hitRect));
        }

        // --- Row Labels ---
        var firstColPads = RegularPads
            .Where(p => p.Col == minCol)
            .OrderBy(p => p.Row)
            .ToList();

        var stepY = 0.0;
        if (firstColPads.Count >= 2)
        {
            // Calculate screen distance between centers of adjacent pads for font scaling.
            stepY = Math.Abs(WorldToScreen(GetBoundsCenter(firstColPads[1].Bounds)).Y -
                             WorldToScreen(GetBoundsCenter(firstColPads[0].Bounds)).Y);
        }

        var fontSizeY = GetLabelFontSize(stepY, baseFontSize, minFontSize, maxFontSize);
        var labelMarginY = Math.Max(
            GetResourceDouble("CanvasAxisLabelSideMargin", GetResourceDouble("Space8", 8.0)),
            fontSizeY * 0.8);
        var labelHeightY = fontSizeY;
        var charWidthY = fontSizeY * 0.65;
        var maxDisplayRow = Math.Max(0, maxRow - minRow);
        var maxRowLabelText = maxDisplayRow.ToString(CultureInfo.InvariantCulture);
        var maxRowLabelWidth = GetAxisLabelLayout(maxRowLabelText, fontSizeY).Width;
        var strideY = GetAxisLabelStride(stepY, fontSizeY, firstColPads.Count, maxRowLabelWidth);
        // Get the screen X-coordinate of the left edge of the regular grid.
        var gridLeftFixed = WorldToScreen(new Point2(regBounds.Value.MinX, regBounds.Value.MaxY)).X;

        for (var i = 0; i < firstColPads.Count; i++)
        {
            if (!ShouldRenderAxisLabel(i, firstColPads.Count, strideY))
            {
                continue;
            }

            var pad = firstColPads[i];
            var screen = WorldToScreen(GetBoundsCenter(pad.Bounds));
            var displayRow = maxRow - pad.Row; // Display row count from bottom up (inverted Y).
            var text = displayRow.ToString(CultureInfo.InvariantCulture);
            var layout = GetAxisLabelLayout(text, fontSizeY);

            var width = Math.Max(layout.Width, Math.Max(labelHeightY, text.Length * charWidthY));
            var height = Math.Max(layout.Height, labelHeightY);
            // Position label to the left of the grid, centered on the pad's Y, offset by margins.
            var labelX = gridLeftFixed - width - labelMarginY;
            if (labelX < 0)
            {
                continue;
            }

            // Keep axis labels outside regular-pad area.
            if (labelX + width > gridLeftFixed)
            {
                continue;
            }

            var offsetPoint = new Point(labelX, screen.Y - height / 2.0);
            layout.Draw(context, offsetPoint);
            var hitRect = new Rect(
                offsetPoint.X - hitPadding,
                offsetPoint.Y - hitPadding,
                width + hitPadding * 2,
                height + hitPadding * 2);
            _axisLabelHits.Add(new AxisLabelHit(AxisLabelKind.Row, pad.Row, hitRect));
        }

        // --- IC Group Blocks ---
        if (firstRowPads.Count > 0)
        {
            var icGroups = firstRowPads
                .GroupBy(p => p.IcIndex) // Group pads by their IC index.
                .OrderBy(g => g.Min(p => p.Col)) // Order groups by their leftmost column.
                .ToList();

            if (icGroups.Count > 1) // Only draw blocks if there's more than one IC.
            {
                var blockHeight = Math.Max(
                    GetResourceDouble("CanvasIcBlockMinHeight", 18.0),
                    fontSizeX + GetResourceDouble("CanvasIcBlockHeightExtra", 8.0));
                var blockPadding = Math.Max(
                    GetResourceDouble("CanvasIcBlockPadding", GetResourceDouble("Space2", 2.0)),
                    fontSizeX * 0.35);
                // Position blocks above column labels.
                var blockTop = gridTop - labelMarginX - labelHeightX - blockPadding - blockHeight;
                if (blockTop < 0)
                {
                    return;
                }

                var fill = GetResourceBrush("BrushCanvasIcBlockFill", Brushes.Transparent);
                var strokeColor = GetResourceColor("ColorCanvasIcBlockStroke");
                var strokeWidth = Math.Max(GetResourceDouble("Space1", 1.0), GetResourceDouble("CanvasSelectionStrokeThickness", 1.5));
                var stroke = new Pen(GetBrush(strokeColor), strokeWidth);
                var blockCornerRadius = Math.Max(GetResourceDouble("Space2", 2.0), fontSizeX * 0.35);
                var labelInset = Math.Max(GetResourceDouble("CanvasIcBlockLabelInset", 3.0), fontSizeX * 0.15);

                foreach (var group in icGroups)
                {
                    // Calculate the world X-range for this IC group.
                    var minX = group.Min(p => p.Bounds.MinX);
                    var maxX = group.Max(p => p.Bounds.MaxX);
                    // Convert world X-coordinates to screen X-coordinates.
                    var sx0 = WorldToScreen(new Point2(minX, regBounds.Value.MaxY)).X;
                    var sx1 = WorldToScreen(new Point2(maxX, regBounds.Value.MaxY)).X;
                    var left = Math.Min(sx0, sx1);
                    var width = Math.Max(2, Math.Abs(sx1 - sx0)); // Ensure minimum width.

                    var rect = new Rect(left, blockTop, width, blockHeight); // Rectangle for the IC block.
                    context.DrawRectangle(fill, stroke, new RoundedRect(rect, blockCornerRadius));

                    var label = $"IC{group.Key + 1}"; // IC label (1-indexed).
                    var layout = GetAxisLabelLayout(label, fontSizeX);
                    if (width >= layout.Width + labelInset * 2)
                    {
                        var labelX = left + Math.Max(labelInset, (width - layout.Width) / 2.0);
                        var centeredY = blockTop + (blockHeight - layout.Height) / 2.0;
                        var maxY = blockTop + Math.Max(labelInset, blockHeight - layout.Height - labelInset);
                        var labelY = Math.Clamp(centeredY, blockTop + labelInset, maxY);
                        var labelPoint = new Point(labelX, labelY);
                        layout.Draw(context, labelPoint);
                    }

                    var hitRect = new Rect(
                        Math.Max(0, left - hitPadding),
                        Math.Max(0, blockTop - hitPadding),
                        width + hitPadding * 2,
                        blockHeight + hitPadding * 2);
                    _axisLabelHits.Add(new AxisLabelHit(AxisLabelKind.Ic, group.Key, hitRect));
                }
            }
        }
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
        // Font size scales with the square root of zoom to make it visually balanced.
        var sizeFromZoom = baseSize * Math.Sqrt(Math.Max(_zoom, 1e-6));
        if (step > 0)
        {
            // Also constrain font size by the space available (step size).
            // This prevents labels from overlapping when zoomed out.
            var sizeFromStep = step * 0.6; // Use 60% of the step to ensure some padding.
            sizeFromZoom = Math.Min(sizeFromZoom, sizeFromStep);
        }

        return Math.Clamp(sizeFromZoom, minSize, maxSize); // Clamp to overall min/max.
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

        var expectedLabelWidth = Math.Max(fontSize, Math.Max(1.0, maxLabelWidth));
        var axisLabelMinGap = Math.Max(GetResourceDouble("CanvasAxisLabelMinGap", GetResourceDouble("Space8", 8.0)), 1.0);
        var minSpacing = Math.Max(GetResourceDouble("Space20", 20.0), expectedLabelWidth + axisLabelMinGap);
        var stride = (int)Math.Ceiling(minSpacing / step);
        return Math.Clamp(stride, 1, count);
    }

    private static bool ShouldRenderAxisLabel(int index, int totalCount, int stride)
    {
        if (totalCount <= 2 || stride <= 1)
        {
            return true;
        }

        return index == 0 || index == totalCount - 1 || index % stride == 0;
    }
}
