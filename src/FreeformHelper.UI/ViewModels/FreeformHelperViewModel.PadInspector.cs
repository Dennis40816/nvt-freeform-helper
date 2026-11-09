using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
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
        var dxfIndex = workflowSnapshot.TryGetCadOutputFwDiffIndex(cadPadId, out var dxfIndexValue) ? dxfIndexValue : (int?)null;
        var cadOutputFwDiffOverride = GetCadOutputFwDiffIndexOverride(cadPadId);
        var isDxfIndexAnchor = IsCadOutputFwDiffIndexAnchorCadPad(cadPadId);
        var dxfIndexDisplayText = dxfIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        var cadOutputFwDiffAssignmentModeText = PadInspectorMatchFormatter.BuildCadOutputFwDiffAssignmentModeText(
            cadOutputFwDiffOverride.HasValue,
            isDxfIndexAnchor,
            CadOutputFwDiffAutoMode);

        var icIndex = workflowSnapshot.TryGetCadIcIndex(cadPadId, out var icIndexValue) ? icIndexValue : (int?)null;

        var matchCache = GetOrBuildCadInspectorMatchCache(cadPadId);
        var (matchedRegularDetails, matchText, matchDetailsText) = BuildCadInspectorMatchDisplay(
            matchCache,
            includeMatchDetails,
            includeRuleTrace);
        var notch = BuildCadInspectorNotch(
            cadPadId,
            includeExpensiveNotchDetails,
            workflowSnapshot,
            precomputedCompensation,
            precomputedDiagnostics);
        var (notchEligibility, notchRowSummary) = BuildCadInspectorNotchRowSummary(cadPad, includeNotchRowEligibilityDetails);

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

    private static (IReadOnlyList<PadInspectorMatchedRegularSnapshot> Details, string MatchText, string MatchDetailsText) BuildCadInspectorMatchDisplay(
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

    private PadInspectorNotchSnapshot BuildCadInspectorNotchSnapshot(
        NotchV22ResolvedResult resolved,
        bool includeExpensiveNotchDetails,
        string? precomputedDiagnostics)
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
                ? precomputedDiagnostics ?? BuildToFullDiagnosticsText(resolvedCompensation)
                : null)
        {
            TargetCoverageProjection = resolved.TargetAllocation.TargetCoverageProjection,
            ComputationMode = CurrentNotchComputationMode
        };
    }

    private (NotchCadRowEligibility Eligibility, string Summary) BuildCadInspectorNotchRowSummary(
        CadPad cadPad,
        bool includeNotchRowEligibilityDetails)
    {
        if (!includeNotchRowEligibilityDetails)
        {
            return (BuildDeferredNotchRowEligibilityPlaceholder(cadPad.Id), "Notch rows: computing...");
        }

        var notchEligibility = BuildCadNotchRowEligibility(cadPad);
        var eligibleVersionText = notchEligibility.EligibleVersions.Count == 0
            ? "-"
            : string.Join(", ", notchEligibility.EligibleVersions.Select(v => v.ToDisplayLabel()));
        var notchRowSummary = notchEligibility.HasRows
            ? $"Notch rows: {notchEligibility.EstimatedRowCount} ({eligibleVersionText})"
            : $"Notch rows: 0 ({notchEligibility.Reason})";
        return (notchEligibility, notchRowSummary);
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
            : PadInspectorRuleTraceBuilder.BuildDeferredRuleTracePlaceholder();

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

    /// <summary>
    /// Gets or sets the text and lines that the pad inspector panels show for the current snapshot.
    /// The properties below read from this one value.
    /// </summary>
    [ObservableProperty]
    private PadInspectorDisplay _padInspectorDisplay = PadInspectorDisplay.CreateEmpty();

    /// <summary>
    /// Gets whether the top-strip pad inspector summary is visible.
    /// </summary>
    public bool HasPadInspectorSummary => PadInspectorDisplay.HasSummary;

    public bool HasNoPadInspectorSummary => !HasPadInspectorSummary && !HasPadInspectorMultiSelection;

    public bool HasPadInspectorMultiSelection => SelectedPadCount > 1;

    public string PadInspectorSelectionSummaryText => HasPadInspectorMultiSelection
        ? string.Create(CultureInfo.InvariantCulture, $"{SelectedPadCount} pads selected")
        : string.Empty;

    /// <summary>
    /// Gets top-strip primary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorPrimaryText => PadInspectorDisplay.PrimaryText;

    /// <summary>
    /// Gets compact header text (ID + diff idx only) for workspace top strip.
    /// </summary>
    public string PadInspectorCompactHeaderText => PadInspectorDisplay.CompactHeaderText;

    /// <summary>
    /// Gets top-strip secondary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorSecondaryText => PadInspectorDisplay.SecondaryText;

    /// <summary>
    /// Gets top-strip match summary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorMatchText => PadInspectorDisplay.MatchText;

    /// <summary>
    /// Gets segmented notch/freeform summary lines for inspector panel.
    /// </summary>
    public ObservableCollection<PadInspectorDisplayLine> PadInspectorCompensationLines => PadInspectorDisplay.CompensationLines;

    /// <summary>
    /// Gets rule trace lines for current inspector snapshot.
    /// </summary>
    public ObservableCollection<string> PadInspectorRuleTraceLines => PadInspectorDisplay.RuleTraceLines;

    /// <summary>
    /// Gets grouped rule trace sections for current inspector snapshot.
    /// </summary>
    public ObservableCollection<PadInspectorRuleTraceSection> PadInspectorRuleTraceSections => PadInspectorDisplay.RuleTraceSections;

    /// <summary>
    /// Gets whether current inspector snapshot has rule trace lines.
    /// </summary>
    public bool HasPadInspectorRuleTrace => PadInspectorDisplay.HasRuleTrace;

    /// <summary>
    /// Gets or sets whether Debug raw section in inspector rule trace is expanded.
    /// </summary>
    public bool IsPadInspectorRawTraceExpanded
    {
        get => _inspectorPanel.IsPadInspectorRawTraceExpanded;
        set
        {
            if (_inspectorPanel.IsPadInspectorRawTraceExpanded == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { IsPadInspectorRawTraceExpanded = value };
            OnPropertyChanged();
        }
    }
}
