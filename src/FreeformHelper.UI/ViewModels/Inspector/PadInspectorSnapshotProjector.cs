// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Assembles pad inspector snapshots from captured values.
/// </summary>
internal static class PadInspectorSnapshotProjector
{
    internal static PadInspectorSnapshot BuildCad(
        CadPad cadPad,
        int? dxfIndex,
        int? cadOutputFwDiffOverride,
        bool isDxfIndexAnchor,
        string dxfIndexDisplayText,
        string cadOutputFwDiffAssignmentModeText,
        int? icIndex,
        CadInspectorMatchCacheEntry matchCache,
        IReadOnlyList<PadInspectorMatchedRegularSnapshot> matchedRegularDetails,
        string matchText,
        string matchDetailsText,
        PadInspectorNotchSnapshot? notch,
        NotchCadRowEligibility notchEligibility,
        string notchRowSummary,
        bool includeRuleTrace)
    {
        var ruleTrace = includeRuleTrace
            ? PadInspectorRuleTraceBuilder.BuildCadRuleTrace(
                dxfIndex,
                cadOutputFwDiffAssignmentModeText,
                matchedRegularDetails,
                notch,
                notchEligibility)
            : PadInspectorRuleTraceBuilder.BuildDeferredRuleTracePlaceholder();

        var snapshot = new CadPadInspectorSnapshot(
            CadPadId: cadPad.Id,
            Name: cadPad.Name,
            Layer: cadPad.Layer,
            Area: cadPad.Area,
            Bounds: cadPad.Bounds,
            Centroid: cadPad.Centroid,
            Vertices: cadPad.Polygon.Vertices.Length,
            DxfIndex: dxfIndex,
            CadOutputFwDiffOverride: cadOutputFwDiffOverride,
            IsDxfIndexAnchor: isDxfIndexAnchor,
            DxfIndexDisplayText: dxfIndexDisplayText,
            CadOutputFwDiffAssignmentModeText: cadOutputFwDiffAssignmentModeText,
            IcIndex: icIndex,
            MatchText: matchText,
            MatchDetailsText: matchDetailsText,
            MatchedRegularPadIds: matchCache.MatchedRegularIds,
            MatchedRegularDetails: matchedRegularDetails,
            IsNotchRowEligible: notchEligibility.HasRows,
            NotchRowSummary: notchRowSummary,
            Notch: notch,
            RuleTrace: ruleTrace,
            MatchConfidence: matchCache.Confidence);

        return new PadInspectorSnapshot(
            Kind: "cad",
            Cad: snapshot,
            Regular: null);
    }

    internal static (
        IReadOnlyList<PadInspectorMatchedRegularSnapshot> Details,
        string MatchText,
        string MatchDetailsText) BuildCadMatchDisplay(
        CadInspectorMatchCacheEntry matchCache,
        bool includeMatchDetails,
        bool includeRuleTrace)
    {
        var matchedRegularIds = matchCache.MatchedRegularIds;
        var matchedRegularDetails = includeMatchDetails || includeRuleTrace
            ? matchCache.MatchedRegularDetails
            : (IReadOnlyList<PadInspectorMatchedRegularSnapshot>)Array.Empty<PadInspectorMatchedRegularSnapshot>();
        var matchDetailsText = includeMatchDetails
            ? matchCache.MatchDetailsText
            : matchedRegularIds.Count == 0
                ? "Unmatched"
                : $"Matched regular pad(s): {matchedRegularIds.Count}.";
        return (matchedRegularDetails, matchCache.MatchText, matchDetailsText);
    }

