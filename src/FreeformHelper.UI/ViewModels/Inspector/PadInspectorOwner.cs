// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Inspector;

/// <summary>Owns the accepted inspector projection, stable UI child, and match cache.</summary>
internal sealed class PadInspectorOwner
{
    private readonly InspectorSnapshotPorts _snapshots;
    private readonly InspectorMatchPorts _matches;
    private readonly InspectorDisplayPorts _display;
    private readonly PadInspectorMatchCache _cache = new();

    // UI thread only. The display is replaced before its derived notifications are sent.
    private PadInspectorDisplay _padInspectorDisplay = PadInspectorDisplay.CreateEmpty();

    public PadInspectorOwner(
        InspectorSnapshotPorts snapshots,
        InspectorMatchPorts matches,
        InspectorDisplayPorts display,
        Func<PadInspectorRequestState, Action<bool>, IInspectorRefresh> createRefresh)
    {
        _snapshots = snapshots;
        _matches = matches;
        _display = display;
        PadInspectorUi = new PadInspectorUiState(OnPadInspectorSnapshotChanged);
        Refresh = createRefresh(Requests, value => PadInspectorUi.IsDeferredRefreshPending = value);
    }

    public PadInspectorRequestState Requests { get; } = new();
    public IInspectorRefresh Refresh { get; }
    public PadInspectorUiState PadInspectorUi { get; }

    public PadInspectorDisplay PadInspectorDisplay
    {
        get => _padInspectorDisplay;
        set
        {
            if (EqualityComparer<PadInspectorDisplay>.Default.Equals(_padInspectorDisplay, value))
            {
                return;
            }

            var oldValue = _padInspectorDisplay;
            _display.PropertyChanging(nameof(PadInspectorDisplay));
            _padInspectorDisplay = value;
            OnPadInspectorDisplayChanged(oldValue, value);
            _display.PropertyChanged(nameof(PadInspectorDisplay));
        }
    }

    public bool HasPadInspectorSummary => PadInspectorDisplay.HasSummary;
    public bool HasNoPadInspectorSummary => !HasPadInspectorSummary && !HasPadInspectorMultiSelection;
    public bool HasPadInspectorMultiSelection => _display.ReadSelectedCount() > 1;
    public string PadInspectorSelectionSummaryText => HasPadInspectorMultiSelection
        ? string.Create(CultureInfo.InvariantCulture, $"{_display.ReadSelectedCount()} pads selected")
        : string.Empty;
    public string PadInspectorPrimaryText => PadInspectorDisplay.PrimaryText;
    public string PadInspectorCompactHeaderText => PadInspectorDisplay.CompactHeaderText;
    public string PadInspectorSecondaryText => PadInspectorDisplay.SecondaryText;
    public string PadInspectorMatchText => PadInspectorDisplay.MatchText;
    public ObservableCollection<PadInspectorDisplayLine> PadInspectorCompensationLines => PadInspectorDisplay.CompensationLines;
    public ObservableCollection<string> PadInspectorRuleTraceLines => PadInspectorDisplay.RuleTraceLines;
    public ObservableCollection<PadInspectorRuleTraceSection> PadInspectorRuleTraceSections => PadInspectorDisplay.RuleTraceSections;
    public bool HasPadInspectorRuleTrace => PadInspectorDisplay.HasRuleTrace;

