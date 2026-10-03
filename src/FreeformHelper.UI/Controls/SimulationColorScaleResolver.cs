using Avalonia.Media;

namespace FreeformHelper.UI.Controls;

internal static class SimulationColorScaleResolver
{
    private const double AutoClampPercentile = 0.85d;
    private const double PositiveGreenBand = 0.18d;
    private const double PositiveWarmPivot = 0.58d;
    private const double NegativeGreenBand = 0.32d;

    internal readonly record struct AutoScaleRange(
        double Minimum,
        double Maximum,
        double NegativeClampAbs,
        double PositiveClamp);

    public static AutoScaleRange ComputeAutoScaleRange(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materialized = values.ToArray();
        if (materialized.Length == 0)
        {
            return new AutoScaleRange(0d, 0d, 0d, 0d);
        }

        var minimum = materialized.Min();
        var maximum = materialized.Max();
        var positive = materialized.Where(static value => value > 1e-9).OrderBy(static value => value).ToArray();
        var negativeAbs = materialized.Where(static value => value < -1e-9).Select(static value => Math.Abs(value)).OrderBy(static value => value).ToArray();

        return new AutoScaleRange(
            minimum,
            maximum,
            ComputePercentile(negativeAbs, AutoClampPercentile),
            ComputePercentile(positive, AutoClampPercentile));
    }

    public static Color ResolvePositiveColor(
        double value,
        double max,
        Color zeroColor,
        Color midColor,
        Color warmColor,
        Color highColor)
    {
        if (max <= 1e-9 || value <= 1e-9)
        {
            return zeroColor;
        }

        var normalized = Math.Clamp(value / max, 0d, 1d);
        if (normalized <= PositiveGreenBand)
        {
            return zeroColor;
        }

        var activeRange = (normalized - PositiveGreenBand) / (1d - PositiveGreenBand);
        var remapped = Math.Pow(activeRange, 0.72d);
        if (remapped <= 0.42d)
        {
            return Lerp(zeroColor, midColor, remapped / 0.42d);
        }

        if (remapped <= PositiveWarmPivot)
        {
            return Lerp(midColor, warmColor, (remapped - 0.42d) / (PositiveWarmPivot - 0.42d));
        }

        return Lerp(warmColor, highColor, (remapped - PositiveWarmPivot) / (1d - PositiveWarmPivot));
    }

    public static Color ResolveNegativeColor(
        double value,
        double maxAbs,
        Color lowColor,
        Color zeroColor)
    {
        if (maxAbs <= 1e-9 || value >= -1e-9)
        {
            return zeroColor;
        }

        var normalized = Math.Clamp(Math.Abs(value) / maxAbs, 0d, 1d);
        if (normalized <= NegativeGreenBand)
        {
            return zeroColor;
        }

        var activeRange = (normalized - NegativeGreenBand) / (1d - NegativeGreenBand);
        var remapped = Math.Pow(activeRange, 1.35d);
        return Lerp(zeroColor, lowColor, remapped);
    }

    public static Color ResolveSequentialColor(
        double normalized,
        Color lowColor,
        Color zeroColor,
        Color midColor,
        Color warmColor,
        Color highColor)
    {
        var t = Math.Clamp(normalized, 0d, 1d);
        if (t <= 0.38d)
        {
            return Lerp(lowColor, zeroColor, t / 0.38d);
        }

        if (t <= 0.62d)
        {
            return Lerp(zeroColor, midColor, (t - 0.38d) / 0.24d);
        }

        if (t <= 0.82d)
        {
            return Lerp(midColor, warmColor, (t - 0.62d) / 0.20d);
        }

        return Lerp(warmColor, highColor, (t - 0.82d) / 0.18d);
    }

    public static Color ResolveSignedColor(
        double value,
        double maxAbs,
        Color lowColor,
        Color zeroColor,
        Color midColor,
        Color warmColor,
        Color highColor)
    {
        if (maxAbs <= 1e-9 || Math.Abs(value) <= 1e-9)
        {
            return zeroColor;
        }

        var normalized = Math.Clamp(Math.Abs(value) / maxAbs, 0d, 1d);
        var remapped = Math.Pow(normalized, 0.55d);

        if (value < 0d)
        {
            return Lerp(zeroColor, lowColor, remapped);
        }

        if (remapped <= 0.42d)
        {
            return Lerp(zeroColor, midColor, remapped / 0.42d);
        }

        if (remapped <= 0.76d)
        {
            return Lerp(midColor, warmColor, (remapped - 0.42d) / 0.34d);
        }

        return Lerp(warmColor, highColor, (remapped - 0.76d) / 0.24d);
    }

    private static Color Lerp(Color from, Color to, double t)
    {
        var amount = Math.Clamp(t, 0d, 1d);
        return Color.FromArgb(
            (byte)Math.Round(from.A + ((to.A - from.A) * amount)),
            (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
            (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
            (byte)Math.Round(from.B + ((to.B - from.B) * amount)));
    }

    private static double ComputePercentile(double[] values, double percentile)
    {
        if (values.Length == 0)
        {
            return 0d;
        }

        if (values.Length == 1)
        {
            return values[0];
        }

        var clampedPercentile = Math.Clamp(percentile, 0d, 1d);
        var position = (values.Length - 1) * clampedPercentile;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return values[lowerIndex];
        }

        var weight = position - lowerIndex;
        return values[lowerIndex] + ((values[upperIndex] - values[lowerIndex]) * weight);
    }
}
