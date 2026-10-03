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

        var lowColor = GetResourceColor("ColorSimulationScaleLow", Colors.DeepSkyBlue);
        var zeroColor = GetResourceColor("ColorSimulationScaleZero", Colors.LimeGreen);
        var midColor = GetResourceColor("ColorSimulationScaleMid", Colors.Gold);
        var warmColor = GetResourceColor("ColorSimulationScaleWarm", Colors.Orange);
        var highColor = GetResourceColor("ColorSimulationScaleHigh", Colors.IndianRed);
        var neutralColor = GetResourceColor("ColorSimulationScaleNeutral", Colors.Gray);
        var minOpacity = GetResourceDouble("NotchApplySimulationAaMinOpacity", 0.12d);
        var maxOpacity = GetResourceDouble("NotchApplySimulationAaMaxOpacity", 0.92d);
        var threshold = Math.Max(0d, SimulationRegularOverlayThresholdValue);

        if (SimulationRegularOverlayViewMode == ViewModels.NotchApplySimulationCanvasViewMode.ChangedOnly && !cell.IsChanged)
        {
            return GetBrush(Color.FromArgb(18, neutralColor.R, neutralColor.G, neutralColor.B));
        }

        var value = cell.DisplayValue;
        if (SimulationRegularOverlayViewMode is ViewModels.NotchApplySimulationCanvasViewMode.Before or ViewModels.NotchApplySimulationCanvasViewMode.After)
        {
            var useAutoAsymmetricScale = SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Auto;
            var signedRange = SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Threshold
                ? threshold
                : Math.Max(Math.Abs(_simulationOverlayMinValue), Math.Abs(_simulationOverlayMaxValue));
            var useSignedScale = SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Threshold &&
                                 _simulationOverlayMinValue < -1e-9;
            if (useSignedScale)
            {
                var normalizedSigned = SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Threshold
                    ? NormalizeThresholdScale(Math.Abs(value), threshold)
                    : signedRange <= 1e-9
                        ? 0d
                        : Math.Clamp(Math.Abs(value) / signedRange, 0d, 1d);
                var signedMixed = SimulationColorScaleResolver.ResolveSignedColor(value, signedRange, lowColor, zeroColor, midColor, warmColor, highColor);
                var signedOpacity = minOpacity + ((maxOpacity - minOpacity) * (0.18d + (0.82d * Math.Pow(normalizedSigned, 0.78d))));
                return BuildAlphaBrush(signedMixed, signedOpacity);
            }

            if (useAutoAsymmetricScale)
            {
                var positiveRange = Math.Max(_simulationOverlayAutoPositiveClamp, Math.Max(_simulationOverlayMaxValue, 0d));
                var negativeRange = Math.Max(_simulationOverlayAutoNegativeClampAbs, Math.Abs(Math.Min(_simulationOverlayMinValue, 0d)));
                if (value < 0d)
                {
                    var negativeNormalized = negativeRange <= 1e-9
                        ? 0d
                        : Math.Clamp(Math.Abs(value) / negativeRange, 0d, 1d);
                    var negativeMixed = SimulationColorScaleResolver.ResolveNegativeColor(value, negativeRange, lowColor, zeroColor);
                    var negativeOpacity = minOpacity + ((maxOpacity - minOpacity) * (0.22d + (0.78d * Math.Pow(negativeNormalized, 1.3d))));
                    return BuildAlphaBrush(negativeMixed, negativeOpacity);
                }

                var positiveNormalized = positiveRange <= 1e-9
                    ? 0d
                    : Math.Clamp(value / positiveRange, 0d, 1d);
                var positiveMixed = SimulationColorScaleResolver.ResolvePositiveColor(value, positiveRange, zeroColor, midColor, warmColor, highColor);
                var positiveOpacity = minOpacity + ((maxOpacity - minOpacity) * (0.28d + (0.72d * Math.Pow(positiveNormalized, 0.72d))));
                return BuildAlphaBrush(positiveMixed, positiveOpacity);
            }

            var sequentialRange = SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Threshold
                ? threshold
                : Math.Max(_simulationOverlayMaxValue, 0d);
            var sequentialNormalized = NormalizeThresholdScale(value, sequentialRange);
            var sequentialMixed = SimulationColorScaleResolver.ResolvePositiveColor(value, sequentialRange, zeroColor, midColor, warmColor, highColor);
            var sequentialOpacity = minOpacity + ((maxOpacity - minOpacity) * (0.34d + (0.66d * Math.Pow(sequentialNormalized, 0.78d))));
            return BuildAlphaBrush(sequentialMixed, sequentialOpacity);
        }

        var magnitude = Math.Abs(value);
        if (magnitude <= 1e-9)
        {
            return BuildAlphaBrush(zeroColor, minOpacity + ((maxOpacity - minOpacity) * 0.18d));
        }

        var deltaNormalized = SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Threshold
            ? NormalizeThresholdScale(magnitude, threshold)
            : _simulationOverlayMaxAbsDelta <= 1e-9
                ? 1d
                : Math.Clamp(magnitude / _simulationOverlayMaxAbsDelta, 0d, 1d);
        var deltaMixed = SimulationColorScaleResolver.ResolveSignedColor(
            value,
            SimulationRegularOverlayColorMode == ViewModels.NotchApplySimulationCanvasColorMode.Threshold ? threshold : _simulationOverlayMaxAbsDelta,
            lowColor,
            zeroColor,
            midColor,
            warmColor,
            highColor);
        var deltaOpacity = minOpacity + ((maxOpacity - minOpacity) * (0.30d + (0.70d * Math.Pow(deltaNormalized, 0.78d))));
        return BuildAlphaBrush(deltaMixed, deltaOpacity);
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
        var valueLayout = new TextLayout(cell.ValueText, AxisLabelTypeface, fontSize, valueBrush);
        var origin = new Point(
            rect.X + Math.Max(0d, (rect.Width - valueLayout.Width) / 2d),
            rect.Y + Math.Max(0d, (rect.Height - valueLayout.Height) / 2d));
        valueLayout.Draw(context, origin);
    }

    private IBrush BuildAlphaBrush(Color baseColor, double opacity)
    {
        var alpha = (byte)Math.Clamp(Math.Round(opacity * 255d), 0d, 255d);
        return GetBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
    }
    private static double NormalizeThresholdScale(double value, double threshold)
    {
        if (threshold <= 1e-9)
        {
            return Math.Abs(value) <= 1e-9 ? 0d : 1d;
        }

        return Math.Clamp(value / threshold, 0d, 1d);
    }
}