    public PadInspectorSnapshot? BuildCadPadInspectorSnapshot(
        int cadPadId,
        bool includeExpensiveNotchDetails = true,
        bool includeNotchRowEligibilityDetails = true,
        bool includeMatchDetails = true,
        bool includeRuleTrace = true,
        NotchV22CompensationResult? precomputedCompensation = null,
        string? precomputedDiagnostics = null)
    {
        var cadPad = _snapshots.FindCad(cadPadId);
        if (cadPad is null)
        {
            return null;
        }

        var workflow = _snapshots.CaptureWorkflow();
        var dxfIndex = workflow.GetOutputIndex(cadPadId);
        var cadOutputFwDiffOverride = _snapshots.GetOutputOverride(cadPadId);
        var isDxfIndexAnchor = _snapshots.IsOutputAnchor(cadPadId);
        var dxfIndexDisplayText = dxfIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var cadOutputFwDiffAssignmentModeText = PadInspectorMatchFormatter.BuildCadOutputFwDiffAssignmentModeText(
            cadOutputFwDiffOverride.HasValue,
            isDxfIndexAnchor,
            _snapshots.ReadAssignmentMode());
        var icIndex = workflow.GetIcIndex(cadPadId);
        var matchCache = GetOrBuildCadInspectorMatchCache(cadPadId);
        var (matchedRegularDetails, matchText, matchDetailsText) = PadInspectorSnapshotProjector.BuildCadMatchDisplay(
            matchCache, includeMatchDetails, includeRuleTrace);
        var notch = workflow.BuildNotch(
            cadPadId, includeExpensiveNotchDetails, precomputedCompensation, precomputedDiagnostics);
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

    public PadInspectorSnapshot? BuildRegularPadInspectorSnapshot(
        int regularPadId,
        bool includeMatchDetails = true,
        bool includeRuleTrace = true)
    {
        var regularPad = _snapshots.FindRegular(regularPadId);
        if (regularPad is null)
        {
            return null;
        }

        var matchCache = GetOrBuildRegularInspectorMatchCache(regularPad);
        return PadInspectorSnapshotProjector.BuildRegular(
            regularPad, matchCache, includeMatchDetails, includeRuleTrace, _snapshots.ToDisplayRow(regularPad.Row));
    }

    public void InvalidateDerivedCache()
    {
        Requests.BumpDerivedCacheRevision();
        _cache.Clear();
    }

    private CadInspectorMatchCacheEntry GetOrBuildCadInspectorMatchCache(int cadPadId)
    {
        if (_cache.CadMatches.TryGetValue(cadPadId, out var cached) &&
            cached.Revision == Requests.ReadDerivedCacheRevision())
        {
            return cached;
        }

        var matchedLinks = _matches.GetRegularLinks(cadPadId);
        var matchedRegularIds = _matches.GetRegularIds(cadPadId);
        var revision = Requests.ReadDerivedCacheRevision();
        var entry = PadInspectorMatchCacheEntryBuilder.BuildCad(
            revision, matchedLinks, matchedRegularIds, _matches.GetRegularIcDiff);
        _cache.StoreCadInspectorMatchCache(cadPadId, entry);
        return entry;
    }

    private RegularInspectorMatchCacheEntry GetOrBuildRegularInspectorMatchCache(RegularPad regularPad)
    {
        if (_cache.RegularMatches.TryGetValue(regularPad.RegularPadId, out var cached) &&
            cached.Revision == Requests.ReadDerivedCacheRevision())
        {
            return cached;
        }

        var matchedLinks = _matches.GetCadLinks(regularPad.RegularPadId);
        var matchedCadIds = _matches.GetCadIds(regularPad.RegularPadId);
        var revision = Requests.ReadDerivedCacheRevision();
        var entry = PadInspectorMatchCacheEntryBuilder.BuildRegular(
            revision, regularPad, matchedLinks, matchedCadIds, _matches.TryGetOutputIndex, _matches.ReadFreeformOverrides());
        _cache.StoreRegularInspectorMatchCache(regularPad.RegularPadId, entry);
        return entry;
    }

    private (NotchCadRowEligibility Eligibility, string Summary) BuildCadInspectorNotchRowSummary(
        CadPad cadPad, bool includeNotchRowEligibilityDetails)
    {
        if (!includeNotchRowEligibilityDetails)
        {
            return (PadInspectorMatchDetailBuilder.BuildDeferredNotchRowEligibilityPlaceholder(cadPad.Id), "Notch rows: computing...");
        }

        var notchEligibility = _snapshots.BuildRowEligibility(cadPad);
        return (notchEligibility, PadInspectorSnapshotProjector.FormatNotchRowSummary(notchEligibility));
    }

    private void OnPadInspectorSnapshotChanged(PadInspectorSnapshot? value)
    {
        var display = value is null ? null : _display.BuildDisplay(value);
        PadInspectorDisplay = display ?? PadInspectorDisplay.CreateEmpty();
        _display.WriteRawTraceExpanded(false);
    }

    private void OnPadInspectorDisplayChanged(PadInspectorDisplay? oldValue, PadInspectorDisplay newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        if (oldValue.HasSummary != newValue.HasSummary)
        {
            _display.PropertyChanged(nameof(HasPadInspectorSummary));
            _display.PropertyChanged(nameof(HasNoPadInspectorSummary));
        }

        RaiseWhenChanged(oldValue.CompactHeaderText, newValue.CompactHeaderText, nameof(PadInspectorCompactHeaderText));
        RaiseWhenChanged(oldValue.PrimaryText, newValue.PrimaryText, nameof(PadInspectorPrimaryText));
        RaiseWhenChanged(oldValue.SecondaryText, newValue.SecondaryText, nameof(PadInspectorSecondaryText));
        RaiseWhenChanged(oldValue.MatchText, newValue.MatchText, nameof(PadInspectorMatchText));
        RaiseWhenChanged(oldValue.CompensationLines, newValue.CompensationLines, nameof(PadInspectorCompensationLines));
        RaiseWhenChanged(oldValue.RuleTraceLines, newValue.RuleTraceLines, nameof(PadInspectorRuleTraceLines));
        RaiseWhenChanged(oldValue.RuleTraceSections, newValue.RuleTraceSections, nameof(PadInspectorRuleTraceSections));
        RaiseWhenChanged(oldValue.HasRuleTrace, newValue.HasRuleTrace, nameof(HasPadInspectorRuleTrace));
    }

    private void RaiseWhenChanged<T>(T oldValue, T newValue, string propertyName)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            _display.PropertyChanged(propertyName);
        }
    }
}
