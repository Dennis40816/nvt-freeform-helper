using Nvt.Core.Progress;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LoadingScopeCoordinatorTests
{
    [Fact]
    public async Task RunAsync_ShowsBeforeAction_AndHidesAfterAction()
    {
        var states = new List<bool>();
        var coordinator = new LoadingScopeCoordinator(
            yieldFrameAsync: static () => Task.CompletedTask,
            minimumVisibleDuration: TimeSpan.Zero);

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = coordinator.RunAsync(
            action: () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
            setVisible: isVisible => states.Add(isVisible));

        try
        {
            Assert.Equal([true], states);
        }
        finally
        {
            release.TrySetResult();
        }

        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal([true, false], states);
    }

    [Fact]
    public async Task RunAsync_RespectsMinimumVisibleDuration()
    {
        var states = new List<bool>();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var coordinator = new LoadingScopeCoordinator(
            yieldFrameAsync: static () => Task.CompletedTask,
            minimumVisibleDuration: TimeSpan.FromMilliseconds(60),
            timeProvider: clock);

        var run = coordinator.RunAsync(
            action: static () => Task.CompletedTask,
            setVisible: isVisible => states.Add(isVisible));

        clock.Advance(TimeSpan.FromMilliseconds(59));
        Assert.False(run.IsCompleted);
        Assert.Equal([true], states);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await run;

        Assert.Equal([true, false], states);
    }
}
