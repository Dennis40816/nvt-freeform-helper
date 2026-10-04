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

        var bounds = UnionBounds(pads.Select(p => p.Bounds));
        BoundsText = FormatBounds(bounds);
        var cadOutputFwDiffOverrideValue = (int?)null;
        var hasNotchCompensationPreview = false;
        var notchRowSummaryText = "Notch rows: -";
        var toRegularRatioText = "Undo NF (To Regular): -";
        var toFullRatioText = "To Full: -";
        var combinedRatioText = "Combined ratio: -";
        var stage3AreaText = "Stage3 area: -";
        var targetAllocationSummaryText = "Targets: -";
        var targetAllocationLines = new List<string>();
        var targetAllocationItems = new List<PadInfoTargetAllocationViewModel>();
        var ownerSummaryText = string.Empty;
        var toFullDiagnosticsText = string.Empty;
        var notchOwnerLinks = new List<NotchOwnerLinkViewModel>();
        var allNotchOwnerCadIds = new List<int>();
        var ruleTraceLines = new List<string>();
        var matchConfidenceText = "-";
        var geometrySeedValueText = "Unmatched";
        var toRegularValueText = "-";
        var toFullValueText = "-";
        var combinedValueText = "-";
        var toFullReasonShortText = string.Empty;
        var toFullReasonFullText = string.Empty;

        if (IsMulti)
        {
            var areas = pads.Select(p => p.Area).ToList();
            AreaText = $"{areas.Min():0.####} - {areas.Max():0.####} {PadInfoUnits.SquareMillimeter}";
            CentroidText = "Mixed"; // Centroid is mixed for multiple pads.
            VertexText = PadInfoTextFormatter.RangeText(pads.Select(p => p.Polygon.Vertices.Length));
            MatchText = "Mixed";
            MatchDetailsText = "Mixed";
            geometrySeedValueText = "Mixed";
            _singleCadPadId = null;
            HasFocusMatchAction = false;
            HasHighlightMatchAction = false;
        }
        else
        {
            AreaText = $"{pads[0].Area:0.####} {PadInfoUnits.SquareMillimeter}";
            CentroidText = FormatPoint(pads[0].Centroid);
            VertexText = pads[0].Polygon.Vertices.Length.ToString(CultureInfo.InvariantCulture);
            var cadPadId = pads[0].Id;
            _singleCadPadId = cadPadId;

            if (snapshotCad is not null)
            {
                MatchText = snapshotCad.MatchText;
                MatchDetailsText = snapshotCad.MatchDetailsText;
                geometrySeedValueText = snapshotCad.MatchText;
                matchConfidenceText = snapshotCad.MatchedRegularPadIds.Count == 0
                    ? "-"
                    : snapshotCad.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture);
                DxfIndexText = snapshotCad.DxfIndexDisplayText;
                cadOutputFwDiffOverrideValue = snapshotCad.CadOutputFwDiffOverride;
                IsDxfIndexAnchor = snapshotCad.IsDxfIndexAnchor;
                var icText = snapshotCad.IcIndex.HasValue
                    ? (snapshotCad.IcIndex.Value + 1).ToString(CultureInfo.InvariantCulture)
                    : "-";
                IdentityText = $"CAD {cadPadId} · IC {icText} · Diff idx {DxfIndexText}";
                HasFocusMatchAction = snapshotCad.MatchedRegularPadIds.Count > 0 && _focusCadMatches is not null;
                HasHighlightMatchAction = snapshotCad.MatchedRegularPadIds.Count > 0 && _highlightCadMatches is not null;

                if (snapshotCad.Notch is not null)
                {
                    hasNotchCompensationPreview = true;
                    notchRowSummaryText = snapshotCad.NotchRowSummary;
                    var notchDisplay = NotchDisplayProjector.Build(snapshotCad.Notch);
                    toRegularRatioText = notchDisplay.ToRegularRatioText;
                    toFullRatioText = notchDisplay.ToFullRatioText;
                    combinedRatioText = notchDisplay.CombinedRatioText;
                    toRegularValueText = notchDisplay.ToRegularValueText;
                    toFullValueText = notchDisplay.ToFullValueText;
                    combinedValueText = notchDisplay.CombinedValueText;
                    stage3AreaText = notchDisplay.Stage3AreaText;
                    targetAllocationSummaryText = notchDisplay.TargetAllocationSummaryText;
                    targetAllocationLines.AddRange(notchDisplay.TargetAllocationLines);
                    targetAllocationItems.AddRange(notchDisplay.TargetAllocationItems.Select(item =>
                        PadInfoTargetAllocationViewModel.FromDisplayItem(item, _highlightRegularPads)));
                    toFullDiagnosticsText = notchDisplay.ToFullDiagnosticsText;
                    toFullReasonFullText = notchDisplay.ToFullReasonFullText;
                    toFullReasonShortText = notchDisplay.ToFullReasonShortText;
                    BuildNotchOwnerLinks(
                        notchDisplay.DiagnosticEntries,
                        cadPadId,
                        _highlightCadOwnerPads,
                        notchOwnerLinks,
                        allNotchOwnerCadIds);
                    ownerSummaryText = notchDisplay.OwnerSummaryText;
                }

                ruleTraceLines.AddRange(snapshotCad.RuleTrace.Select(PadInspectorRuleTraceFormatter.Format));
            }
            else
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
                MatchText = BuildCadMatchText(matchedRegularIds, matchedLinks, getRegularPadIcDiff);
                MatchDetailsText = BuildCadMatchDetailsText(matchedRegularIds, matchedLinks, getRegularPadIcDiff);
                geometrySeedValueText = MatchText;
                cadOutputFwDiffOverrideValue = getCadOutputFwDiffOverride?.Invoke(cadPadId);
                IsDxfIndexAnchor = isDxfIndexAnchor?.Invoke(cadPadId) ?? false;
                HasFocusMatchAction = matchedRegularIds.Count > 0 && _focusCadMatches is not null;
                HasHighlightMatchAction = matchedRegularIds.Count > 0 && _highlightCadMatches is not null;

                var compensation = getCadV22CompensationPreview?.Invoke(cadPadId);
                if (compensation.HasValue)
                {
                    hasNotchCompensationPreview = true;
                    notchRowSummaryText = "Notch rows: unknown (open from selection inspector)";
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
                    toRegularRatioText = notchDisplay.ToRegularRatioText;
                    toFullRatioText = notchDisplay.ToFullRatioText;
                    combinedRatioText = notchDisplay.CombinedRatioText;
                    toRegularValueText = notchDisplay.ToRegularValueText;
                    toFullValueText = notchDisplay.ToFullValueText;
                    combinedValueText = notchDisplay.CombinedValueText;
                    stage3AreaText = notchDisplay.Stage3AreaText;
                    targetAllocationSummaryText = notchDisplay.TargetAllocationSummaryText;
                    targetAllocationLines.AddRange(notchDisplay.TargetAllocationLines);
                    targetAllocationItems.AddRange(notchDisplay.TargetAllocationItems.Select(item =>
                        PadInfoTargetAllocationViewModel.FromDisplayItem(item, _highlightRegularPads)));
                    toFullDiagnosticsText = notchDisplay.ToFullDiagnosticsText;
                    toFullReasonFullText = notchDisplay.ToFullReasonFullText;
                    toFullReasonShortText = notchDisplay.ToFullReasonShortText;
                    ownerSummaryText = notchDisplay.OwnerSummaryText;
                    BuildNotchOwnerLinks(
                        notchDisplay.DiagnosticEntries,
                        cadPadId,
                        _highlightCadOwnerPads,
                        notchOwnerLinks,
                        allNotchOwnerCadIds);
                }
            }
        }

        HasNotchCompensationPreview = hasNotchCompensationPreview;
        NotchRowSummaryText = notchRowSummaryText;
        ToRegularRatioText = toRegularRatioText;
        ToFullRatioText = toFullRatioText;
        CombinedRatioText = combinedRatioText;
        ToRegularValueText = toRegularValueText;
        ToFullValueText = toFullValueText;
        CombinedValueText = combinedValueText;
        ToFullReasonShortText = toFullReasonShortText;
        ToFullReasonFullText = toFullReasonFullText;
        Stage3AreaText = stage3AreaText;
        TargetAllocationSummaryText = targetAllocationSummaryText;
        TargetAllocationLines = targetAllocationLines;
        TargetAllocationItems = targetAllocationItems;
        OwnerSummaryText = ownerSummaryText;
        ToFullDiagnosticsText = toFullDiagnosticsText;
        NotchOwnerLinks = notchOwnerLinks;
        _allNotchOwnerCadIds = allNotchOwnerCadIds;
        RuleTraceLines = ruleTraceLines;
        MatchConfidenceText = matchConfidenceText;
        GeometrySeedValueText = geometrySeedValueText;

        var dxfOverrideBase = cadOutputFwDiffOverrideValue
            ?? (dxfIndices.Count > 0 ? dxfIndices[0] : 0);
        CadOutputFwDiffOverride = Math.Clamp(dxfOverrideBase, 0, 1_000_000);
        HasCadOutputFwDiffOverride = cadOutputFwDiffOverrideValue.HasValue;
        HasCadOutputFwDiffOverrideEditor = !IsMulti && _singleCadPadId.HasValue && _setCadOutputFwDiffOverride is not null;
        HasDxfIndexAnchorEditor = !IsMulti && _singleCadPadId.HasValue && _setDxfIndexAnchor is not null;

        // Handle custom values: check for mixed state and set initial value.
        var values = pads.Select(p => (decimal)getCustomValue(p.Id)).Distinct().ToList();
        IsCustomValueMixed = values.Count > 1;
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
        FocusMatchCommand = new RelayCommand(FocusMatch);
        LocateMatchCommand = new RelayCommand(LocateMatch);
        OpenOwnerDetailCommand = FocusMatchCommand;
        HighlightMatchesCommand = new RelayCommand(HighlightMatches);
        HighlightAllNotchOwnersCommand = new RelayCommand(HighlightAllNotchOwners);
        ToggleNotchDiagnosticsCommand = new RelayCommand(() => ShowNotchDiagnostics = !ShowNotchDiagnostics);
        ToggleRuleTraceCommand = new RelayCommand(() => ShowRuleTrace = !ShowRuleTrace);
        ToggleGeometryDetailsCommand = new RelayCommand(() => ShowGeometryDetails = !ShowGeometryDetails);
        ToggleLowFrequencyDetailSectionCommand = new RelayCommand(ToggleLowFrequencyDetailSection);
        ShowGeometryDetails = false;
    }
}