    internal static PadInspectorNotchSnapshot BuildNotch(
        NotchV22ResolvedResult resolved,
        bool includeExpensiveNotchDetails,
        string? precomputedDiagnostics,
        Func<NotchV22CompensationResult, string?> buildDiagnosticsText,
        NotchComputationMode computationMode)
    {
        var resolvedCompensation = resolved.Compensation;
        var stage3Area = resolvedCompensation.Stage3Area;
        var strictAreaThreshold = resolved.TargetAllocation.StrictAreaThreshold;
        var targets = resolved.TargetAllocation.Targets
            .Select(target => new PadInspectorNotchTargetSnapshot(
                target.IcIndex,
                target.DiffIndex,
                target.EffectiveArea,
                target.Ratio,
                target.RatioPercentRounded,
                target.PassesStrictThreshold,
                target.IsAnchorDiff,
                target.ToFullAppliedRegularCount,
                target.RegularCount,
                target.RegularAreas
                    .Select(area => new PadInspectorNotchTargetRegularAreaSnapshot(
                        area.RegularPadId,
                        area.EffectiveArea))
                    .ToList(),
                target.RegularPadIds))
            .ToList();
        return new PadInspectorNotchSnapshot(
            resolvedCompensation.ToRegularRatio,
            resolvedCompensation.ToFullRatio,
            resolvedCompensation.CombinedRatio,
            resolvedCompensation.IsToFullEnabled,
            stage3Area,
            strictAreaThreshold,
            targets,
            includeExpensiveNotchDetails
                ? precomputedDiagnostics ?? buildDiagnosticsText(resolvedCompensation)
                : null)
        {
            TargetCoverageProjection = resolved.TargetAllocation.TargetCoverageProjection,
            ComputationMode = computationMode
        };
    }

    internal static string FormatNotchRowSummary(NotchCadRowEligibility eligibility)
    {
        var eligibleVersionText = eligibility.EligibleVersions.Count == 0
            ? "-"
            : string.Join(", ", eligibility.EligibleVersions.Select(v => v.ToDisplayLabel()));
        var notchRowSummary = eligibility.HasRows
            ? $"Notch rows: {eligibility.EstimatedRowCount} ({eligibleVersionText})"
            : $"Notch rows: 0 ({eligibility.Reason})";
        return notchRowSummary;
    }

    internal static PadInspectorSnapshot BuildRegular(
        RegularPad regularPad,
        RegularInspectorMatchCacheEntry matchCache,
        bool includeMatchDetails,
        bool includeRuleTrace,
        int displayRow)
    {
        var matchedCadIds = matchCache.MatchedCadIds;
        var matchedCadDetails = includeMatchDetails || includeRuleTrace
            ? matchCache.MatchedCadDetails
            : (IReadOnlyList<PadInspectorMatchedCadSnapshot>)Array.Empty<PadInspectorMatchedCadSnapshot>();
        var matchText = matchCache.MatchText;
        var matchDetailsText = includeMatchDetails
            ? matchCache.MatchDetailsText
            : matchedCadIds.Count == 0
                ? "Unmatched"
                : $"Matched CAD pad(s): {matchedCadIds.Count}.";

        var diffSource = includeMatchDetails || includeRuleTrace
            ? matchCache.DiffSource
            : matchedCadIds.Count == 0
                ? "Unmatched"
                : "Computing...";
        var freeformSource = includeMatchDetails || includeRuleTrace
            ? matchCache.FreeformSource
            : regularPad.Freeform == FreeformType.None
                ? "None"
                : "Computing...";
        var freeformSourceDetail = includeMatchDetails || includeRuleTrace
            ? matchCache.FreeformSourceDetail
            : regularPad.Freeform == FreeformType.None
                ? "No Step 2 freeform tag."
                : "Computing...";
        var confidence = matchCache.Confidence;
        var ruleTrace = includeRuleTrace
            ? matchCache.RuleTrace
            : PadInspectorRuleTraceBuilder.BuildDeferredRuleTracePlaceholder();

        var snapshot = new RegularPadInspectorSnapshot(
            RegularPadId: regularPad.RegularPadId,
            Index: regularPad.Index,
            Row: regularPad.Row,
            Col: regularPad.Col,
            DisplayRow: displayRow,
            IcIndex: regularPad.IcIndex,
            DiffIndex: regularPad.DiffIndex,
            Area: regularPad.Area,
            Bounds: regularPad.Bounds,
            Centroid: regularPad.Centroid,
            Freeform: regularPad.Freeform,
            MatchText: matchText,
            MatchDetailsText: matchDetailsText,
            MatchedCadPadIds: matchedCadIds,
            MatchedCadDetails: matchedCadDetails,
            RuleTrace: ruleTrace,
            MatchConfidence: confidence,
            DiffSource: diffSource)
        {
            FreeformSource = freeformSource,
            FreeformSourceDetail = freeformSourceDetail,
        };

        return new PadInspectorSnapshot(
            Kind: "regular",
            Cad: null,
            Regular: snapshot);
    }
}
