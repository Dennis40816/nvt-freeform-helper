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

        var matchedLinks = GetMatchedRegularLinks(cadPadId)
            .OrderByDescending(link => link.RegularCoverage)
            .ThenByDescending(link => link.CadCoverage)
            .ThenBy(link => link.RegularPadId)
            .ToList();
        var matchedRegularIds = GetMatchedRegularPadIds(cadPadId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var matchedRegularDetails = PadInspectorMatchDetailBuilder.BuildMatchedRegularDetails(matchedRegularIds, matchedLinks, GetRegularPadIcDiff);
        var matchText = PadInspectorMatchFormatter.BuildCadMatchText(matchedRegularIds, matchedLinks, GetRegularPadIcDiff);
        var matchDetailsText = PadInspectorMatchFormatter.BuildCadMatchDetailsText(matchedRegularIds, matchedLinks, GetRegularPadIcDiff);
        var confidence = matchedRegularDetails.Count == 0
            ? 0.0
            : matchedRegularDetails.Max(detail => detail.CadCoverage);
        var entry = new CadInspectorMatchCacheEntry(
            _padInspectorRequests.ReadDerivedCacheRevision(),
            matchedLinks,
            matchedRegularIds,
            matchedRegularDetails,
            matchText,
            matchDetailsText,
            confidence);
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

        var matchedLinks = GetMatchedCadLinks(regularPad.RegularPadId)
            .OrderByDescending(link => link.CadCoverage)
            .ThenBy(link => link.CadPadId)
            .ToList();
        var matchedCadIds = GetMatchedCadPadIds(regularPad.RegularPadId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var matchedCadDetails = PadInspectorMatchDetailBuilder.BuildMatchedCadDetails(matchedCadIds, matchedLinks, TryGetCadOutputFwDiffIndex);
        var matchText = PadInspectorMatchFormatter.BuildRegularMatchText(matchedCadDetails, matchedCadIds);
        var matchDetailsText = PadInspectorMatchFormatter.BuildRegularMatchDetailsText(matchedCadDetails, matchedLinks);
        var diffSource = PadInspectorMatchFormatter.BuildRegularDiffSource(regularPad.DiffIndex, matchedCadDetails);
        var (freeformSource, freeformSourceDetail) = PadInspectorDisplayProjector.BuildRegularFreeformSource(
            regularPad,
            _projectFile.FreeformOverrides);
        var confidence = matchedCadDetails.Count == 0
            ? 0.0
            : matchedCadDetails.Max(detail => detail.CadCoverage);
        var ruleTrace = PadInspectorRuleTraceBuilder.BuildRegularRuleTrace(
            regularPad,
            matchedCadDetails,
            diffSource,
            freeformSource,
            freeformSourceDetail);
        var entry = new RegularInspectorMatchCacheEntry(
            _padInspectorRequests.ReadDerivedCacheRevision(),
            matchedLinks,
            matchedCadIds,
            matchedCadDetails,
            matchText,
            matchDetailsText,
            diffSource,
            freeformSource,
            freeformSourceDetail,
            confidence,
            ruleTrace);
        _padInspectorCache.StoreRegularInspectorMatchCache(regularPad.RegularPadId, entry);
        return entry;
    }
}
