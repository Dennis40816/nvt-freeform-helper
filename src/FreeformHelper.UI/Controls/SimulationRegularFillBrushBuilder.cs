using Avalonia.Media;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Builds the fill brush of one regular pad in the simulation overlay of a <see cref="PadCanvas"/>.
/// It gets theme resources and the brush cache from the canvas through <see cref="Services"/>.
/// </summary>
internal sealed class SimulationRegularFillBrushBuilder
{
    private readonly Services _services;

    public SimulationRegularFillBrushBuilder(Services services)
    {
        _services = services;
    }

    /// <summary>
    /// The canvas operations the builder needs. The canvas passes its own methods in.
    /// </summary>
    internal sealed record Services(
        Func<string, Color, Color> GetResourceColor,
        Func<string, double, double> GetResourceDouble,
        Func<Color, ISolidColorBrush> GetBrush);

    /// <summary>
    /// The view mode, the color mode, the threshold and the value ranges of the overlay.
    /// </summary>
    internal readonly record struct Scale(
        bool IsChangedOnlyView,
        bool IsBeforeAfterView,
        bool IsAutoColorMode,
        bool IsThresholdColorMode,
        double ThresholdValue,
        double MinValue,
        double MaxValue,
        double MaxAbsDelta,
        double AutoNegativeClampAbs,
        double AutoPositiveClamp);

    private readonly record struct Palette(
        Color LowColor,
        Color ZeroColor,
        Color MidColor,
        Color WarmColor,
        Color HighColor,
        double MinOpacity,
        double MaxOpacity,
        double Threshold);

    public IBrush Build(double value, bool isChanged, Scale scale)
    {
        var lowColor = _services.GetResourceColor("ColorSimulationScaleLow", Colors.DeepSkyBlue);
        var zeroColor = _services.GetResourceColor("ColorSimulationScaleZero", Colors.LimeGreen);
        var midColor = _services.GetResourceColor("ColorSimulationScaleMid", Colors.Gold);
        var warmColor = _services.GetResourceColor("ColorSimulationScaleWarm", Colors.Orange);
        var highColor = _services.GetResourceColor("ColorSimulationScaleHigh", Colors.IndianRed);
        var neutralColor = _services.GetResourceColor("ColorSimulationScaleNeutral", Colors.Gray);
        var minOpacity = _services.GetResourceDouble("NotchApplySimulationAaMinOpacity", 0.12d);
        var maxOpacity = _services.GetResourceDouble("NotchApplySimulationAaMaxOpacity", 0.92d);
        var threshold = Math.Max(0d, scale.ThresholdValue);
        var palette = new Palette(lowColor, zeroColor, midColor, warmColor, highColor, minOpacity, maxOpacity, threshold);

        if (scale.IsChangedOnlyView && !isChanged)
        {
            return _services.GetBrush(Color.FromArgb(18, neutralColor.R, neutralColor.G, neutralColor.B));
        }

        if (scale.IsBeforeAfterView)
        {
            return BuildBeforeAfterBrush(value, scale, palette);
        }

        return BuildDeltaBrush(value, scale, palette);
    }

    private IBrush BuildBeforeAfterBrush(double value, Scale scale, Palette palette)
    {
        var (lowColor, zeroColor, midColor, warmColor, highColor, minOpacity, maxOpacity, threshold) = palette;
        var useAutoAsymmetricScale = scale.IsAutoColorMode;
        var signedRange = scale.IsThresholdColorMode
            ? threshold
            : Math.Max(Math.Abs(scale.MinValue), Math.Abs(scale.MaxValue));
        var useSignedScale = scale.IsThresholdColorMode &&
                             scale.MinValue < -1e-9;
        if (useSignedScale)
        {
            var normalizedSigned = scale.IsThresholdColorMode
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
            var positiveRange = Math.Max(scale.AutoPositiveClamp, Math.Max(scale.MaxValue, 0d));
            var negativeRange = Math.Max(scale.AutoNegativeClampAbs, Math.Abs(Math.Min(scale.MinValue, 0d)));
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

        var sequentialRange = scale.IsThresholdColorMode
            ? threshold
            : Math.Max(scale.MaxValue, 0d);
        var sequentialNormalized = NormalizeThresholdScale(value, sequentialRange);
        var sequentialMixed = SimulationColorScaleResolver.ResolvePositiveColor(value, sequentialRange, zeroColor, midColor, warmColor, highColor);
        var sequentialOpacity = minOpacity + ((maxOpacity - minOpacity) * (0.34d + (0.66d * Math.Pow(sequentialNormalized, 0.78d))));
        return BuildAlphaBrush(sequentialMixed, sequentialOpacity);
    }

    private IBrush BuildDeltaBrush(double value, Scale scale, Palette palette)
    {
        var (lowColor, zeroColor, midColor, warmColor, highColor, minOpacity, maxOpacity, threshold) = palette;
        var magnitude = Math.Abs(value);
        if (magnitude <= 1e-9)
        {
            return BuildAlphaBrush(zeroColor, minOpacity + ((maxOpacity - minOpacity) * 0.18d));
        }

        var deltaNormalized = scale.IsThresholdColorMode
            ? NormalizeThresholdScale(magnitude, threshold)
            : scale.MaxAbsDelta <= 1e-9
                ? 1d
                : Math.Clamp(magnitude / scale.MaxAbsDelta, 0d, 1d);
        var deltaMixed = SimulationColorScaleResolver.ResolveSignedColor(
            value,
            scale.IsThresholdColorMode ? threshold : scale.MaxAbsDelta,
            lowColor,
            zeroColor,
            midColor,
            warmColor,
            highColor);
        var deltaOpacity = minOpacity + ((maxOpacity - minOpacity) * (0.30d + (0.70d * Math.Pow(deltaNormalized, 0.78d))));
        return BuildAlphaBrush(deltaMixed, deltaOpacity);
    }

    private IBrush BuildAlphaBrush(Color baseColor, double opacity)
    {
        var alpha = (byte)Math.Clamp(Math.Round(opacity * 255d), 0d, 255d);
        return _services.GetBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
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
