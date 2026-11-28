// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;
using Nvt.Core.Time;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchProgressGateTests
{
    [Fact]
    public void NonFinalUpdateIsDroppedAt119MillisecondsAndPublishedAt120()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var gate = new NotchProgressGate(clock, static () => true, 120);
        var progress = new NotchGenerationProgress(1, 4, 1, null, null);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(gate.ShouldPublish(progress));

        clock.Advance(TimeSpan.FromMilliseconds(119));
        Assert.False(gate.ShouldPublish(progress));
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(gate.ShouldPublish(progress));
    }

    [Fact]
    public void FirstNonFinalUpdateUsesZeroTimestampRatherThanConstructionTime()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        clock.Advance(TimeSpan.FromMilliseconds(119));
        var gate = new NotchProgressGate(clock, static () => true, 120);
        var progress = new NotchGenerationProgress(1, 4, 1, null, null);

        Assert.False(gate.ShouldPublish(progress));
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(gate.ShouldPublish(progress));
    }

    [Fact]
    public void FinalUpdateBypassesThrottleAndStartsTheNextWindow()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var gate = new NotchProgressGate(clock, static () => true, 120);
        var partial = new NotchGenerationProgress(1, 4, 1, null, null);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(gate.ShouldPublish(partial));
        clock.Advance(TimeSpan.FromMilliseconds(119));

        Assert.True(gate.ShouldPublish(new NotchGenerationProgress(4, 4, 3, null, null, "Scan CAD", 1, 3)));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(gate.ShouldPublish(partial));
        clock.Advance(TimeSpan.FromMilliseconds(119));

        Assert.True(gate.ShouldPublish(partial));
    }

    [Fact]
    public void IndependentReportersKeepIndependentLastTimestamps()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var export = new NotchProgressGate(clock, static () => true, 120);
        var step5 = new NotchProgressGate(clock, static () => true, 120);
        var progress = new NotchGenerationProgress(1, 4, 1, null, null);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(export.ShouldPublish(progress));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(step5.ShouldPublish(progress));
        clock.Advance(TimeSpan.FromMilliseconds(119));

        Assert.True(export.ShouldPublish(progress));
        Assert.False(step5.ShouldPublish(progress));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(step5.ShouldPublish(progress));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void StaleUpdateIsDroppedBeforeReadingTheClock(int processed)
    {
        var clock = new DelegateTimeProvider(
            static () => throw new InvalidOperationException("A stale update must not read UTC."),
            static () => throw new InvalidOperationException("A stale update must not read a timestamp."),
            TimeSpan.TicksPerSecond);
        var gate = new NotchProgressGate(clock, static () => false, 120);

        Assert.False(gate.ShouldPublish(new NotchGenerationProgress(processed, 4, 1, null, null)));
    }

    [Fact]
    public void CurrentnessIsRecheckedAndStaleUpdateDoesNotMoveTheWindow()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        bool current = true;
        var gate = new NotchProgressGate(clock, () => current, 120);
        var partial = new NotchGenerationProgress(1, 4, 1, null, null);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(gate.ShouldPublish(partial));
        clock.Advance(TimeSpan.FromMilliseconds(119));
        current = false;

        Assert.False(gate.ShouldPublish(new NotchGenerationProgress(4, 4, 3, null, null)));
        current = true;
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(gate.ShouldPublish(partial));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, -4, false)]
    [InlineData(1, -4, true)]
    [InlineData(-2, 4, false)]
    [InlineData(9, 4, true)]
    public void FinalDetectionNormalizesTotalAndProcessedCounts(int processed, int total, bool expected)
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var gate = new NotchProgressGate(clock, static () => true, 120);

        Assert.Equal(expected, gate.ShouldPublish(new NotchGenerationProgress(processed, total, 3, null, null)));
    }

    [Fact]
    public void ElapsedReadFollowsTimestampReadAndAcceptedUpdateStoresTheFirstRead()
    {
        var timestamps = new Queue<long>([120, 121, 239, 240]);
        var clock = new DelegateTimeProvider(static () => DateTimeOffset.UnixEpoch, timestamps.Dequeue, 1000);
        var gate = new NotchProgressGate(clock, static () => true, 120);
        var progress = new NotchGenerationProgress(1, 4, 1, null, null);

        Assert.True(gate.ShouldPublish(progress));
        Assert.True(gate.ShouldPublish(progress));
        Assert.Empty(timestamps);
    }
}
