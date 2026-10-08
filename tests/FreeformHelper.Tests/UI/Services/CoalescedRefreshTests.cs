using Nvt.Core.Lifecycle;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoalescedRefreshTests
{
    [Fact]
    public void Request_BurstBeforeTheRefreshRuns_SchedulesOnce()
    {
        var scheduled = new List<Action>();
        var refreshCount = 0;
        var refresh = new CoalescedRefresh(scheduled.Add, () => refreshCount++);

        for (var i = 0; i < 25; i++)
        {
            refresh.Request();
        }

        Assert.Single(scheduled);
        scheduled[0]();
        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public void Request_AfterTheRefreshRan_SchedulesAgain()
    {
        var scheduled = new List<Action>();
        var refreshCount = 0;
        var refresh = new CoalescedRefresh(scheduled.Add, () => refreshCount++);

        refresh.Request();
        scheduled[0]();
        refresh.Request();

        Assert.Equal(2, scheduled.Count);
        scheduled[1]();
        Assert.Equal(2, refreshCount);
    }

    [Fact]
    public void Request_WhileTheRefreshIsRunning_SchedulesAnother()
    {
        var scheduled = new List<Action>();
        CoalescedRefresh? refresh = null;
        var refreshCount = 0;
        refresh = new CoalescedRefresh(scheduled.Add, () =>
        {
            refreshCount++;
            if (refreshCount == 1)
            {
                // A change that arrives during the refresh must not be lost.
                refresh!.Request();
            }
        });

        refresh.Request();
        scheduled[0]();

        Assert.Equal(2, scheduled.Count);
    }

    [Fact]
    public void Reset_DropsAScheduledRefresh_SoTheNextRequestSchedules()
    {
        var scheduled = new List<Action>();
        var refresh = new CoalescedRefresh(scheduled.Add, static () => { });

        refresh.Request();
        refresh.Reset();
        refresh.Request();

        Assert.Equal(2, scheduled.Count);
        LifecycleCharacterization.AssertRefreshSequence();
    }
}
