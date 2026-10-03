namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Reports whether ToFull-affected target diffs are covered by emitted canonical rows.
/// </summary>
public sealed record NotchToFullCoverageAudit(
    int BucketCount,
    int ExpectedTargetDiffCount,
    int CoveredTargetDiffCount,
    IReadOnlyList<NotchToFullCoverageGap> MissingGaps)
{
    public static NotchToFullCoverageAudit Empty { get; } = new(
        BucketCount: 0,
        ExpectedTargetDiffCount: 0,
        CoveredTargetDiffCount: 0,
        MissingGaps: Array.Empty<NotchToFullCoverageGap>());

    public int MissingTargetDiffCount => MissingGaps.Count;

    public bool HasExpectations => ExpectedTargetDiffCount > 0;

    public bool HasMissingCoverage => MissingTargetDiffCount > 0;
}

/// <summary>
/// Describes one missing ToFull target diff in a canonical source bucket.
/// </summary>
public sealed record NotchToFullCoverageGap(
    int IcIndex,
    int SourceDiffIndex,
    int TargetDiffIndex,
    IReadOnlyList<int> CadPadIds);
