using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private bool HasActiveSimulationRegularOverlay()
    {
        return ShowSimulationRegularOverlay && _simulationOverlayByRegularPadId.Count > 0;
    }

    private IBrush? TryBuildSimulationRegularFillBrush(RegularPad pad)
    {
        if (!HasActiveSimulationRegularOverlay() || !_simulationOverlayByRegularPadId.TryGetValue(pad.RegularPadId, out var cell))
        {
            return null;
        }

        var builder = new SimulationRegularFillBrushBuilder(new SimulationRegularFillBrushBuilder.Services(
            GetResourceColor,
            GetResourceDouble,
            GetBrush));
        return builder.Build(cell.DisplayValue, cell.IsChanged, new SimulationRegularFillBrushBuilder.Scale(
            SimulationRegularOverlayViewMode == ViewModels.NotchApplySimulationCanvasViewMode.ChangedOnly,
            SimulationRegularOverlayViewMode is ViewModels.NotchApplySimulationCanvasViewMode.Before or ViewModels.NotchApplySimulationCanvasViewMode.After,
            SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Auto,
            SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Threshold,
            SimulationRegularOverlayThresholdValue,
            _simulationOverlayMinValue,
            _simulationOverlayMaxValue,
            _simulationOverlayMaxAbsDelta,
            _simulationOverlayAutoNegativeClampAbs,
            _simulationOverlayAutoPositiveClamp));
    }

    private void DrawSimulationRegularLabels(DrawingContext context)
    {
        if (!HasActiveSimulationRegularOverlay() || RegularPads is null || RegularPads.Count == 0)
        {
            return;
        }

        var valueFontMin = GetResourceDouble("NotchApplySimulationAaValueFontMin", 8d);
        var valueFontMax = GetResourceDouble("NotchApplySimulationAaValueFontMax", 15d);
        var textBrush = GetResourceBrush("BrushTextPrimary", Brushes.White);
        var visiblePads = _visibleRegularUnselected.Concat(_visibleRegularSelected);

        foreach (var pad in visiblePads)
        {
            if (!_simulationOverlayByRegularPadId.TryGetValue(pad.RegularPadId, out var cell))
            {
                continue;
            }

            if (SimulationRegularOverlayViewMode == ViewModels.NotchApplySimulationCanvasViewMode.ChangedOnly && !cell.IsChanged)
            {
                continue;
            }

            var topLeft = WorldToScreen(new Point2(pad.Bounds.MinX, pad.Bounds.MaxY));
            var bottomRight = WorldToScreen(new Point2(pad.Bounds.MaxX, pad.Bounds.MinY));
            var rect = new Rect(topLeft, bottomRight).Normalize();
            if (rect.Width < 18d || rect.Height < 14d)
            {
                continue;
            }

            DrawSimulationCellText(
                context,
                cell,
                rect,
                textBrush,
                valueFontMin,
                valueFontMax);
        }
    }

    private void DrawSimulationCopperPillar(DrawingContext context)
    {
        if (!ShowSimulationCopperPillar || SimulationCopperDiameter <= 1e-9)
        {
            return;
        }

        var center = WorldToScreen(new Point2(SimulationCopperCenterX, SimulationCopperCenterY));
        var radiusWorld = SimulationCopperDiameter * 0.5d;
        var radiusEdge = WorldToScreen(new Point2(SimulationCopperCenterX + radiusWorld, SimulationCopperCenterY));
        var radiusScreen = Math.Abs(radiusEdge.X - center.X);
        if (radiusScreen <= 1e-9)
        {
            return;
        }

        var fill = GetResourceBrush("BrushSimulationCopperFill", Brushes.Transparent);
        var haloPen = new Pen(
            GetResourceBrush("BrushSimulationCopperHalo", Brushes.DeepSkyBlue),
            GetResourceDouble("NotchApplySimulationCopperHaloStrokeThickness", 5d));
        var rimPen = new Pen(
            GetResourceBrush("BrushSimulationCopperRim", Brushes.DeepSkyBlue),
            GetResourceDouble("NotchApplySimulationCopperRimStrokeThickness", 2d));
        var centerPen = new Pen(
            GetResourceBrush("BrushSimulationCopperCenter", Brushes.White),
            GetResourceDouble("NotchApplySimulationCopperCrossStrokeThickness", 1.5d));

        context.DrawEllipse(null, haloPen, center, radiusScreen, radiusScreen);
        context.DrawEllipse(fill, rimPen, center, radiusScreen, radiusScreen);

        var crossHalf = Math.Min(radiusScreen, GetResourceDouble("NotchApplySimulationCopperCrossHalfLength", 13d));
        context.DrawLine(centerPen, center + new Vector(-crossHalf, 0d), center + new Vector(crossHalf, 0d));
        context.DrawLine(centerPen, center + new Vector(0d, -crossHalf), center + new Vector(0d, crossHalf));

        var centerRadius = Math.Min(radiusScreen * 0.28d, GetResourceDouble("NotchApplySimulationCopperCenterRadius", 3.5d));
        context.DrawEllipse(GetResourceBrush("BrushSimulationCopperCenter", Brushes.White), null, center, centerRadius, centerRadius);
    }

    private static void DrawSimulationCellText(
        DrawingContext context,
        ViewModels.NotchApplySimulationAaDisplayCell cell,
        Rect rect,
        IBrush valueBrush,
        double valueFontMin,
        double valueFontMax)
    {
        if (string.IsNullOrWhiteSpace(cell.ValueText))
        {
            return;
        }

        var fontSize = Math.Clamp(Math.Min(rect.Width / 4.6d, rect.Height / 1.8d), valueFontMin, valueFontMax);
        using var valueLayout = new TextLayout(cell.ValueText, AxisLabelTypeface, fontSize, valueBrush);
        var origin = new Point(
            rect.X + Math.Max(0d, (rect.Width - valueLayout.Width) / 2d),
            rect.Y + Math.Max(0d, (rect.Height - valueLayout.Height) / 2d));
        valueLayout.Draw(context, origin);
    }
}
