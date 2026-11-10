// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Builds Pad Inspector match cache entries from supplied match data and lookups.
/// </summary>
internal static class PadInspectorMatchCacheEntryBuilder
{
    internal static CadInspectorMatchCacheEntry BuildCad(
        int revision,
        IEnumerable<PadMatchLink> links,
        IEnumerable<int> matchedRegularIds,
        Func<int, (int IcIndex, int DiffIndex)?> getRegularPadIcDiff)
    {
        var matchedLinks = links
            .OrderByDescending(link => link.RegularCoverage)
            .ThenByDescending(link => link.CadCoverage)
            .ThenBy(link => link.RegularPadId)
            .ToList();
        var orderedMatchedRegularIds = matchedRegularIds
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var matchedRegularDetails = PadInspectorMatchDetailBuilder.BuildMatchedRegularDetails(
            orderedMatchedRegularIds, matchedLinks, getRegularPadIcDiff);
        var matchText = PadInspectorMatchFormatter.BuildCadMatchText(orderedMatchedRegularIds, matchedLinks, getRegularPadIcDiff);
        var matchDetailsText = PadInspectorMatchFormatter.BuildCadMatchDetailsText(
            orderedMatchedRegularIds, matchedLinks, getRegularPadIcDiff);
        var confidence = matchedRegularDetails.Count == 0
            ? 0.0
            : matchedRegularDetails.Max(detail => detail.CadCoverage);
        var entry = new CadInspectorMatchCacheEntry(
            revision,
            matchedLinks,
            orderedMatchedRegularIds,
            matchedRegularDetails,
            matchText,
            matchDetailsText,
            confidence);
        return entry;
    }

    internal static RegularInspectorMatchCacheEntry BuildRegular(
        int revision,
        RegularPad regularPad,
        IEnumerable<PadMatchLink> links,
        IEnumerable<int> matchedCadIds,
        PadInspectorMatchDetailBuilder.CadDxfIndexLookup tryGetCadOutputFwDiffIndex,
        IReadOnlyDictionary<int, FreeformType> freeformOverrides)
    {
        var matchedLinks = links
            .OrderByDescending(link => link.CadCoverage)
            .ThenBy(link => link.CadPadId)
            .ToList();
        var orderedMatchedCadIds = matchedCadIds
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var matchedCadDetails = PadInspectorMatchDetailBuilder.BuildMatchedCadDetails(
            orderedMatchedCadIds, matchedLinks, tryGetCadOutputFwDiffIndex);
        var matchText = PadInspectorMatchFormatter.BuildRegularMatchText(matchedCadDetails, orderedMatchedCadIds);
        var matchDetailsText = PadInspectorMatchFormatter.BuildRegularMatchDetailsText(matchedCadDetails, matchedLinks);
        var diffSource = PadInspectorMatchFormatter.BuildRegularDiffSource(regularPad.DiffIndex, matchedCadDetails);
        var (freeformSource, freeformSourceDetail) = PadInspectorDisplayProjector.BuildRegularFreeformSource(
            regularPad,
            freeformOverrides);
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
            revision,
            matchedLinks,
            orderedMatchedCadIds,
            matchedCadDetails,
            matchText,
            matchDetailsText,
            diffSource,
            freeformSource,
            freeformSourceDetail,
            confidence,
            ruleTrace);
        return entry;
    }
}
