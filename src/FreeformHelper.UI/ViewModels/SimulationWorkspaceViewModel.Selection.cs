using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    public string? BuildHoverTipText(int regularPadId)
    {
        if (!_cellsByRegularPadId.TryGetValue(regularPadId, out var cell) || !_regularPadById.TryGetValue(regularPadId, out var pad))
        {
            return null;
        }

        return $"REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · FW Diff Idx {GetFwDiffIndex(pad.RegularPadId)} · CAD Output FW Diff Idx {GetCadOutputFwDiffIndex(pad.RegularPadId)}\n" +
               $"Before {FormatWholeNumberDisplay(cell.BeforeValue)}\n" +
               $"After {FormatWholeNumberDisplay(cell.AfterValue)}\n" +
               $"Delta {cell.DeltaValue.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)}\n" +
               FormatCellEmsStatus(cell);
    }

    public double GetEditableRegularValue(int regularPadId)
    {
        return _manualOverridesByRegularPadId.TryGetValue(regularPadId, out var overrideValue)
            ? overrideValue
            : GlobalValue;
    }

    public void ApplyRegularOverride(int regularPadId, double value)
    {
        if (!_regularPadById.ContainsKey(regularPadId))
        {
            return;
        }

        SelectRegularPad(regularPadId);
        SelectedManualValue = value;
        ApplySelectedOverride();
    }

    internal bool TryGetRegularQuerySnapshot(int regularPadId, out SimulationRegularQuerySnapshot snapshot)
    {
        if (!_regularPadById.TryGetValue(regularPadId, out var pad) || _snapshot is null)
        {
            snapshot = default;
            return false;
        }

        if (!_snapshotCellsByRegularPadId.TryGetValue(regularPadId, out var cell))
        {
            snapshot = default;
            return false;
        }

        var impacts = GetImpactItemsForPad(pad);
        snapshot = new SimulationRegularQuerySnapshot(
            pad.RegularPadId,
            pad.Row,
            pad.Col,
            pad.IcIndex,
            GetFwDiffIndex(pad.RegularPadId),
            GetCadOutputFwDiffIndex(pad.RegularPadId),
            cell.BeforeValue,
            cell.AfterValue,
            cell.DeltaValue,
            SimulationSafetyAuditService.IsEmsAfterCapViolation(cell.AfterValue, SimulationEmsAfterCap),
            _session.ActiveRegularPadIds.Contains(regularPadId),
            _cellsByRegularPadId.ContainsKey(regularPadId),
            SelectedSourceText,
            impacts);
        return true;
    }

    public void SelectRegularPad(int regularPadId)
    {
        if (regularPadId < 0)
        {
            SelectedRegularPadId = -1;
            return;
        }

        if (!_regularPadById.ContainsKey(regularPadId))
        {
            return;
        }

        SelectedRegularPadId = regularPadId;
    }

    public void RefreshExternalBindings()
    {
        RefreshCommandState();
    }

    private void ApplySelectedOverride()
    {
        if (!HasSelectedRegular)
        {
            return;
        }

        _manualOverridesByRegularPadId[SelectedRegularPadId] = SelectedManualValue;
        if (IsManualSource || !HasCsvData)
        {
            RefreshSnapshot();
            return;
        }

        RefreshSelectionPresentation();
        RefreshCommandState();
    }

    private void ClearSelectedOverride()
    {
        if (!HasSelectedRegular)
        {
            return;
        }

        _manualOverridesByRegularPadId.Remove(SelectedRegularPadId);
        SelectedManualValue = GlobalValue;
        if (IsManualSource || !HasCsvData)
        {
            RefreshSnapshot();
            return;
        }

        RefreshSelectionPresentation();
        RefreshCommandState();
    }

    private void ClearAllManualOverrides()
    {
        if (_manualOverridesByRegularPadId.Count == 0)
        {
            return;
        }

        _manualOverridesByRegularPadId.Clear();
        if (HasSelectedRegular)
        {
            SelectedManualValue = GlobalValue;
        }

        RefreshSnapshot();
    }

    private void RandomizeVisibleValues()
    {
        var visiblePads = _session.FwDiffGrid.Pads
            .Where(IsPadInSelectedAreaAndActiveSurface)
            .ToList();
        if (visiblePads.Count == 0)
        {
            return;
        }

        StopPlayback();
        foreach (var pad in visiblePads)
        {
            var nextValue = SelectedCanvasViewMode is NotchApplySimulationCanvasViewMode.Delta or NotchApplySimulationCanvasViewMode.ChangedOnly
                ? Random.Shared.Next(-120, 121)
                : Random.Shared.Next(-40, 401);
            _manualOverridesByRegularPadId[pad.RegularPadId] = nextValue;
        }

        if (HasSelectedRegular && _manualOverridesByRegularPadId.TryGetValue(SelectedRegularPadId, out var overrideValue))
        {
            SelectedManualValue = overrideValue;
        }

        RefreshSnapshot();
        StatusText = $"Randomized {visiblePads.Count} regular pad(s) in {SelectedAreaOption.Display}.";
    }

    private void ApplyManualPreset(string? valueText)
    {
        if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        StopPlayback();
        var hadOverrides = _manualOverridesByRegularPadId.Count > 0;
        var globalChanged = Math.Abs(GlobalValue - value) > SignificantDeltaEpsilon;
        _manualOverridesByRegularPadId.Clear();

        if (HasSelectedRegular)
        {
            SelectedManualValue = value;
        }

        GlobalValue = value;
        if (IsManualSource && !globalChanged && hadOverrides)
        {
            RefreshSnapshot();
        }
        else
        {
            RefreshCommandState();
        }

        StatusText = $"Manual preset applied: uniform {FormatWholeNumberDisplay(value)}.";
    }

    private void RefreshSelectionPresentation()
    {
        _selectedImpactItems.Clear();
        if (!TryGetSelectedRegularPad(out _))
        {
            OnSelectionPresentationChanged();
            return;
        }

        if (IsManualSource)
        {
            SelectedManualValue = _manualOverridesByRegularPadId.TryGetValue(SelectedRegularPadId, out var overrideValue)
                ? overrideValue
                : GlobalValue;
        }

        OnSelectionPresentationChanged();
        RebuildSelectedImpactItems();
        OnPropertyChanged(nameof(SelectedImpactSummaryText));
        OnPropertyChanged(nameof(HasSelectedImpactItems));
    }

    private void OnSelectionPresentationChanged()
    {
        OnPropertyChanged(nameof(SelectedRegularHeaderText));
        OnPropertyChanged(nameof(SelectedRegularLocationText));
        OnPropertyChanged(nameof(SelectedBeforeText));
        OnPropertyChanged(nameof(SelectedAfterText));
        OnPropertyChanged(nameof(SelectedDeltaText));
        OnPropertyChanged(nameof(SelectedPercentChangeText));
        OnPropertyChanged(nameof(SelectedEmsStatusText));
        OnPropertyChanged(nameof(SelectedNetFlowText));
        OnPropertyChanged(nameof(SelectedFlowLegSummaryText));
        OnPropertyChanged(nameof(SelectedFlowLegs));
        OnPropertyChanged(nameof(HasSelectedFlowLegs));
        OnPropertyChanged(nameof(SelectedSourceText));
        OnPropertyChanged(nameof(SelectedDiffBadgeText));
        OnPropertyChanged(nameof(ShowSelectedOverrideEditor));
        OnPropertyChanged(nameof(SelectedImpactSummaryText));
        OnPropertyChanged(nameof(HasSelectedImpactItems));
    }

    private void RefreshCommandState()
    {
        ImportCsvCommand.NotifyCanExecuteChanged();
        ClearCsvCommand.NotifyCanExecuteChanged();
        RandomizeVisibleValuesCommand.NotifyCanExecuteChanged();
        ApplyManualPresetCommand.NotifyCanExecuteChanged();
        TogglePlaybackCommand.NotifyCanExecuteChanged();
        PreviousFrameCommand.NotifyCanExecuteChanged();
        NextFrameCommand.NotifyCanExecuteChanged();
        ApplySelectedOverrideCommand.NotifyCanExecuteChanged();
        ClearSelectedOverrideCommand.NotifyCanExecuteChanged();
        ClearAllManualOverridesCommand.NotifyCanExecuteChanged();
        ReplayCopperPathCommand.NotifyCanExecuteChanged();
        UseCurrentCopperAsPathStartCommand.NotifyCanExecuteChanged();
        UseCurrentCopperAsPathEndCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanImportCsv));
        OnPropertyChanged(nameof(HasCsvFiles));
        OnPropertyChanged(nameof(HasCsvData));
        OnPropertyChanged(nameof(IsCsvSource));
        OnPropertyChanged(nameof(IsCopperSource));
        OnPropertyChanged(nameof(IsManualSource));
        OnPropertyChanged(nameof(ShowSimulationDiagnostics));
        OnPropertyChanged(nameof(HasUserDerivedState));
        OnPropertyChanged(nameof(CanAutoRefreshFromSourceChange));
        OnPropertyChanged(nameof(CanPlayFrames));
        OnPropertyChanged(nameof(CanStepFrames));
        OnPropertyChanged(nameof(CanRandomizeVisibleValues));
        OnPropertyChanged(nameof(IsThresholdMode));
        OnPropertyChanged(nameof(IsSignedCanvasView));
        OnPropertyChanged(nameof(HasSelectedRegular));
        OnPropertyChanged(nameof(HasManualOverrides));
        OnPropertyChanged(nameof(HasSelectedOverride));
        OnPropertyChanged(nameof(InputSummaryText));
        OnPropertyChanged(nameof(InputPrimarySummaryText));
        OnPropertyChanged(nameof(InputSecondarySummaryText));
        OnPropertyChanged(nameof(InputContractTitleText));
        OnPropertyChanged(nameof(InputContractDetailText));
        OnPropertyChanged(nameof(CsvProjectionContractText));
        OnPropertyChanged(nameof(HasCsvInspectorData));
        OnPropertyChanged(nameof(CsvInspectorSummaryText));
        OnPropertyChanged(nameof(CsvInspectorShapeSummaryText));
        OnPropertyChanged(nameof(CsvInspectorRowOriginText));
        OnPropertyChanged(nameof(CsvInspectorBeforePreviewText));
        OnPropertyChanged(nameof(SourceModeSummaryText));
        OnPropertyChanged(nameof(FrameSummaryText));
        OnPropertyChanged(nameof(VersionSummaryText));
        OnPropertyChanged(nameof(ViewModeSummaryText));
        OnPropertyChanged(nameof(ColorModeSummaryText));
        OnPropertyChanged(nameof(CanvasStatusSummaryText));
        OnPropertyChanged(nameof(PlaybackButtonText));
        OnPropertyChanged(nameof(PlaybackButtonGlyph));
        OnPropertyChanged(nameof(PlaybackLoopText));
        OnPropertyChanged(nameof(SelectedFrameSliderMaximum));
        OnPropertyChanged(nameof(SelectedFrameDisplayText));
        OnPropertyChanged(nameof(ShowSelectedOverrideEditor));
        OnPropertyChanged(nameof(ColorLegendTitleText));
        OnPropertyChanged(nameof(ColorLegendMeaningText));
        OnPropertyChanged(nameof(ColorLegendStartText));
        OnPropertyChanged(nameof(ColorLegendCenterText));
        OnPropertyChanged(nameof(ColorLegendEndText));
        OnPropertyChanged(nameof(ShowLegendCenterLabel));
        OnPropertyChanged(nameof(ShowSequentialLegend));
        OnPropertyChanged(nameof(HasDuplicateDiffResolutions));
        OnPropertyChanged(nameof(DuplicateDiffResolutionCount));
        OnPropertyChanged(nameof(DuplicateDiffResolutionStrategyText));
        OnPropertyChanged(nameof(DuplicateDiffResolutionSummaryText));
        OnPropertyChanged(nameof(DuplicateDiffResolutionSampleText));
        OnPropertyChanged(nameof(SelectedImpactSummaryText));
        OnPropertyChanged(nameof(HasSelectedImpactItems));
        OnPropertyChanged(nameof(CopperPositionText));
        OnPropertyChanged(nameof(CopperSummaryText));
        OnPropertyChanged(nameof(CopperProjectionModelText));
        OnPropertyChanged(nameof(CopperContactModelSummaryText));
        OnPropertyChanged(nameof(CopperInstructionText));
        OnPropertyChanged(nameof(CopperPathReplayArtifactSummaryText));
    }

    private bool TryGetSelectedRegularPad(out RegularPad pad)
    {
        if (_regularPadById.TryGetValue(SelectedRegularPadId, out pad!))
        {
            return true;
        }

        pad = null!;
        return false;
    }

    private bool TryGetSelectedCell(out NotchApplySimulationDiffCell cell)
    {
        if (_cellsByRegularPadId.TryGetValue(SelectedRegularPadId, out cell!))
        {
            return true;
        }

        cell = null!;
        return false;
    }

    partial void OnSelectedRegularPadIdChanged(int value)
    {
        RefreshSelectionPresentation();
        RefreshCommandState();
    }

    private void RebuildSelectedImpactItems()
    {
        _selectedImpactItems.Clear();
        if (_snapshot is null || !TryGetSelectedRegularPad(out var pad))
        {
            return;
        }

        foreach (var impact in GetImpactItemsForPad(pad))
        {
            _selectedImpactItems.Add(impact);
        }
    }

    private IReadOnlyList<NotchApplySimulationImpactItemViewModel> GetImpactItemsForPad(RegularPad pad)
    {
        return _impactItemsByRegularPadId.TryGetValue(pad.RegularPadId, out var impacts)
            ? impacts
            : Array.Empty<NotchApplySimulationImpactItemViewModel>();
    }

    private void RebuildImpactItemIndex(IReadOnlyList<NotchApplySimulationAction> actions)
    {
        var indexed = new Dictionary<int, List<(double SortKey, NotchApplySimulationImpactItemViewModel Item)>>();
        foreach (var action in actions)
        {
            var sourceRowsText = BuildSourceRowsText(action.SourceRowNumbers);
            var sourceCadText = action.CadPadId.HasValue
                ? action.CadPadId.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
            var sourceDelta = action.SourceAfterValue - action.SourceBeforeValue;
            if (Math.Abs(sourceDelta) > SignificantDeltaEpsilon)
            {
                var sourceImpact = new NotchApplySimulationImpactItemViewModel(
                    "Anchor retain",
                    $"FW Diff Idx {action.AnchorDiffIndex} keeps {action.SourceRetainedPercent}%",
                    $"Combine {action.CombinePercent}% · CAD {sourceCadText}",
                    sourceDelta.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture),
                    sourceRowsText);
                AppendImpactForDiff(
                    action.IcIndex,
                    action.AnchorDiffIndex,
                    Math.Abs(sourceDelta),
                    sourceImpact,
                    indexed);
            }

            foreach (var leg in action.Legs)
            {
                var targetImpact = new NotchApplySimulationImpactItemViewModel(
                    "Target receive",
                    $"From FW Diff Idx {action.AnchorDiffIndex} at {leg.RatioPercent}%",
                    $"CAD {sourceCadText} · anchor REG {action.RegularPadId}",
                    leg.DeltaValue.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture),
                    sourceRowsText);
                AppendImpactForDiff(
                    action.IcIndex,
                    leg.TargetDiffIndex,
                    Math.Abs(leg.DeltaValue),
                    targetImpact,
                    indexed);
            }
        }

        var finalized = new Dictionary<int, IReadOnlyList<NotchApplySimulationImpactItemViewModel>>(indexed.Count);
        foreach (var (regularPadId, impacts) in indexed)
        {
            finalized[regularPadId] = impacts
                .OrderByDescending(static item => item.SortKey)
                .ThenBy(static item => item.Item.RoleText, StringComparer.Ordinal)
                .Select(static item => item.Item)
                .ToArray();
        }

        _impactItemsByRegularPadId = finalized;
    }

    private void AppendImpactForDiff(
        int icIndex,
        int diffIndex,
        double sortKey,
        NotchApplySimulationImpactItemViewModel impact,
        Dictionary<int, List<(double SortKey, NotchApplySimulationImpactItemViewModel Item)>> indexed)
    {
        if (!_regularPadIdsByIcDiff.TryGetValue((icIndex, diffIndex), out var regularPadIds))
        {
            return;
        }

        foreach (var regularPadId in regularPadIds)
        {
            if (!indexed.TryGetValue(regularPadId, out var impacts))
            {
                impacts = new List<(double SortKey, NotchApplySimulationImpactItemViewModel Item)>();
                indexed[regularPadId] = impacts;
            }

            impacts.Add((sortKey, impact));
        }
    }

    private static string BuildSourceRowsText(IReadOnlyList<int> sourceRowNumbers)
    {
        if (sourceRowNumbers.Count == 0)
        {
            return "Rows: -";
        }

        return "Rows: #" + string.Join(", #", sourceRowNumbers);
    }

    internal static string FormatWholeNumberDisplay(double value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    }
}
