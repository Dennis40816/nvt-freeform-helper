// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchCompensationCacheCountersTests
{
    [Fact]
    public void RecordHit_Called_CountsOnlyTheHit()
    {
        var counters = new NotchCompensationCacheCounters();

        counters.RecordHit();

        Assert.Equal((1L, 0L, 0L), (counters.HitCount, counters.MissCount, counters.InvalidationCount));
    }

    [Fact]
    public void RecordMiss_Called_CountsOnlyTheMiss()
    {
        var counters = new NotchCompensationCacheCounters();

        counters.RecordMiss();

        Assert.Equal((0L, 1L, 0L), (counters.HitCount, counters.MissCount, counters.InvalidationCount));
    }

    [Fact]
    public void RecordInvalidation_CalledOnce_CountsOnlyTheInvalidation()
    {
        var counters = new NotchCompensationCacheCounters();

        counters.RecordInvalidation(7);

        Assert.Equal((0L, 0L, 1L), (counters.HitCount, counters.MissCount, counters.InvalidationCount));
    }

    [Fact]
    public void RecordInvalidation_WithClearedCount_KeepsTheClearSize()
    {
        var counters = new NotchCompensationCacheCounters();

        counters.RecordInvalidation(7);

        Assert.Equal(7, counters.LastClearSize);
    }

    [Fact]
    public void RecordInvalidation_CalledTwice_CountsBothInvalidations()
    {
        var counters = new NotchCompensationCacheCounters();
        counters.RecordInvalidation(7);

        counters.RecordInvalidation(2);

        Assert.Equal(2L, counters.InvalidationCount);
    }

    [Fact]
    public void RecordInvalidation_CalledTwice_KeepsTheLatestClearSize()
    {
        var counters = new NotchCompensationCacheCounters();
        counters.RecordInvalidation(7);

        counters.RecordInvalidation(2);

        Assert.Equal(2, counters.LastClearSize);
    }

    [Fact]
    public void LastClearSize_NewInstance_IsZero()
    {
        var counters = new NotchCompensationCacheCounters();

        Assert.Equal(0, counters.LastClearSize);
    }
}
