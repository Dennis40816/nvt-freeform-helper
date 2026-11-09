namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Owns the Pad Inspector cache revision and the selection and deferred refresh counters.
/// Each counter is a small holder with an intent-revealing API, so the version rule lives in one place.
/// </summary>
internal sealed class PadInspectorRequestState
{
    private readonly CacheStamp _derivedCache = new();
    private readonly AtomicSequence _selection = new();
    private readonly AtomicSequence _cadRefresh = new();
    private readonly AtomicSequence _regularRefresh = new();

    internal void BumpDerivedCacheRevision() => _derivedCache.Bump();

    internal int ReadDerivedCacheRevision() => _derivedCache.Read();

    internal long NextSelectionRevision() => _selection.Next();

    internal long ReadSelectionRevision() => _selection.Read();

    internal long NextCadRefreshRequestId() => _cadRefresh.Next();

    internal long ReadCadRefreshRequestId() => _cadRefresh.Read();

    internal long NextRegularRefreshRequestId() => _regularRefresh.Next();

    internal long ReadRegularRefreshRequestId() => _regularRefresh.Read();

    /// <summary>Holds an int version that starts at 1 and wraps around without an overflow check.</summary>
    private sealed class CacheStamp
    {
        private int _value = 1;

        internal void Bump()
        {
            unchecked
            {
                _value++;
            }
        }

        internal int Read() => _value;
    }

    /// <summary>Holds a long sequence that is advanced and read atomically.</summary>
    private sealed class AtomicSequence
    {
        private long _value;

        internal long Next() => Interlocked.Increment(ref _value);

        internal long Read() => Interlocked.Read(ref _value);
    }
}
