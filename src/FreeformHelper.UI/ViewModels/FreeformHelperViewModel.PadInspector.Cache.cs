using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const int PadInspectorCacheMaxEntries = 4096;
    private readonly PadInspectorCacheState _padInspectorCache = new(); // Holds the match caches of the pad inspector.
    private int _padInspectorDerivedCacheRevision = 1;
    private long _padInspectorSelectionRevision;

    private void InvalidatePadInspectorDerivedCache()
    {
        unchecked
        {
            _padInspectorDerivedCacheRevision++;
        }

        _padInspectorCache.CadMatches.Clear();
        _padInspectorCache.RegularMatches.Clear();
    }

    private CadInspectorMatchCacheEntry GetOrBuildCadInspectorMatchCache(int cadPadId)
    {
        if (_padInspectorCache.CadMatches.TryGetValue(cadPadId, out var cached) &&
            cached.Revision == _padInspectorDerivedCacheRevision)
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
        var matchedRegularDetails = BuildMatchedRegularDetails(matchedRegularIds, matchedLinks);
        var matchText = BuildCadMatchText(matchedRegularIds, matchedLinks, GetRegularPadIcDiff);
        var matchDetailsText = BuildCadMatchDetailsText(matchedRegularIds, matchedLinks, GetRegularPadIcDiff);
        var confidence = matchedRegularDetails.Count == 0
            ? 0.0
            : matchedRegularDetails.Max(detail => detail.CadCoverage);
        var entry = new CadInspectorMatchCacheEntry(
            _padInspectorDerivedCacheRevision,
            matchedLinks,
            matchedRegularIds,
            matchedRegularDetails,
            matchText,
            matchDetailsText,
            confidence);
        StoreCadInspectorMatchCache(cadPadId, entry);
        return entry;
    }

    private RegularInspectorMatchCacheEntry GetOrBuildRegularInspectorMatchCache(RegularPad regularPad)
    {
        if (_padInspectorCache.RegularMatches.TryGetValue(regularPad.RegularPadId, out var cached) &&
            cached.Revision == _padInspectorDerivedCacheRevision)
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
        var matchedCadDetails = BuildMatchedCadDetails(matchedCadIds, matchedLinks);
        var matchText = BuildRegularMatchText(matchedCadDetails, matchedCadIds);
        var matchDetailsText = BuildRegularMatchDetailsText(matchedCadDetails, matchedLinks);
        var diffSource = BuildRegularDiffSource(regularPad.DiffIndex, matchedCadDetails);
        var (freeformSource, freeformSourceDetail) = BuildRegularFreeformSource(regularPad);
        var confidence = matchedCadDetails.Count == 0
            ? 0.0
            : matchedCadDetails.Max(detail => detail.CadCoverage);
        var ruleTrace = BuildRegularRuleTrace(
            regularPad,
            matchedCadDetails,
            diffSource,
            freeformSource,
            freeformSourceDetail);
        var entry = new RegularInspectorMatchCacheEntry(
            _padInspectorDerivedCacheRevision,
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
        StoreRegularInspectorMatchCache(regularPad.RegularPadId, entry);
        return entry;
    }

    private void StoreCadInspectorMatchCache(int cadPadId, CadInspectorMatchCacheEntry entry)
    {
        if (_padInspectorCache.CadMatches.Count >= PadInspectorCacheMaxEntries &&
            !_padInspectorCache.CadMatches.ContainsKey(cadPadId))
        {
            _padInspectorCache.CadMatches.Clear();
        }

        _padInspectorCache.CadMatches[cadPadId] = entry;
    }

    private void StoreRegularInspectorMatchCache(int regularPadId, RegularInspectorMatchCacheEntry entry)
    {
        if (_padInspectorCache.RegularMatches.Count >= PadInspectorCacheMaxEntries &&
            !_padInspectorCache.RegularMatches.ContainsKey(regularPadId))
        {
            _padInspectorCache.RegularMatches.Clear();
        }

        _padInspectorCache.RegularMatches[regularPadId] = entry;
    }

    private sealed record CadInspectorMatchCacheEntry(
        int Revision,
        IReadOnlyList<PadMatchLink> MatchedLinks,
        IReadOnlyList<int> MatchedRegularIds,
        IReadOnlyList<PadInspectorMatchedRegularSnapshot> MatchedRegularDetails,
        string MatchText,
        string MatchDetailsText,
        double Confidence);

    private sealed record RegularInspectorMatchCacheEntry(
        int Revision,
        IReadOnlyList<PadMatchLink> MatchedLinks,
        IReadOnlyList<int> MatchedCadIds,
        IReadOnlyList<PadInspectorMatchedCadSnapshot> MatchedCadDetails,
        string MatchText,
        string MatchDetailsText,
        string DiffSource,
        string FreeformSource,
        string FreeformSourceDetail,
        double Confidence,
        IReadOnlyList<PadInspectorRuleTraceEntry> RuleTrace);

    /// <summary>
    /// Holds the match caches of the pad inspector.
    /// </summary>
    private sealed class PadInspectorCacheState
    {
        /// <summary>Gets the cached CAD pad matches, keyed by CAD pad id.</summary>
        internal Dictionary<int, CadInspectorMatchCacheEntry> CadMatches { get; } = new();

        /// <summary>Gets the cached regular pad matches, keyed by regular pad id.</summary>
        internal Dictionary<int, RegularInspectorMatchCacheEntry> RegularMatches { get; } = new();
    }
}
