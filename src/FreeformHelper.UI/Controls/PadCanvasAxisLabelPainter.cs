using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Draws the column labels, row labels and IC group blocks of one <see cref="PadCanvas"/> frame.
/// It gets coordinate conversion, theme resources and label layouts from the canvas through <see cref="Services"/>, and records the hit rectangles of the labels it draws.
/// </summary>
internal sealed class PadCanvasAxisLabelPainter
{
    private readonly Services _services;
    private readonly List<PadCanvas.AxisLabelHit> _hits;

    public PadCanvasAxisLabelPainter(Services services, List<PadCanvas.AxisLabelHit> hits)
    {
        _services = services;
        _hits = hits;
    }

    /// <summary>
    /// The canvas operations the painter needs. The canvas passes its own methods in.
    /// </summary>
    internal sealed record Services(
        Func<Point2, Point> WorldToScreen,
        Func<Rect2, Point2> GetBoundsCenter,
        Func<string, double, double> GetResourceDouble,
        Func<string, Color> GetResourceColor,
        Func<string, IBrush, IBrush> GetResourceBrush,
        Func<Color, ISolidColorBrush> GetBrush,
        Func<double, double, double, double, double> GetLabelFontSize,
        Func<string, double, TextLayout> GetAxisLabelLayout,
        Func<double, double, int, double, int> GetAxisLabelStride);

    internal readonly record struct Style(double BaseFontSize, double MinFontSize, double MaxFontSize, double HitPadding);

    internal readonly record struct ColumnLabelMetrics(double FontSize, double Margin, double Height);

    /// <summary>
    /// Draws the column numbers above the first row of the regular grid and records their hit rectangles.
    /// </summary>
    /// <returns>The font size, margin and height of the column labels, which the IC blocks are placed against.</returns>
    internal ColumnLabelMetrics DrawColumnLabels(
        DrawingContext context,
        List<RegularPad> firstRowPads,
        double gridTop,
        Style labelStyle)
    {
        var stepX = 0.0;
        if (firstRowPads.Count >= 2)
        {
            // Calculate screen distance between centers of adjacent pads for font scaling.
            stepX = Math.Abs(_services.WorldToScreen(_services.GetBoundsCenter(firstRowPads[1].Bounds)).X -
                             _services.WorldToScreen(_services.GetBoundsCenter(firstRowPads[0].Bounds)).X);
        }

        var fontSizeX = _services.GetLabelFontSize(stepX, labelStyle.BaseFontSize, labelStyle.MinFontSize, labelStyle.MaxFontSize);
        var labelMarginX = Math.Max(
            _services.GetResourceDouble("CanvasAxisLabelTopMargin", _services.GetResourceDouble("Space8", 8.0)),
            fontSizeX * 1.1);
        var labelHeightX = fontSizeX;
        var charWidthX = fontSizeX * 0.65; // Approximate char width for layout.
        var maxColLabelText = firstRowPads
            .Select(p => p.Col.ToString(CultureInfo.InvariantCulture))
            .OrderByDescending(text => text.Length)
            .ThenByDescending(text => text, StringComparer.Ordinal)
            .FirstOrDefault() ?? "0";
        var maxColLabelWidth = _services.GetAxisLabelLayout(maxColLabelText, fontSizeX).Width;
        var strideX = _services.GetAxisLabelStride(stepX, fontSizeX, firstRowPads.Count, maxColLabelWidth);
        var hitPadding = labelStyle.HitPadding;

        for (var i = 0; i < firstRowPads.Count; i++)
        {
            if (!ShouldRenderAxisLabel(i, firstRowPads.Count, strideX))
            {
                continue;
            }

            var pad = firstRowPads[i];
            var screen = _services.WorldToScreen(_services.GetBoundsCenter(pad.Bounds)); // Screen center of the pad.
            var text = pad.Col.ToString(CultureInfo.InvariantCulture);
            var layout = _services.GetAxisLabelLayout(text, fontSizeX);

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
            _hits.Add(new PadCanvas.AxisLabelHit(PadCanvas.AxisLabelKind.Column, pad.Col, hitRect));
        }

        return new ColumnLabelMetrics(fontSizeX, labelMarginX, labelHeightX);
    }

