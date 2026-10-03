using Avalonia;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    /// <summary>
    /// Draws a single CAD pad onto the canvas, applying appropriate styling based on its state.
    /// </summary>
    private void DrawCadPad(
        DrawingContext context,
        CadPad pad,
        bool isSelected,
        IBrush cadFillDefault,
        IBrush cadFillSelected,
        IBrush cadFillOverlap,
        Pen cadPen,
        Pen cadPenSelected,
        Pen cadPenOverlap,
        bool lowDetailMode)
    {
        var isOverlapHighlighted = !isSelected && _highlightedCadIds.Contains(pad.Id);
        var suppressFillForNotchPreview = ShouldSuppressCadFillForNotchPreview(pad.Id);
        var requirePreciseGeometry = ShouldUsePreciseCadGeometry(pad.Id, isSelected, isOverlapHighlighted, lowDetailMode);

        IBrush fill = suppressFillForNotchPreview
            ? Brushes.Transparent
            : cadFillDefault;
        // If coloring by area is enabled, retrieve the area-based fill brush.
        if (!suppressFillForNotchPreview &&
            !isSelected &&
            ColorCadByArea &&
            _cadAreaBrushCache is not null &&
            _cadAreaBrushCache.TryGetValue(pad.Id, out var areaFill))
        {
            fill = areaFill;
        }
        if (!suppressFillForNotchPreview && !isSelected && isOverlapHighlighted)
        {
            fill = cadFillOverlap;
        }
        // If selected, use the selected fill brush.
        if (!suppressFillForNotchPreview && isSelected)
        {
            fill = cadFillSelected;
        }

        // Use the selected pen based on selection state.
        var pen = isSelected
            ? cadPenSelected
            : isOverlapHighlighted
                ? cadPenOverlap
                : cadPen;
        if (!requirePreciseGeometry)
        {
            var b = pad.Bounds;
            context.DrawRectangle(fill, pen, new Rect(b.MinX, b.MinY, b.Width, b.Height));
            return;
        }

        // Retrieve the cached geometry for the CAD pad.
        if (_cadGeometryCache is null || !_cadGeometryCache.TryGetValue(pad.Id, out var geometry))
        {
            return;
        }

        context.DrawGeometry(fill, pen, geometry); // Draw the CAD pad's geometry.
    }

    private bool ShouldSuppressCadFillForNotchPreview(int cadPadId)
    {
        return _notchPreviewByCadId.ContainsKey(cadPadId) &&
               (ShowNotchToFullSeedOverlay || ShowNotchToFullCandidateOverlay || ShowNotchToFullFinalOverlay);
    }

    private bool ShouldUsePreciseCadGeometry(int cadPadId, bool isSelected, bool isOverlapHighlighted, bool lowDetailMode)
    {
        if (!lowDetailMode)
        {
            return true;
        }

        return isSelected ||
               isOverlapHighlighted ||
               _notchPreviewByCadId.ContainsKey(cadPadId) ||
               _diffIndexOverrideCadIds.Contains(cadPadId) ||
               _diffIndexAnchorCadPadIds.Contains(cadPadId);
    }

    private Rect2 GetWorldViewport()
    {
        return ResolveViewFrameSnapshot().WorldViewport;
    }

    private void DrawDiffIndexOverlay(DrawingContext context, bool showCad, bool lowDetailMode, double cadOpacity)
    {
        if (!showCad || !ShowDiffIndexOverlay)
        {
            return;
        }

        if (_diffIndexOverrideCadIds.Count == 0 && _diffIndexAnchorCadPadIds.Count == 0)
        {
            return;
        }

        var highlightAlpha = ApplyOpacity(GetResourceAlpha("CanvasHighlightStrokeAlpha", 255), Math.Clamp(cadOpacity, 0.0, 1.0));
        var overrideColor = GetResourceColor("ColorCanvasDiffIdxOverride", GetResourceColor("ColorCanvasCadOverlapStroke"));
        var anchorColor = GetResourceColor("ColorCanvasDiffIdxAnchor", GetResourceColor("ColorCanvasCadSelected"));
        var highlightStrokeWidthAdjust = HighlightStrokeWidthAdjust;
        var overrideWidth = Math.Max(
            CadLineWidth + GetResourceDouble("CanvasDiffIdxOverrideStrokeWidthAdd", 1.2) + highlightStrokeWidthAdjust,
            CadLineWidth + 0.1);
        var anchorWidth = Math.Max(
            CadLineWidth + GetResourceDouble("CanvasDiffIdxAnchorStrokeWidthAdd", 1.8) + highlightStrokeWidthAdjust,
            CadLineWidth + 0.15);

        var overridePen = new Pen(
            GetBrush(Color.FromArgb(highlightAlpha, overrideColor.R, overrideColor.G, overrideColor.B)),
            overrideWidth);
        var anchorPen = new Pen(
            GetBrush(Color.FromArgb(highlightAlpha, anchorColor.R, anchorColor.G, anchorColor.B)),
            anchorWidth);

        foreach (var pad in _visibleCadUnselected)
        {
            DrawDiffIndexMarkerForPad(context, pad, overridePen, anchorPen, lowDetailMode);
        }

        foreach (var pad in _visibleCadSelected)
        {
            DrawDiffIndexMarkerForPad(context, pad, overridePen, anchorPen, lowDetailMode);
        }
    }

    private void DrawDiffIndexMarkerForPad(
        DrawingContext context,
        CadPad pad,
        Pen overridePen,
        Pen anchorPen,
        bool lowDetailMode)
    {
        var isOverride = _diffIndexOverrideCadIds.Contains(pad.Id);
        var isAnchor = _diffIndexAnchorCadPadIds.Contains(pad.Id);
        if (!isOverride && !isAnchor)
        {
            return;
        }

        if (lowDetailMode)
        {
            var b = pad.Bounds;
            var rect = new Rect(b.MinX, b.MinY, b.Width, b.Height);
            if (isAnchor)
            {
                context.DrawRectangle(null, anchorPen, rect);
            }

            if (isOverride)
            {
                context.DrawRectangle(null, overridePen, rect);
            }

            return;
        }

        if (_cadGeometryCache is null || !_cadGeometryCache.TryGetValue(pad.Id, out var geometry))
        {
            return;
        }

        if (isAnchor)
        {
            context.DrawGeometry(null, anchorPen, geometry);
        }

        if (isOverride)
        {
            context.DrawGeometry(null, overridePen, geometry);
        }
    }
}
