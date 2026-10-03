using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private bool HasActiveNotchStageOverlay()
    {
        return _notchPreviewByCadId.Count > 0 &&
               (ShowNotchToFullSeedOverlay || ShowNotchToFullCandidateOverlay || ShowNotchToFullFinalOverlay);
    }

    private bool ShouldUseBaseRegularStylingForNotchPreview()
    {
        return HasActiveNotchStageOverlay();
    }

    private bool ShouldSuppressFreeformAccentBorderForNotchPreview()
    {
        return HighlightFreeform && HasActiveNotchStageOverlay();
    }

    private bool ShouldSimplifyAggregateNotchPreviewVisuals()
    {
        return _notchPreviewByCadId.Count > 1 && HasActiveNotchStageOverlay();
    }

    private void DrawNotchToFullOverlay(DrawingContext context, bool showCad, double cadOpacity, double highlightOutlineWidth)
    {
        if (!showCad || !ShowNotchToFullFinalOverlay || _notchPreviewByCadId.Count == 0)
        {
            return;
        }

        var cadOpacityClamped = Math.Clamp(cadOpacity, 0.0, 1.0);
        var strokeColor = GetResourceColor("ColorNotchToFullOutline", Color.FromRgb(255, 159, 67));
        var fillColor = GetResourceColor("ColorNotchToFullFill", Color.FromArgb(77, 255, 159, 67));
        var fillOpacityScale = GetResourceDouble("CanvasNotchToFullFinalFillOpacity", 1.0);
        var strokeAlpha = ApplyOpacity(strokeColor.A, cadOpacityClamped);
        var fillAlpha = ApplyOpacity(ScaleAlpha(fillColor.A, fillOpacityScale), cadOpacityClamped);
        var strokeWidthAdd = GetResourceDouble("CanvasNotchToFullFinalStrokeWidthAdd", 0.0);
        var strokeWidth = Math.Max(
            highlightOutlineWidth + strokeWidthAdd,
            CadLineWidth + 0.1);
        var pen = new Pen(
            GetBrush(Color.FromArgb(strokeAlpha, strokeColor.R, strokeColor.G, strokeColor.B)),
            strokeWidth);
        var fill = fillAlpha == 0
            ? Brushes.Transparent
            : GetBrush(Color.FromArgb(fillAlpha, fillColor.R, fillColor.G, fillColor.B));

        var cadById = CadPads?
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First());
        foreach (var item in _notchPreviewByCadId.Values)
        {
            if (!item.IsToFullEnabled)
            {
                continue;
            }

            if (cadById is null || !cadById.TryGetValue(item.CadPadId, out _))
            {
                continue;
            }

            var finalPolygons = item.ToFullFinalOutlinePolygons.Count > 0
                ? item.ToFullFinalOutlinePolygons
                : item.ToFullPolygons;
            foreach (var polygon in finalPolygons)
            {
                if (polygon.Vertices.Length < 3 || polygon.Area() <= 1e-12)
                {
                    continue;
                }

                var geometry = BuildWorldGeometry(polygon);
                context.DrawGeometry(fill, pen, geometry);
            }
        }
    }

    private void DrawNotchToFullSeedOverlay(DrawingContext context, bool showCad, double cadOpacity, double highlightOutlineWidth)
    {
        if (!showCad || !ShowNotchToFullSeedOverlay || _notchPreviewByCadId.Count == 0)
        {
            return;
        }

        var cadOpacityClamped = Math.Clamp(cadOpacity, 0.0, 1.0);
        var strokeColor = GetResourceColor("ColorNotchToFullSeedOutline", Color.FromRgb(93, 226, 165));
        var fillColor = GetResourceColor("ColorNotchToFullSeedFill", Color.FromArgb(48, 93, 226, 165));
        var strokeAlpha = ApplyOpacity(strokeColor.A, cadOpacityClamped);
        var fillAlpha = ApplyOpacity(fillColor.A, cadOpacityClamped);
        var strokeWidthAdd = GetResourceDouble("CanvasNotchToFullSeedStrokeWidthAdd", 0.0);
        var pen = new Pen(
            GetBrush(Color.FromArgb(strokeAlpha, strokeColor.R, strokeColor.G, strokeColor.B)),
            Math.Max(highlightOutlineWidth + strokeWidthAdd, CadLineWidth + 0.1));
        var fill = fillAlpha == 0
            ? Brushes.Transparent
            : GetBrush(Color.FromArgb(fillAlpha, fillColor.R, fillColor.G, fillColor.B));

        foreach (var item in _notchPreviewByCadId.Values)
        {
            if (!item.IsToFullEnabled)
            {
                continue;
            }

            if (item.ToFullSeedPolygons.Count == 0)
            {
                continue;
            }

            foreach (var seedPolygon in item.ToFullSeedPolygons)
            {
                if (seedPolygon.Vertices.Length < 3 || seedPolygon.Area() <= 1e-12)
                {
                    continue;
                }

                var geometry = BuildWorldGeometry(seedPolygon);
                context.DrawGeometry(fill, pen, geometry);
            }
        }
    }

    private void DrawNotchToFullCandidateOverlay(DrawingContext context, bool showCad, double cadOpacity, double highlightOutlineWidth)
    {
        if (!showCad || !ShowNotchToFullCandidateOverlay || _notchToFullCandidatePolygons.Count == 0)
        {
            return;
        }

        var cadOpacityClamped = Math.Clamp(cadOpacity, 0.0, 1.0);
        var strokeColor = GetResourceColor("ColorNotchToFullCandidateOutline", Color.FromRgb(255, 209, 102));
        var fillColor = GetResourceColor("ColorNotchToFullCandidateFill", Color.FromArgb(51, 255, 209, 102));
        var strokeAlpha = ApplyOpacity(strokeColor.A, cadOpacityClamped);
        var fillAlpha = ApplyOpacity(fillColor.A, cadOpacityClamped);
        var strokeWidthAdd = GetResourceDouble("CanvasNotchToFullCandidateStrokeWidthAdd", 0.0);
        var stroke = new Pen(
            GetBrush(Color.FromArgb(strokeAlpha, strokeColor.R, strokeColor.G, strokeColor.B)),
            Math.Max(highlightOutlineWidth + strokeWidthAdd, CadLineWidth + 0.1));
        var fill = fillAlpha == 0
            ? Brushes.Transparent
            : GetBrush(Color.FromArgb(fillAlpha, fillColor.R, fillColor.G, fillColor.B));

        foreach (var polygon in _notchToFullCandidatePolygons)
        {
            if (polygon.Vertices.Length < 3 || polygon.Area() <= 1e-12)
            {
                continue;
            }

            var geometry = BuildWorldGeometry(polygon);
            context.DrawGeometry(fill, stroke, geometry);
        }
    }

    private void DrawNotchToRegularLabels(DrawingContext context, List<Rect> occupiedLabelRects)
    {
        if (!ShowNotchToRegularLabels || _notchPreviewByCadId.Count == 0 || CadPads is null || CadPads.Count == 0)
        {
            return;
        }

        var labelBrush = GetBrush(GetResourceColor("ColorCanvasNotchRatioLabel", GetResourceColor("ColorCanvasAxisLabel")));
        var backgroundBrush = GetBrush(GetResourceColor("ColorCanvasNotchRatioBackground", GetResourceColor("ColorCanvasMatchRatioBackground")));
        var borderBrush = GetBrush(GetResourceColor("ColorCanvasNotchRatioBorder", Color.FromRgb(255, 184, 107)));
        var cornerRadius = GetResourceDouble("CanvasNotchRatioCornerRadius", 6);
        var horizontalPadding = GetResourceDouble("CanvasNotchRatioPaddingX", 6);
        var verticalPadding = GetResourceDouble("CanvasNotchRatioPaddingY", 3);
        var offsetY = GetResourceDouble("CanvasNotchRatioOffsetY", 4);
        var minGap = Math.Max(0, GetResourceDouble("CanvasNotchRatioLabelMinGap", 2));
        var maxSearchRings = Math.Max(1, (int)Math.Round(GetResourceDouble("CanvasNotchRatioLabelMaxSearchRings", 8)));
        var fontSize = Math.Clamp(
            GetResourceDouble("CanvasNotchRatioFontSize", GetResourceDouble("FontSizeMd", 13)),
            GetResourceDouble("FontSizeMd", 13),
            GetResourceDouble("FontSizeLg", 15));
        var stroke = new Pen(borderBrush, GetResourceDouble("CanvasNotchRatioBorderThickness", 1));
        var viewport = new Rect(Bounds.Size);
        var orderedPads = CadPads
            .Where(pad => _notchPreviewByCadId.ContainsKey(pad.Id))
            .OrderByDescending(pad => _selectedCadIds.Contains(pad.Id))
            .ThenByDescending(static pad => pad.Area)
            .ThenBy(static pad => pad.Id);

        foreach (var pad in orderedPads)
        {
            var preview = _notchPreviewByCadId[pad.Id];

            var text = $"R {preview.ToRegularRatio * 100:0.#}%";
            var layout = new TextLayout(text, AxisLabelTypeface, fontSize, labelBrush);
            var labelWidth = layout.Width + (horizontalPadding * 2);
            var labelHeight = layout.Height + (verticalPadding * 2);
            var center = WorldToScreen(pad.Centroid);
            if (!TryResolveNotchRatioLabelRect(
                center,
                labelWidth,
                labelHeight,
                offsetY,
                minGap,
                maxSearchRings,
                viewport,
                occupiedLabelRects,
                out var rect))
            {
                continue;
            }

            context.DrawRectangle(backgroundBrush, stroke, new RoundedRect(rect, cornerRadius));
            layout.Draw(context, new Point(rect.X + horizontalPadding, rect.Y + verticalPadding));
            occupiedLabelRects.Add(rect.Inflate(minGap));
        }
    }

    private static bool TryResolveNotchRatioLabelRect(
        Point center,
        double labelWidth,
        double labelHeight,
        double offset,
        double minGap,
        int maxSearchRings,
        Rect viewport,
        IList<Rect> occupiedRects,
        out Rect resolvedRect,
        bool preferBelow = false)
    {
        resolvedRect = default;
        var topCandidate = new Rect(center.X - (labelWidth / 2.0), center.Y - (labelHeight + offset), labelWidth, labelHeight);
        var bottomCandidate = new Rect(center.X - (labelWidth / 2.0), center.Y + offset, labelWidth, labelHeight);
        var rightCandidate = new Rect(center.X + offset, center.Y - (labelHeight / 2.0), labelWidth, labelHeight);
        var leftCandidate = new Rect(center.X - (labelWidth + offset), center.Y - (labelHeight / 2.0), labelWidth, labelHeight);
        var candidates = preferBelow
            ? new[]
            {
                bottomCandidate,
                topCandidate,
                rightCandidate,
                leftCandidate,
            }
            : new[]
        {
            topCandidate,
            bottomCandidate,
            rightCandidate,
            leftCandidate,
        };

        foreach (var candidate in candidates)
        {
            var clamped = ClampToViewport(candidate, viewport);
            if (!IntersectsAny(clamped, occupiedRects))
            {
                resolvedRect = clamped;
                return true;
            }
        }

        var verticalStep = labelHeight + Math.Max(2.0, minGap);
        var horizontalStep = labelWidth + Math.Max(2.0, minGap);
        var baseRect = ClampToViewport(candidates[0], viewport);
        for (var ring = 1; ring <= maxSearchRings; ring++)
        {
            var offsets = new[]
            {
                new Vector(0, -verticalStep * ring),
                new Vector(0, verticalStep * ring),
                new Vector(horizontalStep * ring, 0),
                new Vector(-horizontalStep * ring, 0),
                new Vector(horizontalStep * ring, -verticalStep * ring),
                new Vector(-horizontalStep * ring, -verticalStep * ring),
                new Vector(horizontalStep * ring, verticalStep * ring),
                new Vector(-horizontalStep * ring, verticalStep * ring),
            };

            foreach (var offsetVector in offsets)
            {
                var shifted = ClampToViewport(
                    new Rect(
                        baseRect.X + offsetVector.X,
                        baseRect.Y + offsetVector.Y,
                        labelWidth,
                        labelHeight),
                    viewport);
                if (IntersectsAny(shifted, occupiedRects))
                {
                    continue;
                }

                resolvedRect = shifted;
                return true;
            }
        }

        return false;
    }

    private static bool IntersectsAny(Rect rect, IList<Rect> occupiedRects)
    {
        foreach (var occupied in occupiedRects)
        {
            if (rect.Intersects(occupied))
            {
                return true;
            }
        }

        return false;
    }

    private static Rect ClampToViewport(Rect rect, Rect viewport)
    {
        var minX = viewport.X;
        var minY = viewport.Y;
        var maxX = viewport.Right - rect.Width;
        var maxY = viewport.Bottom - rect.Height;
        var clampedX = Math.Clamp(rect.X, minX, Math.Max(minX, maxX));
        var clampedY = Math.Clamp(rect.Y, minY, Math.Max(minY, maxY));
        return new Rect(clampedX, clampedY, rect.Width, rect.Height);
    }
}
