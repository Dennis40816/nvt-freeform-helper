namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the cancellation source for one deferred Pad Inspector refresh.</summary>
internal sealed class PadInspectorDeferredRefreshSlot
{
    private CancellationTokenSource? _current;

    internal bool IsActive => _current is not null;

    internal void Install(CancellationTokenSource cts) => _current = cts;

    internal void Cancel()
    {
        var cts = Interlocked.Exchange(ref _current, null);
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    internal bool TryComplete(CancellationTokenSource cts)
    {
        if (ReferenceEquals(Interlocked.CompareExchange(ref _current, null, cts), cts))
        {
            cts.Dispose();
            return true;
        }

        return false;
    }
}
