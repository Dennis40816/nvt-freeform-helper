using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private void DrawMatchAllocationLabels(DrawingContext context, IList<Rect> occupiedLabelRects)
    {
        if (_selectedRegIdx.Count == 0 || _selectedCadIds.Count == 0)
        {
            return;
        }

        var labelBrush = GetBrush(GetResourceColor("ColorCanvasMatchRatioLabel", GetResourceColor("ColorCanvasAxisLabel")));
        var backgroundBrush = GetBrush(GetResourceColor("ColorCanvasMatchRatioBackground", Colors.Transparent));
        var borderBrush = GetBrush(GetResourceColor("ColorCanvasMatchRatioBorder", GetResourceColor("ColorCanvasCadSelected")));
        var cornerRadius = GetResourceDouble("CanvasMatchRatioCornerRadius", GetResourceDouble("RadiusSm", 6));
        var horizontalPadding = GetResourceDouble("CanvasMatchRatioPaddingX", 6);
        var verticalPadding = GetResourceDouble("CanvasMatchRatioPaddingY", 3);
        var offsetY = GetResourceDouble("CanvasMatchRatioOffsetY", 4);
        var minGap = Math.Max(
            0,
            GetResourceDouble("CanvasMatchRatioLabelMinGap", GetResourceDouble("CanvasNotchRatioLabelMinGap", 2)));
        var maxSearchRings = Math.Max(
            1,
            (int)Math.Round(
                GetResourceDouble("CanvasMatchRatioLabelMaxSearchRings", GetResourceDouble("CanvasNotchRatioLabelMaxSearchRings", 8))));
        var fontSize = Math.Clamp(
            GetResourceDouble("CanvasMatchRatioFontSize", GetResourceDouble("FontSizeSm", 11)),
            GetResourceDouble("FontSizeSm", 11),
            GetResourceDouble("FontSizeMd", 13));
        var stroke = new Pen(borderBrush, GetResourceDouble("CanvasMatchRatioBorderThickness", 1));
        var preferBelow = ShowNotchToRegularLabels && _notchPreviewByCadId.Count > 0;

        // CAD-centric: one CAD + many regulars -> show CAD coverage on regular pads.
        var drewCadCentric = false;
        if (_selectedCadIds.Count == 1 && _selectedRegIdx.Count >= 1)
        {
            drewCadCentric = DrawRegularCoverageLabels(
                context,
                _selectedCadIds.First(),
                labelBrush,
                backgroundBrush,
                stroke,
                cornerRadius,
                horizontalPadding,
                verticalPadding,
                offsetY,
                fontSize,
                preferBelow,
                minGap,
                maxSearchRings,
                occupiedLabelRects);
        }

        // Regular-centric: one regular + many CADs -> show regular coverage on CAD pads.
        if (!drewCadCentric && _selectedRegIdx.Count == 1 && _selectedCadIds.Count >= 1)
        {
            DrawCadCoverageLabels(
                context,
                _selectedRegIdx.First(),
                labelBrush,
                backgroundBrush,
                stroke,
                cornerRadius,
                horizontalPadding,
                verticalPadding,
                offsetY,
                fontSize,
                preferBelow,
                minGap,
                maxSearchRings,
                occupiedLabelRects);
        }
    }

    private bool DrawRegularCoverageLabels(
        DrawingContext context,
        int selectedCadId,
        IBrush labelBrush,
        IBrush backgroundBrush,
        Pen stroke,
        double cornerRadius,
        double horizontalPadding,
        double verticalPadding,
        double offsetY,
        double fontSize,
        bool preferBelow,
        double minGap,
        int maxSearchRings,
        IList<Rect> occupiedLabelRects)
    {
        if (RegularPads is null || RegularPads.Count == 0 || _matchLinksByRegularId.Count == 0)
        {
            return false;
        }

        var drew = false;

        foreach (var regularId in _selectedRegIdx)
        {
            if (!_matchLinksByRegularId.TryGetValue(regularId, out var links) || links.Count == 0)
            {
                continue;
            }

            var selectedCoverage = links
                .Where(link => link.CadPadId == selectedCadId)
                .Sum(link => link.CadCoverage);
            if (selectedCoverage <= 0)
            {
                continue;
            }

            var regularPad = RegularPads.FirstOrDefault(pad => pad.RegularPadId == regularId);
            if (regularPad is null)
            {
                continue;
            }

            if (DrawMatchRatioLabel(
                    context,
                    GetBoundsCenter(regularPad.Bounds),
                    selectedCoverage,
                    labelBrush,
                    backgroundBrush,
                    stroke,
                    cornerRadius,
                    horizontalPadding,
                    verticalPadding,
                    offsetY,
                    fontSize,
                    preferBelow,
                    minGap,
                    maxSearchRings,
                    occupiedLabelRects))
            {
                drew = true;
            }
        }

        return drew;
    }

    private void DrawCadCoverageLabels(
        DrawingContext context,
        int selectedRegularId,
        IBrush labelBrush,
        IBrush backgroundBrush,
        Pen stroke,
        double cornerRadius,
        double horizontalPadding,
        double verticalPadding,
        double offsetY,
        double fontSize,
        bool preferBelow,
        double minGap,
        int maxSearchRings,
        IList<Rect> occupiedLabelRects)
    {
        if (CadPads is null || CadPads.Count == 0 || _matchLinksByCadId.Count == 0)
        {
            return;
        }

        foreach (var cadId in _selectedCadIds)
        {
            if (!_matchLinksByCadId.TryGetValue(cadId, out var links) || links.Count == 0)
            {
                continue;
            }

            var selectedCoverage = links
                .Where(link => link.RegularPadId == selectedRegularId)
                .Sum(link => link.RegularCoverage);
            if (selectedCoverage <= 0)
            {
                continue;
            }

            var cadPad = CadPads.FirstOrDefault(pad => pad.Id == cadId);
            if (cadPad is null)
            {
                continue;
            }

            DrawMatchRatioLabel(
                context,
                GetBoundsCenter(cadPad.Bounds),
                selectedCoverage,
                labelBrush,
                backgroundBrush,
                stroke,
                cornerRadius,
                horizontalPadding,
                verticalPadding,
                offsetY,
                fontSize,
                preferBelow,
                minGap,
                maxSearchRings,
                occupiedLabelRects);
        }
    }

    private bool DrawMatchRatioLabel(
        DrawingContext context,
        Point2 worldCenter,
        double coverage,
        IBrush labelBrush,
        IBrush backgroundBrush,
        Pen stroke,
        double cornerRadius,
        double horizontalPadding,
        double verticalPadding,
        double offsetY,
        double fontSize,
        bool preferBelow,
        double minGap,
        int maxSearchRings,
        IList<Rect> occupiedLabelRects)
    {
        var text = $"M {coverage * 100:0.#}%";
        var layout = new TextLayout(
            text,
            AxisLabelTypeface,
            fontSize,
            labelBrush);

        var labelWidth = layout.Width + (horizontalPadding * 2);
        var labelHeight = layout.Height + (verticalPadding * 2);
        var center = WorldToScreen(worldCenter);
        var viewport = new Rect(Bounds.Size);
        if (!TryResolveNotchRatioLabelRect(
                center,
                labelWidth,
                labelHeight,
                offsetY,
                minGap,
                maxSearchRings,
                viewport,
                occupiedLabelRects,
                out var rect,
                preferBelow))
        {
            return false;
        }

        context.DrawRectangle(backgroundBrush, stroke, new RoundedRect(rect, cornerRadius));
        layout.Draw(context, new Point(rect.X + horizontalPadding, rect.Y + verticalPadding));
        occupiedLabelRects.Add(rect.Inflate(minGap));
        return true;
    }
}
