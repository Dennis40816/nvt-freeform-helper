// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchPreviewTimingTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 1)]
    [InlineData(0, 2)]
    [InlineData(4, 1)]
    public void NextAutoPlayStageClampsThenCycles(int current, int expected)
    {
        Assert.Equal(expected, NotchPreviewTiming.NextAutoPlayStage(current));
    }

    [Theory]
    [InlineData(1, -1, 3)]
    [InlineData(2, -1, 1)]
    [InlineData(3, -1, 2)]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 3)]
    [InlineData(3, 1, 1)]
    [InlineData(1, 4, 2)]
    [InlineData(2, 4, 3)]
    [InlineData(3, 4, 1)]
    [InlineData(1, -4, 3)]
    [InlineData(2, -4, 1)]
    [InlineData(3, -4, 2)]
    [InlineData(0, 1, 2)]
    [InlineData(4, -1, 2)]
    public void ShiftStageClampsThenWraps(int current, int delta, int expected)
    {
        Assert.Equal(expected, NotchPreviewTiming.ShiftStage(current, delta));
    }

    [Theory]
    [InlineData(100, 200)]
    [InlineData(700, 700)]
    [InlineData(9000, 5000)]
    public void ClampAutoPlayIntervalPreservesLimits(int interval, int expected)
    {
        Assert.Equal(expected, NotchPreviewTiming.ClampAutoPlayIntervalMs(interval));
    }

    [Fact]
    public void ClampAutoPlayIntervalRoundsToTheNearestEvenMillisecond()
    {
        Assert.Equal(701, NotchPreviewTiming.ClampAutoPlayIntervalMs(700.6m));
        Assert.Equal(700, NotchPreviewTiming.ClampAutoPlayIntervalMs(700.5m));
    }

    [Fact]
    public void DefaultClockIsTheSystemClock()
    {
        Assert.Same(TimeProvider.System, new NotchPreviewTiming().TimeProvider);
    }

    [Fact]
    public async Task DelayCompletesOnlyAtTheDueTimeAsync()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var timing = new NotchPreviewTiming { TimeProvider = clock };
        var delay = timing.DelayAsync(TimeSpan.FromMilliseconds(120), TestContext.Current.CancellationToken);
        Assert.False(delay.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(119));
        Assert.False(delay.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await delay.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DelayHonorsCancellationBeforeTheDueTimeAsync()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var timing = new NotchPreviewTiming { TimeProvider = clock };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var delay = timing.DelayAsync(TimeSpan.FromMilliseconds(120), cancellation.Token);
        Assert.False(delay.IsCompleted);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => delay.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        clock.Advance(TimeSpan.FromMilliseconds(120));
        Assert.True(delay.IsCanceled);
    }
}
