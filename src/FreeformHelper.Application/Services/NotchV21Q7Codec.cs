namespace FreeformHelper.Application.Services;

internal static class NotchV21Q7Codec
{
    internal const int Scale = 128;
    internal const int TypeNone = 0;
    internal const int TypeAdd = 1;
    internal const int TypeSub = 2;

    internal static int NormalizeMagnitude(int magnitudeQ7)
    {
        return Math.Clamp(magnitudeQ7, 0, byte.MaxValue);
    }

    internal static int EncodePercentMagnitude(int ratioPercent)
    {
        var magnitudePercent = Math.Abs((long)ratioPercent);
        var encoded = Math.Round(
            magnitudePercent * Scale / 100.0,
            MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(encoded, 0.0, byte.MaxValue);
    }

    internal static int EncodeLegacyRowFractionRaw(double fraction)
    {
        if (!double.IsFinite(fraction) || fraction <= 0.0)
        {
            return 0;
        }

        var encoded = Math.Round(fraction * Scale, MidpointRounding.AwayFromZero);
        return encoded >= int.MaxValue ? int.MaxValue : (int)encoded;
    }

    internal static int DecodeMagnitudePercent(int magnitudeQ7)
    {
        return (int)Math.Round(
            NormalizeMagnitude(magnitudeQ7) * 100.0 / Scale,
            MidpointRounding.AwayFromZero);
    }

    internal static int DecodeSignedPercent(int type, int magnitudeQ7)
    {
        var magnitudePercent = DecodeMagnitudePercent(magnitudeQ7);
        return type switch
        {
            TypeAdd => magnitudePercent,
            TypeSub => -magnitudePercent,
            _ => 0,
        };
    }

    internal static int ScaleSigned(int sourceValue, int type, int magnitudeQ7)
    {
        var scaledMagnitude = (sourceValue * NormalizeMagnitude(magnitudeQ7)) >> 7;
        return type switch
        {
            TypeAdd => scaledMagnitude,
            TypeSub => -scaledMagnitude,
            _ => 0,
        };
    }
}
