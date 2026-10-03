using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const int MaxInspectorMatchDetailLines = 8;
    private static readonly char[] ToFullDiagnosticsLineSeparators = ['\r', '\n'];
    private static readonly char[] ToFullDiagnosticsOwnerSeparators = ['|', ';'];

    internal NotchComputationMode CurrentNotchComputationMode => _projectFile.Settings.Notch.ComputationMode;

    public PadInspectorSnapshot? BuildCadPadInspectorSnapshot(
        int cadPadId,
        bool includeExpensiveNotchDetails = true,
        bool includeNotchRowEligibilityDetails = true,
        bool includeMatchDetails = true,
        bool includeRuleTrace = true,
        NotchV22CompensationResult? precomputedCompensation = null,
        string? precomputedDiagnostics = null)
    {
        var cadPad = CadPads.FirstOrDefault(pad => pad.Id == cadPadId);
        if (cadPad is null)
        {
            return null;
        }

        var workflowSnapshot = BuildWorkflowDataSnapshot();
        var hasDxfIndex = workflowSnapshot.TryGetCadOutputFwDiffIndex(cadPadId, out var dxfIndexValue);
        var dxfIndex = hasDxfIndex ? dxfIndexValue : (int?)null;
        var cadOutputFwDiffOverride = GetCadOutputFwDiffIndexOverride(cadPadId);
        var isDxfIndexAnchor = IsCadOutputFwDiffIndexAnchorCadPad(cadPadId);
        var dxfIndexDisplayText = dxfIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        var cadOutputFwDiffAssignmentModeText = BuildCadOutputFwDiffAssignmentModeText(
            cadOutputFwDiffOverride.HasValue,
            isDxfIndexAnchor,
            CadOutputFwDiffAutoMode);

        var hasIcIndex = workflowSnapshot.TryGetCadIcIndex(cadPadId, out var icIndexValue);
        var icIndex = hasIcIndex ? icIndexValue : (int?)null;

        var matchCache = GetOrBuildCadInspectorMatchCache(cadPadId);
        var matchedRegularIds = matchCache.MatchedRegularIds;
        var matchedRegularDetails = includeMatchDetails || includeRuleTrace
            ? matchCache.MatchedRegularDetails
            : (IReadOnlyList<PadInspectorMatchedRegularSnapshot>)Array.Empty<PadInspectorMatchedRegularSnapshot>();
        var matchText = matchCache.MatchText;
        var matchDetailsText = includeMatchDetails
            ? matchCache.MatchDetailsText
            : matchedRegularIds.Count == 0
                ? "Unmatched"
                : $"Matched regular pad(s): {matchedRegularIds.Count}.";

        CadPad notchCad = null!;
        IReadOnlyList<CadPad> allCadPads = Array.Empty<CadPad>();
        IReadOnlySet<int>? activeRegularPadIds = null;
        var hasNotchInputs = !includeExpensiveNotchDetails && TryResolveNotchComputationInputs(
            cadPadId,
            out notchCad,
            out allCadPads,
            out activeRegularPadIds);
        var resolved = includeExpensiveNotchDetails
            ? GetCadV22ResolvedResult(cadPadId)
            : hasNotchInputs && _notchResolvedResultCacheByCadId.ContainsKey(cadPadId)
                ? GetOrBuildCadV22ResolvedResultCached(
                    notchCad,
                    allCadPads,
                    activeRegularPadIds,
                    workflowSnapshot,
                    buildIfMissing: false)
                : null;
        var compensationResult = precomputedCompensation ?? resolved?.Compensation;
        PadInspectorNotchSnapshot? notch = null;
        if (compensationResult is not null &&
            resolved is null &&
            (hasNotchInputs ||
             TryResolveNotchComputationInputs(
                 cadPadId,
                 out notchCad,
                 out allCadPads,
                 out activeRegularPadIds)))
        {
            resolved = GetOrBuildCadV22ResolvedResultCached(
                notchCad,
                allCadPads,
                activeRegularPadIds,
                workflowSnapshot,
                precomputedCompensation: compensationResult);
        }

        if (resolved is not null)
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
            notch = new PadInspectorNotchSnapshot(
                resolvedCompensation.ToRegularRatio,
                resolvedCompensation.ToFullRatio,
                resolvedCompensation.CombinedRatio,
                resolvedCompensation.IsToFullEnabled,
                stage3Area,
                strictAreaThreshold,
                targets,
                includeExpensiveNotchDetails
                    ? precomputedDiagnostics ?? BuildToFullDiagnosticsText(resolvedCompensation)
                    : null)
            {
                TargetCoverageProjection = resolved.TargetAllocation.TargetCoverageProjection,
                ComputationMode = CurrentNotchComputationMode
            };
        }
        else if (!includeExpensiveNotchDetails && _grid is not null)
        {
            Logger.Debug(System.Globalization.CultureInfo.InvariantCulture, "Pad inspector fast snapshot: CAD={0} skipped cold Notch 2.2 compute (cache miss).",
                cadPadId);
        }

        NotchCadRowEligibility notchEligibility;
        string notchRowSummary;
        if (includeNotchRowEligibilityDetails)
        {
            notchEligibility = BuildCadNotchRowEligibility(cadPad);
            var eligibleVersionText = notchEligibility.EligibleVersions.Count == 0
                ? "-"
                : string.Join(", ", notchEligibility.EligibleVersions.Select(v => v.ToDisplayLabel()));
            notchRowSummary = notchEligibility.HasRows
                ? $"Notch rows: {notchEligibility.EstimatedRowCount} ({eligibleVersionText})"
                : $"Notch rows: 0 ({notchEligibility.Reason})";
        }
        else
        {
            notchEligibility = BuildDeferredNotchRowEligibilityPlaceholder(cadPad.Id);
            notchRowSummary = "Notch rows: computing...";
        }

        var confidence = matchCache.Confidence;
        var ruleTrace = includeRuleTrace
            ? BuildCadRuleTrace(
                dxfIndex,
                cadOutputFwDiffAssignmentModeText,
                matchedRegularDetails,
                notch,
                notchEligibility)
            : BuildDeferredRuleTracePlaceholder();

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
            MatchedRegularPadIds: matchedRegularIds,
            MatchedRegularDetails: matchedRegularDetails,
            IsNotchRowEligible: notchEligibility.HasRows,
            NotchRowSummary: notchRowSummary,
            Notch: notch,
            RuleTrace: ruleTrace,
            MatchConfidence: confidence);

        return new PadInspectorSnapshot(
            Kind: "cad",
            Cad: snapshot,
            Regular: null);
    }

    public PadInspectorSnapshot? BuildRegularPadInspectorSnapshot(
        int regularPadId,
        bool includeMatchDetails = true,
        bool includeRuleTrace = true)
    {
        var regularPad = RegularPads.FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regularPad is null)
        {
            return null;
        }

        var matchCache = GetOrBuildRegularInspectorMatchCache(regularPad);
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
            : BuildDeferredRuleTracePlaceholder();

        var snapshot = new RegularPadInspectorSnapshot(
            RegularPadId: regularPad.RegularPadId,
            Index: regularPad.Index,
            Row: regularPad.Row,
            Col: regularPad.Col,
            DisplayRow: ToDisplayRow(regularPad.Row),
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