    /// <summary>
    /// Draws the row numbers left of the first column of the regular grid and records their hit rectangles.
    /// </summary>
    internal void DrawRowLabels(
        DrawingContext context,
        List<RegularPad> firstColPads,
        int minRow,
        int maxRow,
        Rect2 regBounds,
        Style labelStyle)
    {
        var stepY = 0.0;
        if (firstColPads.Count >= 2)
        {
            // Calculate screen distance between centers of adjacent pads for font scaling.
            stepY = Math.Abs(_services.WorldToScreen(_services.GetBoundsCenter(firstColPads[1].Bounds)).Y -
                             _services.WorldToScreen(_services.GetBoundsCenter(firstColPads[0].Bounds)).Y);
        }

        var fontSizeY = _services.GetLabelFontSize(stepY, labelStyle.BaseFontSize, labelStyle.MinFontSize, labelStyle.MaxFontSize);
        var labelMarginY = Math.Max(
            _services.GetResourceDouble("CanvasAxisLabelSideMargin", _services.GetResourceDouble("Space8", 8.0)),
            fontSizeY * 0.8);
        var labelHeightY = fontSizeY;
        var charWidthY = fontSizeY * 0.65;
        var maxDisplayRow = Math.Max(0, maxRow - minRow);
        var maxRowLabelText = maxDisplayRow.ToString(CultureInfo.InvariantCulture);
        var maxRowLabelWidth = _services.GetAxisLabelLayout(maxRowLabelText, fontSizeY).Width;
        var strideY = _services.GetAxisLabelStride(stepY, fontSizeY, firstColPads.Count, maxRowLabelWidth);
        // Get the screen X-coordinate of the left edge of the regular grid.
        var gridLeftFixed = _services.WorldToScreen(new Point2(regBounds.MinX, regBounds.MaxY)).X;
        var hitPadding = labelStyle.HitPadding;

        for (var i = 0; i < firstColPads.Count; i++)
        {
            if (!ShouldRenderAxisLabel(i, firstColPads.Count, strideY))
            {
                continue;
            }

            var pad = firstColPads[i];
            var screen = _services.WorldToScreen(_services.GetBoundsCenter(pad.Bounds));
            var displayRow = maxRow - pad.Row; // Display row count from bottom up (inverted Y).
            var text = displayRow.ToString(CultureInfo.InvariantCulture);
            var layout = _services.GetAxisLabelLayout(text, fontSizeY);

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
            _hits.Add(new PadCanvas.AxisLabelHit(PadCanvas.AxisLabelKind.Row, pad.Row, hitRect));
        }
    }

    /// <summary>
    /// Draws one block per IC above the column labels and records their hit rectangles.
    /// Nothing is drawn when the first row holds a single IC or the blocks would leave the canvas.
    /// </summary>
    internal void DrawIcGroupBlocks(
        DrawingContext context,
        List<RegularPad> firstRowPads,
        Rect2 regBounds,
        double gridTop,
        ColumnLabelMetrics columnMetrics,
        double hitPadding)
    {
        var fontSizeX = columnMetrics.FontSize;
        var labelMarginX = columnMetrics.Margin;
        var labelHeightX = columnMetrics.Height;

        if (firstRowPads.Count > 0)
        {
            var icGroups = firstRowPads
                .GroupBy(p => p.IcIndex) // Group pads by their IC index.
                .OrderBy(g => g.Min(p => p.Col)) // Order groups by their leftmost column.
                .ToList();

            if (icGroups.Count > 1) // Only draw blocks if there's more than one IC.
            {
                var blockHeight = Math.Max(
                    _services.GetResourceDouble("CanvasIcBlockMinHeight", 18.0),
                    fontSizeX + _services.GetResourceDouble("CanvasIcBlockHeightExtra", 8.0));
                var blockPadding = Math.Max(
                    _services.GetResourceDouble("CanvasIcBlockPadding", _services.GetResourceDouble("Space2", 2.0)),
                    fontSizeX * 0.35);
                // Position blocks above column labels.
                var blockTop = gridTop - labelMarginX - labelHeightX - blockPadding - blockHeight;
                if (blockTop < 0)
                {
                    return;
                }

                var fill = _services.GetResourceBrush("BrushCanvasIcBlockFill", Brushes.Transparent);
                var strokeColor = _services.GetResourceColor("ColorCanvasIcBlockStroke");
                var strokeWidth = Math.Max(_services.GetResourceDouble("Space1", 1.0), _services.GetResourceDouble("CanvasSelectionStrokeThickness", 1.5));
                var stroke = new Pen(_services.GetBrush(strokeColor), strokeWidth);
                var blockCornerRadius = Math.Max(_services.GetResourceDouble("Space2", 2.0), fontSizeX * 0.35);
                var labelInset = Math.Max(_services.GetResourceDouble("CanvasIcBlockLabelInset", 3.0), fontSizeX * 0.15);

                foreach (var group in icGroups)
                {
                    // Calculate the world X-range for this IC group.
                    var minX = group.Min(p => p.Bounds.MinX);
                    var maxX = group.Max(p => p.Bounds.MaxX);
                    // Convert world X-coordinates to screen X-coordinates.
                    var sx0 = _services.WorldToScreen(new Point2(minX, regBounds.MaxY)).X;
                    var sx1 = _services.WorldToScreen(new Point2(maxX, regBounds.MaxY)).X;
                    var left = Math.Min(sx0, sx1);
                    var width = Math.Max(2, Math.Abs(sx1 - sx0)); // Ensure minimum width.

                    var rect = new Rect(left, blockTop, width, blockHeight); // Rectangle for the IC block.
                    context.DrawRectangle(fill, stroke, new RoundedRect(rect, blockCornerRadius));

                    var label = $"IC{group.Key + 1}"; // IC label (1-indexed).
                    var layout = _services.GetAxisLabelLayout(label, fontSizeX);
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
                    _hits.Add(new PadCanvas.AxisLabelHit(PadCanvas.AxisLabelKind.Ic, group.Key, hitRect));
                }
            }
        }
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
