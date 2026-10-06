using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// A singleton class that acts as a centralized store for application log entries.
/// It provides a thread-safe mechanism to add log entries and exposes them via
/// an <see cref="ObservableCollection{T}"/> for UI binding. It also handles
/// buffering log entries until the UI is ready to display them.
/// </summary>
public sealed class AppLogStore
{
    private const int DefaultMaxEntries = 10000;

    private readonly object _sync = new();
    // The UI collection and whatever listens to it are not thread-safe. In the app only the UI thread changes
    // it. Where no UI loop runs (unit tests, or a dispatcher that is shutting down) every thread counts as the
    // UI thread, so the changes are made one at a time. Subscribers run while this gate is held; they must
    // not log synchronously or wait for another thread.
    private readonly object _uiCollectionGate = new();
    private readonly Func<bool> _hasUiThreadAccess;
    private readonly Action<Action> _postToUiThread;
    // The main collection of log entries that the UI binds to.
    private readonly ObservableCollection<AppLogEntry> _entries = new();
    // A concurrent queue to temporarily store log entries before the UI is ready to process them.
    private readonly ConcurrentQueue<(AppLogEntry Entry, long Generation)> _pending = new();
    // A thread-safe ring buffer snapshot used by runtime query and diagnostics.
    private readonly List<AppLogEntry> _ringEntries;
    private readonly int _maxEntries;
    // A flag indicating whether the UI thread has completed its initialization and is ready to process log entries.
    private bool _uiReady;
    private long _generation;

    /// <summary>
    /// Gets the singleton instance of the <see cref="AppLogStore"/>.
    /// </summary>
    public static AppLogStore Instance { get; } = new();

    /// <summary>
    /// Gets a read-only observable collection of <see cref="AppLogEntry"/> objects.
    /// UI components can bind to this collection to display log messages.
    /// </summary>
    public ReadOnlyObservableCollection<AppLogEntry> Entries { get; }

    /// <summary>
    /// Private constructor to enforce the singleton pattern.
    /// </summary>
    private AppLogStore()
        : this(DefaultMaxEntries)
    {
    }

    internal AppLogStore(int maxEntries)
        : this(
            maxEntries,
            static () => !UiThread.TryGetRunningDispatcher(out var dispatcher) || dispatcher!.CheckAccess(),
            static action =>
            {
                if (UiThread.TryGetRunningDispatcher(out var dispatcher))
                {
                    dispatcher!.Post(action);
                }
                else
                {
                    action();
                }
            })
    {
    }

    internal AppLogStore(int maxEntries, Func<bool> hasUiThreadAccess, Action<Action> postToUiThread)
    {
        _hasUiThreadAccess = hasUiThreadAccess ?? throw new ArgumentNullException(nameof(hasUiThreadAccess));
        _postToUiThread = postToUiThread ?? throw new ArgumentNullException(nameof(postToUiThread));
        _maxEntries = Math.Max(1, maxEntries);
        _ringEntries = new List<AppLogEntry>(_maxEntries);
        Entries = new ReadOnlyObservableCollection<AppLogEntry>(_entries);
    }

    public int MaxEntries => _maxEntries;

    /// <summary>
    /// Marks the UI as ready to receive and display log entries.
    /// Any pending log entries will be flushed to the observable collection on the UI thread.
    /// </summary>
    public void MarkUiReady()
    {
        if (_uiReady)
        {
            return; // UI is already marked as ready.
        }

        _uiReady = true;
        // Post the FlushPending operation to the UI thread to avoid cross-thread issues.
        _postToUiThread(FlushPending);
    }

    /// <summary>
    /// Adds a new <see cref="AppLogEntry"/> to the store.
    /// If the UI is not yet ready, the entry is queued. Otherwise, it's added to the
    /// observable collection on the UI thread.
    /// </summary>
    /// <param name="entry">The log entry to add.</param>
    public void Add(AppLogEntry entry)
    {
        var generation = AddToRing(entry);

        if (!_uiReady)
        {
            _pending.Enqueue((entry, generation)); // Enqueue if UI is not ready.
            TrimPendingQueueToMax();
            return;
        }

        // Ensure that additions to the ObservableCollection happen on the UI thread.
        if (_hasUiThreadAccess())
        {
            AppendEntryToUiCollection(entry, generation);
        }
        else
        {
            _postToUiThread(() => AppendEntryToUiCollection(entry, generation));
        }
    }

    /// <summary>
    /// Clears all log entries from the store.
    /// If the UI is not yet ready, it clears the pending queue. Otherwise, it clears
    /// the observable collection on the UI thread.
    /// </summary>
    public void Clear()
    {
        lock (_uiCollectionGate)
        {
            lock (_sync)
            {
                _generation++;
                _ringEntries.Clear();
                while (_pending.TryDequeue(out _))
                {
                }
            }

            if (!_uiReady)
            {
                return;
            }

            // Ensure that clearing the ObservableCollection happens on the UI thread.
            if (_hasUiThreadAccess())
            {
                ClearUiCollection();
            }
            else
            {
                _postToUiThread(ClearUiCollection);
            }
        }
    }

    public IReadOnlyList<AppLogEntry> GetTail(int tailCount)
    {
        return GetTail(tailCount, out _);
    }

    internal IReadOnlyList<AppLogEntry> GetTail(int tailCount, out int totalCount)
    {
        var effectiveTail = Math.Max(0, tailCount);
        lock (_sync)
        {
            totalCount = _ringEntries.Count;
            if (effectiveTail == 0 || totalCount == 0)
            {
                return Array.Empty<AppLogEntry>();
            }

            var start = Math.Max(0, _ringEntries.Count - effectiveTail);
            var length = _ringEntries.Count - start;
            var snapshot = new List<AppLogEntry>(length);
            for (var i = start; i < _ringEntries.Count; i++)
            {
                snapshot.Add(_ringEntries[i]);
            }

            return snapshot;
        }
    }

    public int GetTotalCount()
    {
        lock (_sync)
        {
            return _ringEntries.Count;
        }
    }

    /// <summary>
    /// Flushes all pending log entries from the <see cref="ConcurrentQueue{T}"/> to the
    /// <see cref="ObservableCollection{T}"/>. This method should only be called on the UI thread.
    /// </summary>
    private void FlushPending()
    {
        lock (_uiCollectionGate)
        {
            while (_pending.TryDequeue(out var entry))
            {
                AppendEntryToUiCollection(entry.Entry, entry.Generation);
            }
        }
    }

    private long AddToRing(AppLogEntry entry)
    {
        lock (_sync)
        {
            _ringEntries.Add(entry);
            TrimRingEntriesToMax();
            return _generation;
        }
    }

    private void TrimRingEntriesToMax()
    {
        var overflow = _ringEntries.Count - _maxEntries;
        if (overflow <= 0)
        {
            return;
        }

        _ringEntries.RemoveRange(0, overflow);
    }

    private void AppendEntryToUiCollection(AppLogEntry entry, long generation)
    {
        lock (_uiCollectionGate)
        {
            lock (_sync)
            {
                if (generation != _generation)
                {
                    return;
                }
            }

            _entries.Add(entry);
            TrimUiEntriesToMax();
        }
    }

    private void ClearUiCollection()
    {
        lock (_uiCollectionGate)
        {
            _entries.Clear();
        }
    }

    private void TrimUiEntriesToMax()
    {
        while (_entries.Count > _maxEntries)
        {
            _entries.RemoveAt(0);
        }
    }

    private void TrimPendingQueueToMax()
    {
        while (_pending.Count > _maxEntries && _pending.TryDequeue(out _))
        {
        }
    }
}
