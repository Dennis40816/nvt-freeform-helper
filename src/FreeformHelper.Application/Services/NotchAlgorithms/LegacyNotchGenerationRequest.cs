using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

internal sealed record LegacyNotchGenerationRequest(
    IReadOnlyList<NotchAlgorithmVersion> EnabledVersions,
    int LenScale,
    int NullValue,
    int ThresholdQ7,
    double EffectiveV22ThresholdPercent);
