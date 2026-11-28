// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Verifies the shared stale-result counters.
/// </summary>
public sealed class AsyncGenerationTests
{
    [Fact]
    public void Read_NewInstance_StartsAtZero()
    {
        Assert.Equal(0, new AsyncGeneration().Read());
    }

    [Fact]
    public void Next_ReturnsEachNewValueOnceAndReadFollowsIt()
    {
        var generation = new AsyncGeneration();

        Assert.Equal(1, generation.Next());
        Assert.Equal(2, generation.Next());
        Assert.Equal(2, generation.Read());
    }

    [Fact]
    public void Next_ConcurrentCallers_NeverReturnTheSameValue()
    {
        var generation = new AsyncGeneration();
        var values = new long[400];

        Parallel.For(0, values.Length, index => values[index] = generation.Next());

        Assert.Equal(values.Length, values.Distinct().Count());
        Assert.Equal(values.Length, generation.Read());
    }

    [Fact]
    public void CacheStamp_StartsAtOneAndBumpsByOne()
    {
        var stamp = new CacheStamp();

        Assert.Equal(1, stamp.Read());
        stamp.Bump();
        stamp.Bump();
        Assert.Equal(3, stamp.Read());
    }
}
