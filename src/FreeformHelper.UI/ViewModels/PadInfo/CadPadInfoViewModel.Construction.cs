using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CadPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    private readonly DisplayValues _display;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadPadInfoViewModel" /> class.
    /// </summary>
    /// <param name="pads">A list of <see cref="CadPad" /> objects to display information for.</param>
    /// <param name="getCustomValue">A function to retrieve the custom value for a given CAD pad ID.</param>
    /// <param name="applyCustomValue">An action to apply a new custom value to a list of CAD pad IDs.</param>
    /// <param name="closePadInfo">An action to close the pad information popup.</param>
    public CadPadInfoViewModel(
        IReadOnlyList<CadPad> pads,
        Func<int, int?> getDxfIndex,
        Func<int, int?>? getCadOutputFwDiffOverride,
        Func<int, bool>? isDxfIndexAnchor,
        Func<int, double> getCustomValue,
        Action<IReadOnlyList<int>, decimal>? applyCustomValue,
        Action<int, int>? setCadOutputFwDiffOverride,
        Action<int>? clearCadOutputFwDiffOverride,
        Action<int>? setDxfIndexAnchor,
        Action<int>? clearDxfIndexAnchor,
        Action? closePadInfo = null,
        ICommand? notchDetailCommand = null,
        Action? selectAreaBucket = null,
        Func<int, IReadOnlyList<int>>? getMatchedRegularPadIds = null,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedRegularLinks = null,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff = null,
        Action<int>? focusCadMatches = null,
        Action<int>? highlightCadMatches = null,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads = null,
        Action<IReadOnlyCollection<int>>? highlightRegularPads = null,
        Func<int, (double ToRegularRatio, double ToFullRatio, double CombinedRatio, bool IsToFullEnabled)?>? getCadV22CompensationPreview = null,
        Func<int, string?>? getCadV22CompensationDiagnostics = null,
        Func<int, NotchV22TargetAllocationSummary?>? getCadV22TargetAllocationSummary = null,
        CadOutputFwDiffAutoMode cadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
        PadInspectorSnapshot? inspectorSnapshot = null,
        NotchComputationMode notchComputationMode = NotchComputationMode.CadAllocation)
    {
        _cadOutputFwDiffAutoMode = cadOutputFwDiffAutoMode;
        _getDxfIndex = getDxfIndex;
        _applyCustomValue = applyCustomValue;
        _closePadInfo = closePadInfo;
        _focusCadMatches = focusCadMatches;
        _highlightCadMatches = highlightCadMatches;
        _highlightCadOwnerPads = highlightCadOwnerPads;
        _highlightRegularPads = highlightRegularPads;
        _setCadOutputFwDiffOverride = setCadOutputFwDiffOverride;
        _clearCadOutputFwDiffOverride = clearCadOutputFwDiffOverride;
        _getIsDxfIndexAnchor = isDxfIndexAnchor;
        _setDxfIndexAnchor = setDxfIndexAnchor;
        _clearDxfIndexAnchor = clearDxfIndexAnchor;
        _padIds = pads.Select(p => p.Id).ToList();
        _display = BuildDisplay(
            pads, getDxfIndex, getCadOutputFwDiffOverride, isDxfIndexAnchor,
            getMatchedRegularPadIds, getMatchedRegularLinks, getRegularPadIcDiff,
            _highlightCadOwnerPads, _highlightRegularPads, getCadV22CompensationPreview,
            getCadV22CompensationDiagnostics, getCadV22TargetAllocationSummary,
            inspectorSnapshot, notchComputationMode);
        _singleCadPadId = IsMulti ? null : pads[0].Id;
        _allNotchOwnerCadIds = _display.Notch.AllNotchOwnerCadIds;
        DxfIndexText = _display.Identity.DxfIndexText;
        IsDxfIndexAnchor = _display.Identity.IsDxfIndexAnchor;
        HasFocusMatchAction = _display.Match.HasMatches && _focusCadMatches is not null;
        HasHighlightMatchAction = _display.Match.HasMatches && _highlightCadMatches is not null;
        CadOutputFwDiffOverride = Math.Clamp(_display.Identity.CadOutputFwDiffOverrideValue ?? _display.Identity.DxfIndexFallback, 0, 1_000_000);
        HasCadOutputFwDiffOverride = _display.Identity.CadOutputFwDiffOverrideValue.HasValue;
        HasCadOutputFwDiffOverrideEditor = !IsMulti && _singleCadPadId.HasValue && _setCadOutputFwDiffOverride is not null;
        HasDxfIndexAnchorEditor = !IsMulti && _singleCadPadId.HasValue && _setDxfIndexAnchor is not null;
        InitializeCustomValue(pads, getCustomValue);

        ApplyChangesCommand = new RelayCommand(ApplyChanges);
        DiscardChangesCommand = new RelayCommand(DiscardChanges);
        ApplyCadOutputFwDiffOverrideCommand = new RelayCommand(ApplyCadOutputFwDiffOverride);
        ClearCadOutputFwDiffOverrideCommand = new RelayCommand(ClearCadOutputFwDiffOverride);
        SetDxfIndexAnchorCommand = new RelayCommand(SetDxfIndexAnchor);
        ClearDxfIndexAnchorCommand = new RelayCommand(ClearDxfIndexAnchor);
        NotchDetailCommand = notchDetailCommand;
        SelectAreaBucketCommand = selectAreaBucket is null ? null : new RelayCommand(selectAreaBucket);
        LocateMatchCommand = new RelayCommand(LocateMatch);
        HighlightAllNotchOwnersCommand = new RelayCommand(HighlightAllNotchOwners);
        ToggleLowFrequencyDetailSectionCommand = new RelayCommand(ToggleLowFrequencyDetailSection);
        ShowGeometryDetails = false;
    }

    private static DisplayValues BuildDisplay(
        IReadOnlyList<CadPad> pads,
        Func<int, int?> getDxfIndex,
        Func<int, int?>? getCadOutputFwDiffOverride,
        Func<int, bool>? isDxfIndexAnchor,
        Func<int, IReadOnlyList<int>>? getMatchedRegularPadIds,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedRegularLinks,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads,
        Action<IReadOnlyCollection<int>>? highlightRegularPads,
        Func<int, (double ToRegularRatio, double ToFullRatio, double CombinedRatio, bool IsToFullEnabled)?>? getCadV22CompensationPreview,
        Func<int, string?>? getCadV22CompensationDiagnostics,
        Func<int, NotchV22TargetAllocationSummary?>? getCadV22TargetAllocationSummary,
        PadInspectorSnapshot? inspectorSnapshot,
        NotchComputationMode notchComputationMode)
    {
        var selectedCount = pads.Count;
        var isMulti = pads.Count > 1;
        var snapshotCad = !isMulti &&
                          inspectorSnapshot?.Cad is CadPadInspectorSnapshot candidate &&
                          candidate.CadPadId == pads[0].Id
            ? candidate
            : null;
        var display = BuildIdentityDisplay(pads, getDxfIndex, isMulti, selectedCount);
        if (display.Identity.IsMulti)
        {
            return BuildMultiSelectionDisplay(pads, display);
        }

        var pad = pads[0];
        display = BuildPadGeometryDisplay(pad, display);
        if (snapshotCad is not null)
        {
            return BuildSnapshotDisplay(pad.Id, snapshotCad, display, highlightRegularPads, highlightCadOwnerPads);
        }

        return BuildCallbackDisplay(
            pad.Id, display, getMatchedRegularLinks, getMatchedRegularPadIds,
            getRegularPadIcDiff, getCadOutputFwDiffOverride, isDxfIndexAnchor,
            getCadV22CompensationPreview, getCadV22CompensationDiagnostics,
            getCadV22TargetAllocationSummary, highlightRegularPads, highlightCadOwnerPads, notchComputationMode);
    }

    private static DisplayValues BuildIdentityDisplay(
        IReadOnlyList<CadPad> pads,
        Func<int, int?> getDxfIndex,
        bool isMulti,
        int selectedCount)
    {
        var subtitle = isMulti ? $"Selected: {selectedCount} pads" : "Single pad";
        var layerText = isMulti ? PadInfoTextFormatter.DistinctText(pads.Select(p => p.Layer)) : pads[0].Layer;
        var nameText = isMulti ? PadInfoTextFormatter.DistinctText(pads.Select(p => p.Name)) : pads[0].Name;
        var dxfIndices = pads
            .Select(p => getDxfIndex(p.Id))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        var dxfIndexText = PadInfoTextFormatter.RangeText(dxfIndices);
        var cadIdText = isMulti ? PadInfoTextFormatter.RangeText(pads.Select(p => p.Id)) : pads[0].Id.ToString(CultureInfo.InvariantCulture);
        var identityText = isMulti
            ? $"CAD pads · {subtitle}"
            : $"CAD {pads[0].Id} · Diff idx {dxfIndexText}";
        return new DisplayValues(
            new IdentityDisplayValues(
                IsMulti: isMulti,
                SelectedCount: selectedCount,
                Title: isMulti ? "CAD pads" : "CAD pad",
                Subtitle: subtitle,
                LayerText: layerText,
                NameText: nameText,
                DxfIndexText: dxfIndexText,
                CadIdText: cadIdText,
                IdentityText: identityText,
                DxfIndexFallback: dxfIndices.Count > 0 ? dxfIndices[0] : 0),
            new MatchDisplayValues(),
            new NotchDisplayValues(
                TargetAllocationLines: new List<string>(),
                TargetAllocationItems: new List<PadInfoTargetAllocationViewModel>(),
                NotchOwnerLinks: new List<NotchOwnerLinkViewModel>(),
                AllNotchOwnerCadIds: new List<int>(),
                RuleTraceLines: new List<string>()));
    }

    private static DisplayValues BuildMultiSelectionDisplay(IReadOnlyList<CadPad> pads, DisplayValues display)
    {
        var areas = pads.Select(p => p.Area).ToList();
        return display with
        {
            Identity = display.Identity with
            {
                AreaText = $"{areas.Min():0.####} - {areas.Max():0.####} {PadInfoUnits.SquareMillimeter}",
                CentroidText = "Mixed",
                VertexText = PadInfoTextFormatter.RangeText(pads.Select(p => p.Polygon.Vertices.Length)),
            },
            Match = display.Match with
            {
                MatchText = "Mixed",
                MatchDetailsText = "Mixed",
                GeometrySeedValueText = "Mixed",
            },
        };
    }

    private static DisplayValues BuildPadGeometryDisplay(CadPad pad, DisplayValues display)
    {
        return display with
        {
            Identity = display.Identity with
            {
                AreaText = $"{pad.Area:0.####} {PadInfoUnits.SquareMillimeter}",
                CentroidText = FormatPoint(pad.Centroid),
                VertexText = pad.Polygon.Vertices.Length.ToString(CultureInfo.InvariantCulture),
            },
        };
    }

    private static DisplayValues BuildSnapshotDisplay(
        int cadPadId,
        CadPadInspectorSnapshot snapshotCad,
        DisplayValues display,
        Action<IReadOnlyCollection<int>>? highlightRegularPads,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads)
    {
        var icText = snapshotCad.IcIndex.HasValue
            ? (snapshotCad.IcIndex.Value + 1).ToString(CultureInfo.InvariantCulture)
            : "-";
        display = display with
        {
            Match = display.Match with
            {
                MatchText = snapshotCad.MatchText,
                MatchDetailsText = snapshotCad.MatchDetailsText,
                GeometrySeedValueText = snapshotCad.MatchText,
                MatchConfidenceText = snapshotCad.MatchedRegularPadIds.Count == 0
                    ? "-"
                    : snapshotCad.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture),
                HasMatches = snapshotCad.MatchedRegularPadIds.Count > 0,
            },
            Identity = display.Identity with
            {
                DxfIndexText = snapshotCad.DxfIndexDisplayText,
                CadOutputFwDiffOverrideValue = snapshotCad.CadOutputFwDiffOverride,
                IsDxfIndexAnchor = snapshotCad.IsDxfIndexAnchor,
                IdentityText = $"CAD {cadPadId} · IC {icText} · Diff idx {snapshotCad.DxfIndexDisplayText}",
            },
        };
        if (snapshotCad.Notch is { } resolvedDisplayInput)
        {
            display = display with { Notch = display.Notch with { NotchRowSummaryText = snapshotCad.NotchRowSummary } };
            display = BuildNotchDisplay(
                display, NotchDisplayProjector.Build(resolvedDisplayInput), cadPadId,
                highlightRegularPads, highlightCadOwnerPads);
        }

        return display with
        {
            Notch = display.Notch with
            {
                RuleTraceLines = snapshotCad.RuleTrace.Select(PadInspectorRuleTraceFormatter.Format).ToList(),
            },
        };
    }

    private static DisplayValues BuildCallbackDisplay(
        int cadPadId,
        DisplayValues display,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedRegularLinks,
        Func<int, IReadOnlyList<int>>? getMatchedRegularPadIds,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff,
        Func<int, int?>? getCadOutputFwDiffOverride,
        Func<int, bool>? isDxfIndexAnchor,
        Func<int, (double ToRegularRatio, double ToFullRatio, double CombinedRatio, bool IsToFullEnabled)?>? getCadV22CompensationPreview,
        Func<int, string?>? getCadV22CompensationDiagnostics,
        Func<int, NotchV22TargetAllocationSummary?>? getCadV22TargetAllocationSummary,
        Action<IReadOnlyCollection<int>>? highlightRegularPads,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads,
        NotchComputationMode notchComputationMode)
    {
        var matchedLinks = getMatchedRegularLinks?.Invoke(cadPadId)
            .OrderByDescending(link => link.RegularCoverage)
            .ThenByDescending(link => link.CadCoverage)
            .ThenBy(link => link.RegularPadId)
            .ToList()
            ?? new List<PadMatchLink>();
        var matchedRegularIds = (getMatchedRegularPadIds?.Invoke(cadPadId) ?? Array.Empty<int>())
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var matchText = BuildCadMatchText(matchedRegularIds, matchedLinks, getRegularPadIcDiff);
        var matchDetailsText = BuildCadMatchDetailsText(matchedRegularIds, matchedLinks, getRegularPadIcDiff);
        var cadOutputFwDiffOverrideValue = getCadOutputFwDiffOverride?.Invoke(cadPadId);
        var isAnchor = isDxfIndexAnchor?.Invoke(cadPadId) ?? false;
        display = display with
        {
            Match = display.Match with
            {
                MatchText = matchText,
                MatchDetailsText = matchDetailsText,
                GeometrySeedValueText = matchText,
                HasMatches = matchedRegularIds.Count > 0,
            },
            Identity = display.Identity with
            {
                CadOutputFwDiffOverrideValue = cadOutputFwDiffOverrideValue,
                IsDxfIndexAnchor = isAnchor,
            },
        };
        return BuildCallbackNotchDisplay(
            cadPadId, display, getCadV22CompensationPreview, getCadV22CompensationDiagnostics,
            getCadV22TargetAllocationSummary, highlightRegularPads, highlightCadOwnerPads, notchComputationMode);
    }

    private static DisplayValues BuildCallbackNotchDisplay(
        int cadPadId,
        DisplayValues display,
        Func<int, (double ToRegularRatio, double ToFullRatio, double CombinedRatio, bool IsToFullEnabled)?>? getCadV22CompensationPreview,
        Func<int, string?>? getCadV22CompensationDiagnostics,
        Func<int, NotchV22TargetAllocationSummary?>? getCadV22TargetAllocationSummary,
        Action<IReadOnlyCollection<int>>? highlightRegularPads,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads,
        NotchComputationMode notchComputationMode)
    {
        var compensation = getCadV22CompensationPreview?.Invoke(cadPadId);
        if (!compensation.HasValue)
        {
            return display;
        }

        display = display with { Notch = display.Notch with { NotchRowSummaryText = "Notch rows: unknown (open from selection inspector)" } };
        var allocation = getCadV22TargetAllocationSummary?.Invoke(cadPadId);
        var diagnostics = getCadV22CompensationDiagnostics?.Invoke(cadPadId);
        var notchDisplay = NotchDisplayProjector.Build(
            compensation.Value.ToRegularRatio,
            compensation.Value.ToFullRatio,
            compensation.Value.CombinedRatio,
            compensation.Value.IsToFullEnabled,
            allocation?.Stage3Area,
            allocation?.Targets.Select(static target => new NotchDisplayTargetInput(
                target.IcIndex,
                target.DiffIndex,
                target.EffectiveArea,
                target.Ratio,
                target.RatioPercentRounded,
                target.PassesStrictThreshold,
                target.IsAnchorDiff,
                target.ToFullAppliedRegularCount,
                target.RegularCount,
                target.RegularAreas.Select(static area => new NotchDisplayRegularAreaInput(
                    area.RegularPadId,
                    area.EffectiveArea)).ToList(),
                target.RegularPadIds)).ToList(),
            diagnostics,
            allocation?.TargetCoverageProjection,
            notchComputationMode);
        return BuildNotchDisplay(display, notchDisplay, cadPadId, highlightRegularPads, highlightCadOwnerPads);
    }

    private static DisplayValues BuildNotchDisplay(
        DisplayValues display,
        NotchDisplayProjection notchDisplay,
        int cadPadId,
        Action<IReadOnlyCollection<int>>? highlightRegularPads,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads)
    {
        var targetAllocationLines = notchDisplay.TargetAllocationLines.ToList();
        var targetAllocationItems = notchDisplay.TargetAllocationItems.Select(item =>
            PadInfoTargetAllocationViewModel.FromDisplayItem(item, highlightRegularPads)).ToList();
        var notchOwnerLinks = new List<NotchOwnerLinkViewModel>();
        var allNotchOwnerCadIds = new List<int>();
        BuildNotchOwnerLinks(
            notchDisplay.DiagnosticEntries, cadPadId, highlightCadOwnerPads,
            notchOwnerLinks, allNotchOwnerCadIds);
        return display with
        {
            Notch = display.Notch with
            {
                ToRegularRatioText = notchDisplay.ToRegularRatioText,
                ToFullRatioText = notchDisplay.ToFullRatioText,
                ToRegularValueText = notchDisplay.ToRegularValueText,
                ToFullValueText = notchDisplay.ToFullValueText,
                CombinedValueText = notchDisplay.CombinedValueText,
                Stage3AreaText = notchDisplay.Stage3AreaText,
                TargetAllocationSummaryText = notchDisplay.TargetAllocationSummaryText,
                TargetAllocationLines = targetAllocationLines,
                TargetAllocationItems = targetAllocationItems,
                ToFullDiagnosticsText = notchDisplay.ToFullDiagnosticsText,
                ToFullReasonFullText = notchDisplay.ToFullReasonFullText,
                ToFullReasonShortText = notchDisplay.ToFullReasonShortText,
                NotchOwnerLinks = notchOwnerLinks,
                AllNotchOwnerCadIds = allNotchOwnerCadIds,
                OwnerSummaryText = notchDisplay.OwnerSummaryText,
            },
        };
    }

    private void InitializeCustomValue(IReadOnlyList<CadPad> pads, Func<int, double> getCustomValue)
    {
        // Handle custom values: check for mixed state and set initial value.
        var values = pads.Select(p => (decimal)getCustomValue(p.Id)).Distinct().ToList();
        _isLoading = true; // Suppress dirty tracking during initial load.
        CustomValue = values.Count == 1 ? values[0] : 0m; // If not mixed, use the common value; otherwise, default to 0.
        _isLoading = false;
        _customValueInitial = CustomValue;
        _customValueDirty = false;
    }

    private sealed record DisplayValues(
        IdentityDisplayValues Identity,
        MatchDisplayValues Match,
        NotchDisplayValues Notch);

    private sealed record IdentityDisplayValues(
        bool IsMulti,
        int SelectedCount,
        string Title,
        string Subtitle,
        string LayerText,
        string NameText,
        string DxfIndexText,
        string CadIdText,
        string IdentityText,
        int DxfIndexFallback,
        string AreaText = "",
        string CentroidText = "",
        string VertexText = "",
        int? CadOutputFwDiffOverrideValue = null,
        bool IsDxfIndexAnchor = false);

    private sealed record MatchDisplayValues(
        string MatchText = "Unmatched",
        string MatchDetailsText = "Unmatched",
        string MatchConfidenceText = "-",
        string GeometrySeedValueText = "Unmatched",
        bool HasMatches = false);

    private sealed record NotchDisplayValues(
        IReadOnlyList<string> TargetAllocationLines,
        IReadOnlyList<PadInfoTargetAllocationViewModel> TargetAllocationItems,
        IReadOnlyList<NotchOwnerLinkViewModel> NotchOwnerLinks,
        IReadOnlyList<int> AllNotchOwnerCadIds,
        IReadOnlyList<string> RuleTraceLines,
        string NotchRowSummaryText = "Notch rows: -",
        string ToRegularRatioText = "Undo NF (To Regular): -",
        string ToFullRatioText = "To Full: -",
        string ToRegularValueText = "-",
        string ToFullValueText = "-",
        string CombinedValueText = "-",
        string ToFullReasonShortText = "",
        string ToFullReasonFullText = "",
        string Stage3AreaText = "Stage3 area: -",
        string TargetAllocationSummaryText = "Targets: -",
        string OwnerSummaryText = "",
        string ToFullDiagnosticsText = "");
}
