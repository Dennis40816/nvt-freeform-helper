namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Captures elapsed milliseconds for the main notch generation phases.
/// </summary>
public sealed record NotchGenerationPhaseTimings(
    long BuildProfilesElapsedMs,
    long BuildCanonicalCandidatesElapsedMs,
    long MergeCanonicalCandidatesElapsedMs,
    long BuildLegacyRowsElapsedMs,
    long FinalizeCanonicalExportsElapsedMs,
    NotchCandidatePhaseTimings? CandidateBreakdown = null)
{
    public static NotchGenerationPhaseTimings Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        NotchCandidatePhaseTimings.Empty);

    public long TotalElapsedMs =>
        BuildProfilesElapsedMs +
        BuildCanonicalCandidatesElapsedMs +
        MergeCanonicalCandidatesElapsedMs +
        BuildLegacyRowsElapsedMs +
        FinalizeCanonicalExportsElapsedMs;

    public bool HasData => TotalElapsedMs > 0;
}
