namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Captures elapsed milliseconds for Notch v2.2 compensation stages.
/// </summary>
public sealed record NotchV22CompensationStageTimings(
    long StageAElapsedMs,
    long StageBElapsedMs,
    long StageCElapsedMs,
    long StageDElapsedMs,
    NotchV22StageCSubphaseTimings? StageCBreakdown = null)
{
    public static NotchV22CompensationStageTimings Empty { get; } = new(0, 0, 0, 0, NotchV22StageCSubphaseTimings.Empty);

    public long TotalElapsedMs => StageAElapsedMs + StageBElapsedMs + StageCElapsedMs + StageDElapsedMs;

    public bool HasData => TotalElapsedMs > 0;
}
