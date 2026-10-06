using System.Collections.Specialized;
using System.Reflection;
using Avalonia.Threading;
using FreeformHelper.UI.Logging;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class AppLogStoreTests
{
    [Fact]
    public async Task AddAndClear_AfterUiReadyWithoutApplication_DoNotCreateDispatcher()
    {
        Assert.Null(global::Avalonia.Application.Current);
        var dispatcherField = typeof(Dispatcher).GetField("s_uiThread", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(dispatcherField);
        var before = dispatcherField.GetValue(null);
        var store = new AppLogStore(maxEntries: 3);

        store.MarkUiReady();
        await Task.Run(() =>
        {
            store.Add(CreateEntry("before"));
            store.Clear();
            store.Add(CreateEntry("after"));
        }, TestContext.Current.CancellationToken);

        Assert.Same(before, dispatcherField.GetValue(null));
        Assert.Equal("after", Assert.Single(store.Entries).Message);
        Assert.Equal("after", Assert.Single(store.GetTail(3)).Message);
    }

    [Fact]
    public void Add_WhenExceedMaxEntries_KeepsNewestTail()
    {
        var store = new AppLogStore(maxEntries: 3);

        store.Add(CreateEntry("m1"));
        store.Add(CreateEntry("m2"));
        store.Add(CreateEntry("m3"));
        store.Add(CreateEntry("m4"));

        var tail = store.GetTail(10);

        Assert.Equal(3, store.GetTotalCount());
        Assert.Equal(3, tail.Count);
        Assert.Equal("m2", tail[0].Message);
        Assert.Equal("m3", tail[1].Message);
        Assert.Equal("m4", tail[2].Message);
    }

    [Fact]
    public void Clear_RemovesAllRingEntries()
    {
        var store = new AppLogStore(maxEntries: 3);
        store.Add(CreateEntry("m1"));
        store.Add(CreateEntry("m2"));

        store.Clear();

        Assert.Equal(0, store.GetTotalCount());
        Assert.Empty(store.GetTail(5));
    }

    [Fact]
    public async Task Add_WhenEveryThreadCountsAsTheUiThread_ChangesTheUiCollectionOneAtATime()
    {
        const int threadCount = 8;
        const int entriesPerThread = 500;
        var store = new AppLogStore(
            maxEntries: threadCount * entriesPerThread,
            hasUiThreadAccess: static () => true,
            postToUiThread: static action => action());
        store.MarkUiReady();

        var handlersRunning = 0;
        var overlappingHandlers = 0;
        ((INotifyCollectionChanged)store.Entries).CollectionChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref handlersRunning) != 1)
            {
                Interlocked.Increment(ref overlappingHandlers);
            }

            Thread.SpinWait(200);
            Interlocked.Decrement(ref handlersRunning);
        };

        using var start = new ManualResetEventSlim();
        var writers = Enumerable.Range(0, threadCount)
            .Select(writer => Task.Run(() =>
            {
                start.Wait();
                for (var index = 0; index < entriesPerThread; index++)
                {
                    store.Add(CreateEntry($"{writer}:{index}"));
                }
            }))
            .ToArray();
        start.Set();
        await Task.WhenAll(writers);

        Assert.Equal(0, overlappingHandlers);
        Assert.Equal(threadCount * entriesPerThread, store.Entries.Count);
    }

    private static AppLogEntry CreateEntry(string message)
    {
        return new AppLogEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "INFO",
            Logger: "test",
            Message: message);
    }
}
