namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the cancellation source and the request counter of the latest deferred request of one
/// kind. Starting a request replaces the stored source. A finished request releases its own source
/// only while it is still the stored one. The view model keeps one gate for each kind of deferred
/// request. The counter and the source live in one immutable ticket, so a reader never sees a
/// counter from one request and a source from another.
/// </summary>
internal sealed class LatestRequestGate
{
    private Ticket _latest = new(0, null);

    /// <summary>
    /// Gets a value indicating whether a request source is stored and not yet released or cancelled.
    /// </summary>
    public bool HasPendingRequest => Volatile.Read(ref _latest).Source is not null;

    /// <summary>
    /// Gets the identifier of the latest started request.
    /// </summary>
    public long LatestRequestId => Volatile.Read(ref _latest).Number;

    /// <summary>
    /// Stores a new cancellation source and returns it with a new request identifier. The caller
    /// cancels the previous request first.
    /// </summary>
    public (long RequestId, CancellationTokenSource Source) Begin()
    {
        var source = new CancellationTokenSource();
        while (true)
        {
            var previous = Volatile.Read(ref _latest);
            var next = new Ticket(previous.Number + 1, source);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _latest, next, previous), previous))
            {
                return (next.Number, source);
            }
        }
    }

    /// <summary>
    /// Cancels and disposes the stored source, if any. The request counter keeps its value.
    /// </summary>
    public void Cancel()
    {
        var source = Detach(expected: null);
        if (source is null)
        {
            return;
        }

        try
        {
            source.Cancel();
        }
        finally
        {
            source.Dispose();
        }
    }

    /// <summary>
    /// Disposes <paramref name="source"/> when it is still the stored source and clears the store.
    /// </summary>
    /// <returns><see langword="true"/> when the source was the stored one.</returns>
    public bool Release(CancellationTokenSource source)
    {
        if (!ReferenceEquals(Detach(expected: source), source))
        {
            return false;
        }

        source.Dispose();
        return true;
    }

    // Clears the stored source and returns it. With an expected source, clears only when it matches.
    private CancellationTokenSource? Detach(CancellationTokenSource? expected)
    {
        while (true)
        {
            var previous = Volatile.Read(ref _latest);
            if (previous.Source is null || (expected is not null && !ReferenceEquals(previous.Source, expected)))
            {
                return null;
            }

            var next = previous with { Source = null };
            if (ReferenceEquals(Interlocked.CompareExchange(ref _latest, next, previous), previous))
            {
                return previous.Source;
            }
        }
    }

    private sealed record Ticket(long Number, CancellationTokenSource? Source);
}
