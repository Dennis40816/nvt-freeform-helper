using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Lightweight per-CAD estimate of notch export eligibility under current settings.
/// </summary>
public readonly record struct NotchCadRowEligibility(
    int CadPadId,
    IReadOnlyList<NotchAlgorithmVersion> EligibleVersions,
    int EstimatedRowCount,
    int? AnchorRegularPadId,
    string Reason)
{
    public bool HasRows => EstimatedRowCount > 0;
}
