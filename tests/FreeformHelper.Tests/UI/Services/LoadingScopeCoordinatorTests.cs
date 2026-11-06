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

        await coordinator.RunAsync(
            action: async () =>
            {
                await Task.Delay(10);
            },
            setVisible: isVisible => states.Add(isVisible));

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
