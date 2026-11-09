using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Owns the Pad Inspector cache revision and the selection and deferred refresh counters.
/// The counters use the shared <see cref="CacheStamp"/> and <see cref="AsyncGeneration"/> types, so the version rule lives in one place.
/// </summary>
internal sealed class PadInspectorRequestState
{
    private readonly CacheStamp _derivedCache = new();
    private readonly AsyncGeneration _selection = new();
    private readonly AsyncGeneration _cadRefresh = new();
    private readonly AsyncGeneration _regularRefresh = new();

    internal void BumpDerivedCacheRevision() => _derivedCache.Bump();

    internal int ReadDerivedCacheRevision() => _derivedCache.Read();

    internal long NextSelectionRevision() => _selection.Next();

    internal long ReadSelectionRevision() => _selection.Read();

    internal long NextCadRefreshRequestId() => _cadRefresh.Next();

    internal long ReadCadRefreshRequestId() => _cadRefresh.Read();

    internal long NextRegularRefreshRequestId() => _regularRefresh.Next();

    internal long ReadRegularRefreshRequestId() => _regularRefresh.Read();
}
