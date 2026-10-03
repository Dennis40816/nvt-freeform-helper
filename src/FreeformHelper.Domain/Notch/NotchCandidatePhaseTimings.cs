namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Captures elapsed milliseconds inside the canonical candidate phase.
/// </summary>
public sealed record NotchCandidatePhaseTimings(
    int CandidateCount,
    long BuildV22CadCandidateElapsedMs,
    long CompensationComputeElapsedMs,
    NotchV22CompensationStageTimings CompensationStageTimings)
{
    public static NotchCandidatePhaseTimings Empty { get; } = new(
        CandidateCount: 0,
        BuildV22CadCandidateElapsedMs: 0,
        CompensationComputeElapsedMs: 0,
        CompensationStageTimings: NotchV22CompensationStageTimings.Empty);

    public long CandidatePostComputeElapsedMs => Math.Max(0, BuildV22CadCandidateElapsedMs - CompensationComputeElapsedMs);

    public bool HasData =>
        CandidateCount > 0 ||
        BuildV22CadCandidateElapsedMs > 0 ||
        CompensationComputeElapsedMs > 0 ||
        CompensationStageTimings.HasData;
}
