using Nvt.Core.Lifecycle;
using Xunit;

namespace FreeformHelper.Tests;

internal static class LifecycleCharacterization
{
    public static void AssertRefreshSequence()
    {
        var scheduled = new List<Action>();
        var refreshCount = 0;
        CoalescedRefresh? refresh = null;
        refresh = new CoalescedRefresh(scheduled.Add, () =>
        {
            refreshCount++;
            if (refreshCount == 1)
            {
                refresh!.Request();
            }
        });

        for (var i = 0; i < 25; i++)
        {
            refresh.Request();
        }

        Assert.Single(scheduled);
        refresh.Reset();
        refresh.Request();
        Assert.Equal(2, scheduled.Count);
        Assert.Equal(0, refreshCount);
        scheduled[0]();
        Assert.Equal(3, scheduled.Count);
        Assert.Equal(1, refreshCount);
        scheduled[1]();
        scheduled[2]();
        Assert.Equal(3, refreshCount);
        refresh.Request();
        Assert.Equal(4, scheduled.Count);
        scheduled[3]();
        Assert.Equal(4, refreshCount);
    }

    public static void AssertUndoSequence()
    {
        var service = new UndoService();
        var executionOrder = new List<int>();
        Action first = () => executionOrder.Add(1);
        Action second = () => executionOrder.Add(2);

        Assert.False(service.CanUndo);
        service.Push(first, "First change");
        service.Push(second, "Second change");
        Assert.True(service.CanUndo);
        Assert.Empty(executionOrder);
        Assert.True(service.TryPop(out var latest));
        Assert.Same(second, latest.Undo);
        Assert.Equal("Second change", latest.Description);
        Assert.True(service.CanUndo);
        Assert.Empty(executionOrder);
        latest.Undo();
        Assert.True(service.TryPop(out var earlier));
        Assert.Same(first, earlier.Undo);
        Assert.Equal("First change", earlier.Description);
        Assert.False(service.CanUndo);
        Assert.Equal(2, Assert.Single(executionOrder));
        earlier.Undo();
        Assert.Collection(executionOrder, item => Assert.Equal(2, item), item => Assert.Equal(1, item));
        Assert.False(service.TryPop(out var empty));
        Assert.Null(empty);
        Assert.False(service.CanUndo);
    }
}
