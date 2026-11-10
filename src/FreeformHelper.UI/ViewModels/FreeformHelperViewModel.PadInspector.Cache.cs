using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly PadInspectorRequestState _padInspectorRequests = new();
    private readonly PadInspectorMatchCache _padInspectorCache = new(); // Holds the match caches of the pad inspector.

    private void InvalidatePadInspectorDerivedCache()
    {
        _padInspectorRequests.BumpDerivedCacheRevision();
        _padInspectorCache.Clear();
    }

    private CadInspectorMatchCacheEntry GetOrBuildCadInspectorMatchCache(int cadPadId)
    {
        if (_padInspectorCache.CadMatches.TryGetValue(cadPadId, out var cached) &&
            cached.Revision == _padInspectorRequests.ReadDerivedCacheRevision())
        {
            return cached;
        }

        var matchedLinks = GetMatchedRegularLinks(cadPadId);
        var matchedRegularIds = GetMatchedRegularPadIds(cadPadId);
        var revision = _padInspectorRequests.ReadDerivedCacheRevision();
        var entry = PadInspectorMatchCacheEntryBuilder.BuildCad(
            revision,
            matchedLinks,
            matchedRegularIds,
            GetRegularPadIcDiff);
        _padInspectorCache.StoreCadInspectorMatchCache(cadPadId, entry);
        return entry;
    }

    private RegularInspectorMatchCacheEntry GetOrBuildRegularInspectorMatchCache(RegularPad regularPad)
    {
        if (_padInspectorCache.RegularMatches.TryGetValue(regularPad.RegularPadId, out var cached) &&
            cached.Revision == _padInspectorRequests.ReadDerivedCacheRevision())
        {
            return cached;
        }

        var matchedLinks = GetMatchedCadLinks(regularPad.RegularPadId);
        var matchedCadIds = GetMatchedCadPadIds(regularPad.RegularPadId);
        var revision = _padInspectorRequests.ReadDerivedCacheRevision();
        var entry = PadInspectorMatchCacheEntryBuilder.BuildRegular(
            revision,
            regularPad,
            matchedLinks,
            matchedCadIds,
            TryGetCadOutputFwDiffIndex,
            _projectFile.FreeformOverrides);
        _padInspectorCache.StoreRegularInspectorMatchCache(regularPad.RegularPadId, entry);
        return entry;
    }
}
