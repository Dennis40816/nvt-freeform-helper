namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Captures elapsed milliseconds for Notch v2.2 Stage C subphases.
/// </summary>
public sealed record NotchV22StageCSubphaseTimings(
    long OwnerAndBlockerLookupElapsedMs,
    long ExactOccupiedAreaElapsedMs,
    long SourceCoverageElapsedMs,
    long ReachabilityElapsedMs,
    long RuleDecisionElapsedMs,
    long PreviewAndDebugElapsedMs)
{
    public static NotchV22StageCSubphaseTimings Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public long TotalElapsedMs =>
        OwnerAndBlockerLookupElapsedMs +
        ExactOccupiedAreaElapsedMs +
        SourceCoverageElapsedMs +
        ReachabilityElapsedMs +
        RuleDecisionElapsedMs +
        PreviewAndDebugElapsedMs;

    public bool HasData => TotalElapsedMs > 0;
}
