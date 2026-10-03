using System.Diagnostics;
using FreeformHelper.UI.Services;
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
        var coordinator = new LoadingScopeCoordinator(
            yieldFrameAsync: static () => Task.CompletedTask,
            minimumVisibleDuration: TimeSpan.FromMilliseconds(60));
        var sw = Stopwatch.StartNew();

        await coordinator.RunAsync(
            action: static () => Task.CompletedTask,
            setVisible: isVisible => states.Add(isVisible));

        sw.Stop();

        Assert.Equal([true, false], states);
        Assert.True(
            sw.ElapsedMilliseconds >= 45,
            $"Expected visible duration >= 45ms, actual={sw.ElapsedMilliseconds}ms.");
    }
}
