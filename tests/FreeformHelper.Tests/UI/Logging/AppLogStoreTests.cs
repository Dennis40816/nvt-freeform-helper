using System.Collections.Specialized;
using System.Reflection;
using Avalonia.Threading;
using FreeformHelper.UI.Logging;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class AppLogStoreTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Clear_WhenNotificationsAreQueued_DiscardsPreClearEntries(bool readyBeforeAdd, bool clearOnUiThread)
    {
        var callbacks = new Queue<Action>();
        var hasUiThreadAccess = false;
        var store = new AppLogStore(3, () => hasUiThreadAccess, callbacks.Enqueue);
        if (readyBeforeAdd)
        {
            store.MarkUiReady();
            callbacks.Dequeue()();
        }

        store.Add(CreateEntry("before clear"));
        if (!readyBeforeAdd)
        {
            store.MarkUiReady();
        }

        Assert.Empty(store.Entries);
        var displayedMessages = new List<string>();
        ((INotifyCollectionChanged)store.Entries).CollectionChanged += (_, change) =>
        {
            if (change.NewItems is not null)
            {
                displayedMessages.AddRange(change.NewItems.Cast<AppLogEntry>().Select(entry => entry.Message));
            }
        };

        hasUiThreadAccess = clearOnUiThread;
        store.Clear();
        Assert.Empty(store.GetTail(3));
        hasUiThreadAccess = false;
        store.Add(CreateEntry("after clear"));
        while (callbacks.TryDequeue(out var callback))
        {
            callback();
        }

        Assert.Equal("after clear", Assert.Single(displayedMessages));
        Assert.Equal("after clear", Assert.Single(store.Entries).Message);
        Assert.Equal("after clear", Assert.Single(store.GetTail(3)).Message);
    }

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
