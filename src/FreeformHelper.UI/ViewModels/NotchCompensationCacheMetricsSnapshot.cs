namespace FreeformHelper.UI.ViewModels;

public sealed record NotchCompensationCacheMetricsSnapshot(
    int Revision,
    int EntryCount,
    int LastClearSize,
    long HitCount,
    long MissCount,
    long InvalidationCount,
    double HitRate);
