namespace FreeformHelper.Application.Services;

internal static class NotchThresholdQ7Contract
{
    internal const int Scale = 128;

    internal static int EncodeFraction(double fraction)
    {
        if (!double.IsFinite(fraction) || fraction <= 0.0)
        {
            return 0;
        }

        var encoded = Math.Round(fraction * Scale, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(encoded, 0.0, Scale);
    }
}
