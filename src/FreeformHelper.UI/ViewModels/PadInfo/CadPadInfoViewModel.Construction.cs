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
        SelectedCount = pads.Count;
        IsMulti = pads.Count > 1;
        _singleCadPadId = IsMulti ? null : pads[0].Id;

        Title = IsMulti ? "CAD pads" : "CAD pad";
        Subtitle = IsMulti ? $"Selected: {SelectedCount} pads" : "Single pad";
        var snapshotCad = !IsMulti &&
                          inspectorSnapshot?.Cad is CadPadInspectorSnapshot candidate &&
                          candidate.CadPadId == pads[0].Id
            ? candidate
            : null;

        // Populate display texts based on whether it's a multi-selection or single.
        LayerText = IsMulti ? PadInfoTextFormatter.DistinctText(pads.Select(p => p.Layer)) : pads[0].Layer;
        NameText = IsMulti ? PadInfoTextFormatter.DistinctText(pads.Select(p => p.Name)) : pads[0].Name;

        var dxfIndices = pads
            .Select(p => getDxfIndex(p.Id))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        DxfIndexText = PadInfoTextFormatter.RangeText(dxfIndices);
        CadIdText = IsMulti ? PadInfoTextFormatter.RangeText(pads.Select(p => p.Id)) : pads[0].Id.ToString(CultureInfo.InvariantCulture);
        IdentityText = IsMulti
            ? $"CAD pads · {Subtitle}"
            : $"CAD {pads[0].Id} · Diff idx {DxfIndexText}";

        var cadOutputFwDiffOverrideValue = (int?)null;
        if (IsMulti)
        {
            ApplyMultiSelectionDisplay(pads);
        }
        else if (snapshotCad is not null)
        {
            cadOutputFwDiffOverrideValue = ApplySnapshotDisplay(pads[0], snapshotCad);
        }
        else
        {
            cadOutputFwDiffOverrideValue = ApplyCallbackDisplay(
                pads[0],
                getMatchedRegularLinks,
                getMatchedRegularPadIds,
                getRegularPadIcDiff,
                getCadOutputFwDiffOverride,
                isDxfIndexAnchor,
                getCadV22CompensationPreview,
                getCadV22CompensationDiagnostics,
                getCadV22TargetAllocationSummary,
                notchComputationMode);
        }

        var dxfOverrideBase = cadOutputFwDiffOverrideValue
            ?? (dxfIndices.Count > 0 ? dxfIndices[0] : 0);
        CadOutputFwDiffOverride = Math.Clamp(dxfOverrideBase, 0, 1_000_000);
        HasCadOutputFwDiffOverride = cadOutputFwDiffOverrideValue.HasValue;
        HasCadOutputFwDiffOverrideEditor = !IsMulti && _singleCadPadId.HasValue && _setCadOutputFwDiffOverride is not null;
        HasDxfIndexAnchorEditor = !IsMulti && _singleCadPadId.HasValue && _setDxfIndexAnchor is not null;

        // Handle custom values: check for mixed state and set initial value.
        var values = pads.Select(p => (decimal)getCustomValue(p.Id)).Distinct().ToList();
        _isLoading = true; // Suppress dirty tracking during initial load.
        CustomValue = values.Count == 1 ? values[0] : 0m; // If not mixed, use the common value; otherwise, default to 0.
        _isLoading = false;
        _customValueInitial = CustomValue;
        _customValueDirty = false;

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

    private void ApplyMultiSelectionDisplay(IReadOnlyList<CadPad> pads)
    {
        var areas = pads.Select(p => p.Area).ToList();
        AreaText = $"{areas.Min():0.####} - {areas.Max():0.####} {PadInfoUnits.SquareMillimeter}";
        CentroidText = "Mixed"; // Centroid is mixed for multiple pads.
        VertexText = PadInfoTextFormatter.RangeText(pads.Select(p => p.Polygon.Vertices.Length));
        MatchText = "Mixed";
        MatchDetailsText = "Mixed";
        GeometrySeedValueText = "Mixed";
        HasFocusMatchAction = false;
        HasHighlightMatchAction = false;
    }

    private void ApplyPadGeometryTexts(CadPad pad)
    {
        AreaText = $"{pad.Area:0.####} {PadInfoUnits.SquareMillimeter}";
        CentroidText = FormatPoint(pad.Centroid);
        VertexText = pad.Polygon.Vertices.Length.ToString(CultureInfo.InvariantCulture);
    }

    private int? ApplySnapshotDisplay(CadPad pad, CadPadInspectorSnapshot snapshotCad)
    {
        ApplyPadGeometryTexts(pad);
        var cadPadId = pad.Id;
        MatchText = snapshotCad.MatchText;
        MatchDetailsText = snapshotCad.MatchDetailsText;
        GeometrySeedValueText = snapshotCad.MatchText;
        MatchConfidenceText = snapshotCad.MatchedRegularPadIds.Count == 0
            ? "-"
            : snapshotCad.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture);
        DxfIndexText = snapshotCad.DxfIndexDisplayText;
        IsDxfIndexAnchor = snapshotCad.IsDxfIndexAnchor;
        var icText = snapshotCad.IcIndex.HasValue
            ? (snapshotCad.IcIndex.Value + 1).ToString(CultureInfo.InvariantCulture)
            : "-";
        IdentityText = $"CAD {cadPadId} · IC {icText} · Diff idx {DxfIndexText}";
        HasFocusMatchAction = snapshotCad.MatchedRegularPadIds.Count > 0 && _focusCadMatches is not null;
        HasHighlightMatchAction = snapshotCad.MatchedRegularPadIds.Count > 0 && _highlightCadMatches is not null;

        if (snapshotCad.Notch is { } resolvedDisplayInput)
        {
            NotchRowSummaryText = snapshotCad.NotchRowSummary;
            ApplyNotchDisplay(NotchDisplayProjector.Build(resolvedDisplayInput), cadPadId);
        }

        RuleTraceLines = snapshotCad.RuleTrace.Select(PadInspectorRuleTraceFormatter.Format).ToList();
        return snapshotCad.CadOutputFwDiffOverride;
    }

    private int? ApplyCallbackDisplay(
        CadPad pad,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedRegularLinks,
        Func<int, IReadOnlyList<int>>? getMatchedRegularPadIds,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff,
        Func<int, int?>? getCadOutputFwDiffOverride,
        Func<int, bool>? isDxfIndexAnchor,
        Func<int, (double ToRegularRatio, double ToFullRatio, double CombinedRatio, bool IsToFullEnabled)?>? getCadV22CompensationPreview,
        Func<int, string?>? getCadV22CompensationDiagnostics,
        Func<int, NotchV22TargetAllocationSummary?>? getCadV22TargetAllocationSummary,
        NotchComputationMode notchComputationMode)
    {
        ApplyPadGeometryTexts(pad);
        var cadPadId = pad.Id;
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
        MatchText = BuildCadMatchText(matchedRegularIds, matchedLinks, getRegularPadIcDiff);
        MatchDetailsText = BuildCadMatchDetailsText(matchedRegularIds, matchedLinks, getRegularPadIcDiff);
        GeometrySeedValueText = MatchText;
        var cadOutputFwDiffOverrideValue = getCadOutputFwDiffOverride?.Invoke(cadPadId);
        IsDxfIndexAnchor = isDxfIndexAnchor?.Invoke(cadPadId) ?? false;
        HasFocusMatchAction = matchedRegularIds.Count > 0 && _focusCadMatches is not null;
        HasHighlightMatchAction = matchedRegularIds.Count > 0 && _highlightCadMatches is not null;

        var compensation = getCadV22CompensationPreview?.Invoke(cadPadId);
        if (compensation.HasValue)
        {
            NotchRowSummaryText = "Notch rows: unknown (open from selection inspector)";
            var allocation = getCadV22TargetAllocationSummary?.Invoke(cadPadId);
            var diagnostics = getCadV22CompensationDiagnostics?.Invoke(cadPadId);
            ApplyNotchDisplay(
                NotchDisplayProjector.Build(
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
                    notchComputationMode),
                cadPadId);
        }

        return cadOutputFwDiffOverrideValue;
    }

    private void ApplyNotchDisplay(NotchDisplayProjection notchDisplay, int cadPadId)
    {
        ToRegularRatioText = notchDisplay.ToRegularRatioText;
        ToFullRatioText = notchDisplay.ToFullRatioText;
        ToRegularValueText = notchDisplay.ToRegularValueText;
        ToFullValueText = notchDisplay.ToFullValueText;
        CombinedValueText = notchDisplay.CombinedValueText;
        Stage3AreaText = notchDisplay.Stage3AreaText;
        TargetAllocationSummaryText = notchDisplay.TargetAllocationSummaryText;
        TargetAllocationLines = notchDisplay.TargetAllocationLines.ToList();
        TargetAllocationItems = notchDisplay.TargetAllocationItems.Select(item =>
            PadInfoTargetAllocationViewModel.FromDisplayItem(item, _highlightRegularPads)).ToList();
        ToFullDiagnosticsText = notchDisplay.ToFullDiagnosticsText;
        ToFullReasonFullText = notchDisplay.ToFullReasonFullText;
        ToFullReasonShortText = notchDisplay.ToFullReasonShortText;
        var notchOwnerLinks = new List<NotchOwnerLinkViewModel>();
        var allNotchOwnerCadIds = new List<int>();
        BuildNotchOwnerLinks(
            notchDisplay.DiagnosticEntries,
            cadPadId,
            _highlightCadOwnerPads,
            notchOwnerLinks,
            allNotchOwnerCadIds);
        NotchOwnerLinks = notchOwnerLinks;
        _allNotchOwnerCadIds = allNotchOwnerCadIds;
        OwnerSummaryText = notchDisplay.OwnerSummaryText;
    }
}
