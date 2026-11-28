using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.ViewModels.Inspector;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // UI thread only; lazy composition does not publish notifications or start work.
    private PadInspectorOwner? _padInspectorOwner;

    private PadInspectorOwner Inspector => _padInspectorOwner ??= new PadInspectorOwner(
        new InspectorSnapshotPorts(
            id => CadPads.FirstOrDefault(pad => pad.Id == id),
            id => RegularPads.FirstOrDefault(pad => pad.RegularPadId == id),
            CaptureInspectorCadWorkflow,
            GetCadOutputFwDiffIndexOverride,
            IsCadOutputFwDiffIndexAnchorCadPad,
            () => CadOutputFwDiffAutoMode,
            BuildCadNotchRowEligibility,
            ToDisplayRow),
        new InspectorMatchPorts(
            GetMatchedRegularLinks,
            GetMatchedRegularPadIds,
            GetRegularPadIcDiff,
            GetMatchedCadLinks,
            GetMatchedCadPadIds,
            TryGetCadOutputFwDiffIndex,
            () => _projectFile.FreeformOverrides),
        new InspectorDisplayPorts(
            name => OnPropertyChanging(name),
            name => OnPropertyChanged(name),
            BuildPadInspectorDisplay,
            () => SelectedPadCount,
            value => IsPadInspectorRawTraceExpanded = value),
        CreateInspectorRefreshOwner);

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
        return Inspector.BuildCadPadInspectorSnapshot(
            cadPadId, includeExpensiveNotchDetails, includeNotchRowEligibilityDetails,
            includeMatchDetails, includeRuleTrace, precomputedCompensation, precomputedDiagnostics);
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

    public PadInspectorSnapshot? BuildRegularPadInspectorSnapshot(
        int regularPadId,
        bool includeMatchDetails = true,
        bool includeRuleTrace = true)
    {
        return Inspector.BuildRegularPadInspectorSnapshot(regularPadId, includeMatchDetails, includeRuleTrace);
    }

    /// <summary>
    /// Gets or sets the text and lines that the pad inspector panels show for the current snapshot.
    /// The properties below read from this one value.
    /// </summary>
    public PadInspectorDisplay PadInspectorDisplay
    {
        get => Inspector.PadInspectorDisplay;
        set => Inspector.PadInspectorDisplay = value;
    }

    /// <summary>
    /// Gets whether the top-strip pad inspector summary is visible.
    /// </summary>
    public bool HasPadInspectorSummary => Inspector.HasPadInspectorSummary;

    public bool HasNoPadInspectorSummary => Inspector.HasNoPadInspectorSummary;

    public bool HasPadInspectorMultiSelection => Inspector.HasPadInspectorMultiSelection;

    public string PadInspectorSelectionSummaryText => Inspector.PadInspectorSelectionSummaryText;

    /// <summary>
    /// Gets top-strip primary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorPrimaryText => Inspector.PadInspectorPrimaryText;

    /// <summary>
    /// Gets compact header text (ID + diff idx only) for workspace top strip.
    /// </summary>
    public string PadInspectorCompactHeaderText => Inspector.PadInspectorCompactHeaderText;

    /// <summary>
    /// Gets top-strip secondary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorSecondaryText => Inspector.PadInspectorSecondaryText;

    /// <summary>
    /// Gets top-strip match summary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorMatchText => Inspector.PadInspectorMatchText;

    /// <summary>
    /// Gets segmented notch/freeform summary lines for inspector panel.
    /// </summary>
    public ObservableCollection<PadInspectorDisplayLine> PadInspectorCompensationLines => Inspector.PadInspectorCompensationLines;

    /// <summary>
    /// Gets rule trace lines for current inspector snapshot.
    /// </summary>
    public ObservableCollection<string> PadInspectorRuleTraceLines => Inspector.PadInspectorRuleTraceLines;

    /// <summary>
    /// Gets grouped rule trace sections for current inspector snapshot.
    /// </summary>
    public ObservableCollection<PadInspectorRuleTraceSection> PadInspectorRuleTraceSections => Inspector.PadInspectorRuleTraceSections;

    /// <summary>
    /// Gets whether current inspector snapshot has rule trace lines.
    /// </summary>
    public bool HasPadInspectorRuleTrace => Inspector.HasPadInspectorRuleTrace;

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
