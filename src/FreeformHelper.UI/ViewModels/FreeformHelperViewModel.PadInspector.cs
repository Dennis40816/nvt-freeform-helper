using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
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
        var (matchedRegularDetails, matchText, matchDetailsText) = PadInspectorSnapshotProjector.BuildCadMatchDisplay(
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

        return PadInspectorSnapshotProjector.BuildCad(
            cadPad,
            dxfIndex,
            cadOutputFwDiffOverride,
            isDxfIndexAnchor,
            dxfIndexDisplayText,
            cadOutputFwDiffAssignmentModeText,
            icIndex,
            matchCache,
            matchedRegularDetails,
            matchText,
            matchDetailsText,
            notch,
            notchEligibility,
            notchRowSummary,
            includeRuleTrace);
    }

    private PadInspectorNotchSnapshot BuildCadInspectorNotchSnapshot(
        NotchV22ResolvedResult resolved,
        bool includeExpensiveNotchDetails,
        string? precomputedDiagnostics)
    {
        return PadInspectorSnapshotProjector.BuildNotch(
            resolved,
            includeExpensiveNotchDetails,
            precomputedDiagnostics,
            BuildToFullDiagnosticsText,
            CurrentNotchComputationMode);
    }

    private (NotchCadRowEligibility Eligibility, string Summary) BuildCadInspectorNotchRowSummary(
        CadPad cadPad,
        bool includeNotchRowEligibilityDetails)
    {
        if (!includeNotchRowEligibilityDetails)
        {
            return (PadInspectorMatchDetailBuilder.BuildDeferredNotchRowEligibilityPlaceholder(cadPad.Id), "Notch rows: computing...");
        }

        var notchEligibility = BuildCadNotchRowEligibility(cadPad);
        return (notchEligibility, PadInspectorSnapshotProjector.FormatNotchRowSummary(notchEligibility));
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
        return PadInspectorSnapshotProjector.BuildRegular(
            regularPad,
            matchCache,
            includeMatchDetails,
            includeRuleTrace,
            ToDisplayRow(regularPad.Row));
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
