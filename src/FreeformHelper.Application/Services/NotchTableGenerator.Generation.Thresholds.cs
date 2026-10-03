using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{
    private static bool PassesThresholdForVersion(NotchAlgorithmVersion version, double maxRatio, NotchSettings notch)
        => PassesThresholdForVersion(
            version,
            maxRatio,
            notch.ThresholdQ7,
            ResolveEffectiveV22ThresholdPercent(notch));

    private static bool PassesThresholdForVersion(
        NotchAlgorithmVersion version,
        double maxRatio,
        LegacyNotchGenerationRequest request)
        => PassesThresholdForVersion(
            version,
            maxRatio,
            request.ThresholdQ7,
            request.EffectiveV22ThresholdPercent);

    private static bool PassesThresholdForVersion(
        NotchAlgorithmVersion version,
        double maxRatio,
        int thresholdQ7,
        double effectiveV22ThresholdPercent)
    {
        var ratio = Math.Max(0.0, maxRatio);
        return version switch
        {
            NotchAlgorithmVersion.V22 => PassesV22ThresholdPercent(ratio, effectiveV22ThresholdPercent),
            _ => PassesV21ThresholdQ7(ratio, thresholdQ7),
        };
    }

    private static bool PassesV21ThresholdQ7(double ratio, int thresholdQ7)
    {
        if (thresholdQ7 <= 0)
        {
            return true;
        }

        var q7 = NotchThresholdQ7Contract.EncodeFraction(ratio);
        return q7 >= thresholdQ7;
    }

    private static double ResolveEffectiveV22ThresholdPercent(NotchSettings notch)
    {
        var thresholdPercent = notch.LinkVersionThresholds
            ? (notch.ThresholdQ7 * 100.0 / 128.0)
            : notch.ThresholdPercentV22;
        return Math.Clamp(thresholdPercent, 0.0, 100.0);
    }

    private static bool PassesV22ThresholdPercent(double ratio, double normalizedThresholdPercent)
    {
        if (normalizedThresholdPercent <= 0.0)
        {
            return true;
        }

        return (ratio * 100.0) >= normalizedThresholdPercent;
    }
}
