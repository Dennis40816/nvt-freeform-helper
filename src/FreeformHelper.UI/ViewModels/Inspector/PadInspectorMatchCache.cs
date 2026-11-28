using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Holds the match caches of the Pad Inspector.</summary>
internal sealed class PadInspectorMatchCache
{
    private const int PadInspectorCacheMaxEntries = 4096;

    /// <summary>Gets the cached CAD pad matches, keyed by CAD pad id.</summary>
    internal Dictionary<int, CadInspectorMatchCacheEntry> CadMatches { get; } = new();

    /// <summary>Gets the cached regular pad matches, keyed by regular pad id.</summary>
    internal Dictionary<int, RegularInspectorMatchCacheEntry> RegularMatches { get; } = new();

    internal void Clear()
    {
        CadMatches.Clear();
        RegularMatches.Clear();
    }

    internal void StoreCadInspectorMatchCache(int cadPadId, CadInspectorMatchCacheEntry entry)
    {
        if (CadMatches.Count >= PadInspectorCacheMaxEntries &&
            !CadMatches.ContainsKey(cadPadId))
        {
            CadMatches.Clear();
        }

        CadMatches[cadPadId] = entry;
    }

    internal void StoreRegularInspectorMatchCache(int regularPadId, RegularInspectorMatchCacheEntry entry)
    {
        if (RegularMatches.Count >= PadInspectorCacheMaxEntries &&
            !RegularMatches.ContainsKey(regularPadId))
        {
            RegularMatches.Clear();
        }

        RegularMatches[regularPadId] = entry;
    }
}

internal sealed record CadInspectorMatchCacheEntry(
    int Revision,
    IReadOnlyList<PadMatchLink> MatchedLinks,
    IReadOnlyList<int> MatchedRegularIds,
    IReadOnlyList<PadInspectorMatchedRegularSnapshot> MatchedRegularDetails,
    string MatchText,
    string MatchDetailsText,
    double Confidence);

internal sealed record RegularInspectorMatchCacheEntry(
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
