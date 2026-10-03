using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using NLog;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const double CsvLocalRepairAutoApplyMinConfidence = 0.80;

    private void FilterCadPadsByLayer()
    {
        if (_cad is null)
        {
            CadPads = new ObservableCollection<CadPad>();
            _cachedFilteredCadForBuild = null;
            _cadOutputFwDiffIndexByCadId.Clear();
            _cadDisplayIndexByCadId.Clear();
            SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
            RefreshDxfEditLayerOptions(Array.Empty<string>());
            SyncHiddenCadIdsToProject();
            ClearPadMatchResult(clearRegularAssignments: true);
            if (!_isLoadingSettings)
            {
                ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
            }
            return;
        }

        try
        {
            var result = LayerFilterUseCase.Filter(_cad, LayerToggles);
            var visible = result.VisiblePads
                .Where(p => !IsCadPadEffectivelyHidden(p.Id))
                .ToList();
            UpdateCadOutputFwDiffIndexing(visible);
            SyncHiddenCadIdsToProject();
            ClearPadMatchResult(clearRegularAssignments: true);
            if (!_isLoadingSettings)
            {
                ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
            }

            // If configured, trigger a grid rebuild when layer filters change (as bounds might change).
            // Suppress during bulk loads; callers will request an explicit rebuild.
            if (RecalcBoundsOnLayerFilter && !_isLoadingSettings)
            {
                _ = TriggerGridRebuildAsync(requestFit: false);
            }
        }
        catch (Exception ex)
        {
            SetStatusError("Layer filter failed", ex);
            Logger.Error(ex, "Layer filter failed.");
        }
    }

    private void UpdateCadOutputFwDiffIndexing(IReadOnlyList<CadPad> visiblePads)
    {
        var ordered = DxfIndexAssigner.OrderPads(visiblePads, _grid, SelectedScanOrder);
        CadPads = new ObservableCollection<CadPad>(ordered);
        _cachedFilteredCadForBuild = new CadPadSet(ordered);
        RebuildVisibleDxfIndexMap(ordered);
        BumpNotchExportCadFingerprint();
    }

    private void RebuildVisibleDxfIndexMap(IReadOnlyList<CadPad> ordered)
    {
        _cadOutputFwDiffIndexByCadId.Clear();
        _cadDisplayIndexByCadId.Clear();
        _cadIcIndexByCadId.Clear();
        if (ordered.Count == 0)
        {
            CadOutputFwDiffIndexOverrideCadIds = new ObservableCollection<int>();
            CadOutputFwDiffIndexAnchorCadIdsForCanvas = new ObservableCollection<int>();
            CadOutputFwDiffAssignmentSummary = "CAD Output FW Diff assignment: no CAD output pads.";
            SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
            RefreshStep4DuplicateDiffGroups(Array.Empty<CadPad>());
            BumpNotchExportIndexFingerprint();
            InvalidateWorkflowDataSnapshot();
            return;
        }

        var displayOrdered = DxfIndexAssigner.OrderPads(ordered, _grid, ScanOrder.LeftToRight_TopToBottom);
        for (var i = 0; i < displayOrdered.Count; i++)
        {
            _cadDisplayIndexByCadId[displayOrdered[i].Id] = i + 1;
        }

        var padsByIc = BuildPadsByIc(ordered);
        MigrateLegacyAnchorToPerIc(padsByIc);
        var autoMode = CadOutputFwDiffAutoMode;
        var strictMatchDiffByCadId = BuildCadStrictMatchDiffById(ordered);

        var assignment = _cadOutputFwDiffIndexAssignmentService.Assign(
            ordered,
            _grid,
            _projectFile.Settings.Grid,
            autoMode,
            strictMatchDiffByCadId,
            _projectFile.CadOutputFwDiffIndexOverrides,
            _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc);

        foreach (var pair in assignment.CadIcIndexByCadId)
        {
            _cadIcIndexByCadId[pair.Key] = pair.Value;
        }

        foreach (var pair in assignment.AssignedIndexByCadId)
        {
            _cadOutputFwDiffIndexByCadId[pair.Key] = pair.Value;
        }
        RefreshLatestCadOutputFwDiffAssignmentDecisions(ordered, allowCsvRepairAutoApply: true);

        if (assignment.DuplicateOverrides > 0 ||
            assignment.InvalidOverrides > 0 ||
            assignment.AutoConflictCount > 0 ||
            assignment.AutoUnmatchedCount > 0 ||
            assignment.OverrideMismatchCount > 0)
        {
            Logger.Warn(CultureInfo.InvariantCulture, "Auto CAD Output FW Diff assignment issues. mode={0}, duplicateOverrides={1}, invalidOverrides={2}, autoConflicts={3}, autoUnmatched={4}, overrideMismatch={5}.",
                autoMode,
                assignment.DuplicateOverrides,
                assignment.InvalidOverrides,
                assignment.AutoConflictCount,
                assignment.AutoUnmatchedCount,
                assignment.OverrideMismatchCount);
            if (assignment.ConflictSamples.Count > 0)
            {
                Logger.Warn(CultureInfo.InvariantCulture, "Auto CAD Output FW Diff conflicts sample: {0}", string.Join("; ", assignment.ConflictSamples));
            }

            if (assignment.UnmatchedSamples.Count > 0)
            {
                Logger.Warn(CultureInfo.InvariantCulture, "Auto CAD Output FW Diff unmatched sample: {0}", string.Join("; ", assignment.UnmatchedSamples));
            }
        }

        RefreshCadOutputFwDiffVisualHints(
            ordered,
            padsByIc,
            autoMode,
            assignment);
        RefreshStep4DuplicateDiffGroups(ordered);
        BumpNotchExportIndexFingerprint();
        InvalidateWorkflowDataSnapshot();
    }

    private Dictionary<int, int?> BuildCadStrictMatchDiffById(IReadOnlyList<CadPad> ordered)
    {
        if (_grid is null || _latestPadMatchResult.CadToRegular.Count == 0)
        {
            return ordered.ToDictionary(static pad => pad.Id, static _ => (int?)null);
        }

        return CadBestMatchSeedService.BuildSeedByCadId(
            ordered,
            _latestPadMatchResult.CadToRegular,
            _grid.Pads.ToDictionary(pad => pad.RegularPadId),
            _cadIcIndexByCadId,
            GetActiveRegularVisibilityMaskPadIds());
    }

    private Dictionary<int, List<CadPad>> BuildPadsByIc(IReadOnlyList<CadPad> ordered)
    {
        var groups = new Dictionary<int, List<CadPad>>();
        foreach (var pad in ordered)
        {
            var ic = ResolveCadIcIndex(pad);
            _cadIcIndexByCadId[pad.Id] = ic;
            if (!groups.TryGetValue(ic, out var list))
            {
                list = new List<CadPad>();
                groups[ic] = list;
            }

            list.Add(pad);
        }

        return groups;
    }

    private void MigrateLegacyAnchorToPerIc(Dictionary<int, List<CadPad>> padsByIc)
    {
        var legacyAnchorCadId = _projectFile.CadOutputFwDiffIndexAnchorCadPadId;
        if (!legacyAnchorCadId.HasValue)
        {
            return;
        }

        var anchorsByIc = _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc;
        if (anchorsByIc.Count > 0)
        {
            _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
            return;
        }

        if (!_cadIcIndexByCadId.TryGetValue(legacyAnchorCadId.Value, out var icIndex))
        {
            return;
        }

        if (!padsByIc.TryGetValue(icIndex, out var pads) || pads.All(p => p.Id != legacyAnchorCadId.Value))
        {
            return;
        }

        anchorsByIc[icIndex] = legacyAnchorCadId.Value;
        _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
    }

    private void RefreshCadOutputFwDiffVisualHints(
        IReadOnlyList<CadPad> ordered,
        Dictionary<int, List<CadPad>> padsByIc,
        CadOutputFwDiffAutoMode autoMode,
        CadOutputFwDiffIndexAssignmentResult assignment)
    {
        var visibleCadIds = ordered.Select(p => p.Id).ToHashSet();
        var visibleOverrideCadIds = _projectFile.CadOutputFwDiffIndexOverrides.Keys
            .Where(visibleCadIds.Contains)
            .OrderBy(id => id)
            .ToList();

        CadOutputFwDiffIndexOverrideCadIds = new ObservableCollection<int>(visibleOverrideCadIds);

        var anchorCadIds = _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc
            .OrderBy(kv => kv.Key)
            .Where(kv => visibleCadIds.Contains(kv.Value))
            .Select(kv => kv.Value)
            .Distinct()
            .ToList();
        CadOutputFwDiffIndexAnchorCadIdsForCanvas = new ObservableCollection<int>(anchorCadIds);

        var anchorPairs = _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc
            .OrderBy(kv => kv.Key)
            .Take(4)
            .Select(kv => $"IC{kv.Key + 1}->CAD {kv.Value}")
            .ToList();
        var anchorText = anchorPairs.Count == 0
            ? "anchor=none"
            : $"anchor=stored (inactive in current modes: {string.Join(", ", anchorPairs)}{(_projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Count > anchorPairs.Count ? " (+)" : string.Empty)})";
        var scanOrderText = $"scan={SelectedScanOrder}";
        var icText = $"IC groups={padsByIc.Count}";
        var unresolvedCount = assignment.AutoConflictCount + assignment.AutoUnmatchedCount;
        var modeText = autoMode == CadOutputFwDiffAutoMode.BestMatchDirect
            ? "geometry seed"
            : "geometry seed (unique per IC)";
        var priorityText = autoMode == CadOutputFwDiffAutoMode.BestMatchDirect
            ? "override > direct geometry-seed"
            : "override > direct geometry-seed (per-IC unique)";
        var qualityText = assignment.DuplicateOverrides == 0 &&
                          assignment.InvalidOverrides == 0 &&
                          assignment.AutoConflictCount == 0 &&
                          assignment.AutoUnmatchedCount == 0 &&
                          assignment.OverrideMismatchCount == 0
            ? string.Empty
            : $" | issues: overrideDuplicate={assignment.DuplicateOverrides}, overrideInvalid={assignment.InvalidOverrides}, autoConflict={assignment.AutoConflictCount}, autoUnmatched={assignment.AutoUnmatchedCount}, overrideMismatch={assignment.OverrideMismatchCount}";

        CadOutputFwDiffAssignmentSummary =
            $"CAD Output FW Diff assignment: override={assignment.OverrideAssignedCount}, auto={assignment.AutoAssignedCount}, unresolved={unresolvedCount}, {icText}, {anchorText}, {scanOrderText}. Mode: {modeText}. Priority: {priorityText}{qualityText}.";
    }

    private void RefreshLatestCadOutputFwDiffAssignmentDecisions(
        IReadOnlyList<CadPad> ordered,
        bool allowCsvRepairAutoApply = false)
    {
        if (_grid is null || _latestPadMatchResult.CadToRegular.Count == 0)
        {
            SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
            return;
        }

        var regularPadById = _grid.Pads.ToDictionary(static pad => pad.RegularPadId);
        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered,
            _latestPadMatchResult.CadToRegular,
            regularPadById,
            _cadIcIndexByCadId,
            GetActiveRegularVisibilityMaskPadIds(),
            _cadOutputFwDiffIndexByCadId,
            _projectFile.CadOutputFwDiffIndexOverrides);
        if (allowCsvRepairAutoApply)
        {
            decisions = TryAutoApplyCsvRepairSuggestions(ordered, regularPadById, decisions);
        }

        SetLatestCadOutputFwDiffAssignmentDecisions(decisions);
    }

    private IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> TryAutoApplyCsvRepairSuggestions(
        IReadOnlyList<CadPad> ordered,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> decisions)
    {
        var activeMaskPadIds = GetActiveRegularVisibilityMaskPadIds();
        if (activeMaskPadIds is null || activeMaskPadIds.Count == 0)
        {
            return decisions;
        }

        if (decisions.Count == 0)
        {
            return decisions;
        }

        var appliedCadIds = new List<int>();
        foreach (var decision in decisions.Values.OrderBy(static row => row.OrderedCadIndex))
        {
            if (!ShouldAutoApplyCsvRepair(decision))
            {
                continue;
            }

            var targetDiff = decision.RepairSuggestionDiffIndex!.Value;
            if (WouldCreateSameIcDuplicate(decision.CadPadId, targetDiff))
            {
                continue;
            }

            _cadOutputFwDiffIndexByCadId[decision.CadPadId] = targetDiff;
            appliedCadIds.Add(decision.CadPadId);
        }

        if (appliedCadIds.Count == 0)
        {
            return decisions;
        }

        var refreshed = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered,
            _latestPadMatchResult.CadToRegular,
            regularPadById,
            _cadIcIndexByCadId,
            activeMaskPadIds,
            _cadOutputFwDiffIndexByCadId,
            _projectFile.CadOutputFwDiffIndexOverrides);
        var refreshedByCadId = refreshed.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        foreach (var cadId in appliedCadIds)
        {
            if (!refreshedByCadId.TryGetValue(cadId, out var decision))
            {
                continue;
            }

            refreshedByCadId[cadId] = decision with
            {
                DecisionSource = CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
            };
        }

        Logger.Info(
            CultureInfo.InvariantCulture,
            "Auto-applied csv local repair suggestions: count={0}, samples={1}.",
            appliedCadIds.Count,
            string.Join(", ", appliedCadIds.Take(5)));
        return refreshedByCadId;
    }

    private bool ShouldAutoApplyCsvRepair(CadOutputFwDiffAssignmentDecision decision)
    {
        if (decision.Mode != CadOutputFwDiffAssignmentMode.CsvConstrained ||
            decision.RepairSuggestionDiffIndex is null ||
            !decision.Confidence.HasValue ||
            decision.Confidence.Value < CsvLocalRepairAutoApplyMinConfidence)
        {
            return false;
        }

        if (decision.CurrentPrimaryDiffIndex.HasValue &&
            decision.CurrentPrimaryDiffIndex.Value == decision.RepairSuggestionDiffIndex.Value)
        {
            return false;
        }

        return !_projectFile.CadOutputFwDiffIndexOverrides.ContainsKey(decision.CadPadId);
    }

    private bool WouldCreateSameIcDuplicate(int cadPadId, int targetDiff)
    {
        if (!_cadIcIndexByCadId.TryGetValue(cadPadId, out var cadIcIndex))
        {
            return false;
        }

        foreach (var (otherCadId, otherDiff) in _cadOutputFwDiffIndexByCadId)
        {
            if (otherCadId == cadPadId || otherDiff != targetDiff)
            {
                continue;
            }

            if (_cadIcIndexByCadId.TryGetValue(otherCadId, out var otherIcIndex) &&
                otherIcIndex == cadIcIndex)
            {
                return true;
            }
        }

        return false;
    }

    private int ResolveCadIcIndex(CadPad pad)
    {
        if (_grid is null || _grid.Cols <= 0 || _grid.XEdges.Count < 2)
        {
            return 0;
        }

        var (_, col) = DxfIndexAssigner.ResolveGridCell(_grid, pad.Centroid);
        var perIcCols = GridIcChannelAllocationService.ResolvePerIcColumns(_projectFile.Settings.Grid, _grid.Cols);
        return GridIcChannelAllocationService.ResolveIcIndexForColumn(col, perIcCols);
    }

}

