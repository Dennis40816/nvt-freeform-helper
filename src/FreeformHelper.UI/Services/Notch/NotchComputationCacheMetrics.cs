// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Reports the state and counters of the resolved Notch cache.</summary>
internal sealed record NotchComputationCacheMetrics(
    int Revision,
    int EntryCount,
    int LastClearSize,
    long HitCount,
    long MissCount,
    long InvalidationCount);
