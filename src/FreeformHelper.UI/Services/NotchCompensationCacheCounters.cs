// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Counts notch compensation cache hits, misses and invalidations for the runtime metrics snapshot.</summary>
internal sealed class NotchCompensationCacheCounters
{
    // The three counters use Interlocked. LastClearSize is a plain value written before the invalidation increment.
    private long _hitCount;
    private long _missCount;
    private long _invalidationCount;

    internal long HitCount => Interlocked.Read(ref _hitCount);

    internal long MissCount => Interlocked.Read(ref _missCount);

    internal long InvalidationCount => Interlocked.Read(ref _invalidationCount);

    internal int LastClearSize { get; private set; }

    internal void RecordHit() => Interlocked.Increment(ref _hitCount);

    internal void RecordMiss() => Interlocked.Increment(ref _missCount);

    internal void RecordInvalidation(int clearedEntryCount)
    {
        LastClearSize = clearedEntryCount;
        Interlocked.Increment(ref _invalidationCount);
    }
}
