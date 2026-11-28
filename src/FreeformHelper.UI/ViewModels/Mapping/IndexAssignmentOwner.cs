// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

// UI thread only. Maps and groups are accepted results; Step4 retains its own notifications.
internal sealed class IndexAssignmentOwner
{
    private readonly IndexAssignmentInputs _inputs;
    private readonly IndexAssignmentPorts _ports;
    private readonly IndexMappingEffects _effects;
    private readonly IndexMappingNotifications _notifications;
    private readonly Step4DuplicateGroupsModel _step4DuplicateGroups = new();
    private Step4IndexState? _step4Index;

    public IndexAssignmentOwner(IndexAssignmentInputs inputs, IndexAssignmentPorts ports,
        IndexMappingEffects effects, IndexMappingNotifications notifications)
    {
        _inputs = inputs;
        _ports = ports;
        _effects = effects;
        _notifications = notifications;
    }

    public CadIndexMaps Maps { get; } = new();
    public Step4IndexState Step4Index => _step4Index ??= new Step4IndexState(OnStep4ShiftDeltaChanged);
    public IReadOnlyList<Step4DuplicateDiffGroupViewModel> Step4DuplicateDiffGroups => _step4DuplicateGroups.Groups;
    public bool HasStep4DuplicateDiffGroups => _step4DuplicateGroups.HasGroups;

    private Dictionary<int, int> _cadOutputFwDiffIndexByCadId => Maps.OutputFwDiffIndexByCadId;
    private Dictionary<int, int> _cadDisplayIndexByCadId => Maps.DisplayIndexByCadId;
    private Dictionary<int, int> _cadIcIndexByCadId => Maps.IcIndexByCadId;

    public bool CanOffsetSelectedCadOutputFwDiffIndices =>
        DxfDiffShiftService.CanOffset(_inputs.SelectedCadIds(), _cadOutputFwDiffIndexByCadId, Step4Index.ShiftDelta);

    public string Step4DiffShiftSelectionSummary
    {
        get
        {
            var workflowSnapshot = _ports.ReadSnapshot();
            return DxfDiffShiftService.BuildSelectionSummary(
                _inputs.SelectedCadIds(), workflowSnapshot.CadOutputFwDiffIndexByCadId, Step4Index.ShiftDelta);
        }
    }

    public int? GetCadOutputFwDiffIndexOverride(int cadPadId)
    {
        return _inputs.Project().CadOutputFwDiffIndexOverrides.TryGetValue(cadPadId, out var index)
            ? index
            : null;
    }

    public void SetCadOutputFwDiffIndexOverride(int cadPadId, int targetIndex)
    {
        var overrides = _inputs.Project().CadOutputFwDiffIndexOverrides;
        var result = DxfOverrideService.SetOverride(cadPadId, targetIndex, overrides, _cadIcIndexByCadId);
        if (!result.Changed)
        {
            return;
        }

        _effects.PushUndo(() =>
        {
            if (result.HadPrevious)
            {
                overrides[cadPadId] = result.PreviousIndex;
            }
            else
            {
                overrides.Remove(cadPadId);
            }

            foreach (var pair in result.ReplacedOverrides)
            {
                overrides[pair.Key] = pair.Value;
            }

            UpdateCadOutputFwDiffIndexing(_inputs.CadPads().ToList());
        }, "CAD Output FW Diff override");

        UpdateCadOutputFwDiffIndexing(_inputs.CadPads().ToList());
        _effects.InvalidateDownstream();
        _effects.NotifySourceChanged();
        _effects.MarkUnsaved();
        _effects.Status(result.Message);
    }

    public void ClearCadOutputFwDiffIndexOverride(int cadPadId)
    {
        var overrides = _inputs.Project().CadOutputFwDiffIndexOverrides;
        var result = DxfOverrideService.ClearOverride(cadPadId, overrides);
        if (!result.HadPrevious)
        {
            _effects.Status($"CAD Output FW Diff override clear: CAD {cadPadId} has no override.");
            return;
        }

        _effects.PushUndo(() =>
        {
            overrides[cadPadId] = result.PreviousIndex;
            UpdateCadOutputFwDiffIndexing(_inputs.CadPads().ToList());
        }, "CAD Output FW Diff override clear");

        UpdateCadOutputFwDiffIndexing(_inputs.CadPads().ToList());
        _effects.InvalidateDownstream();
        _effects.NotifySourceChanged();
        _effects.MarkUnsaved();
        _effects.Status($"CAD Output FW Diff override cleared: CAD {cadPadId}.");
    }

    public bool IsCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
    {
        return DxfOverrideService.IsAnchor(cadPadId, _cadIcIndexByCadId, _inputs.Project().CadOutputFwDiffIndexAnchorCadPadByIc);
    }

    public void SetCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
    {
        if (!_cadIcIndexByCadId.TryGetValue(cadPadId, out var icIndex))
        {
            _effects.Status($"CAD Output FW Diff anchor ignored: CAD {cadPadId} is not visible.");
            return;
        }

        if (_inputs.Project().CadOutputFwDiffIndexAnchorCadPadByIc.TryGetValue(icIndex, out var currentAnchor) &&
            currentAnchor == cadPadId)
        {
            return;
        }

        _ports.SetAnchor(cadPadId);
    }

    public void ClearCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
    {
        if (!_cadIcIndexByCadId.TryGetValue(cadPadId, out var icIndex))
        {
            _effects.Status($"CAD Output FW Diff anchor clear ignored: CAD {cadPadId} is not visible.");
            return;
        }

        if (!_inputs.Project().CadOutputFwDiffIndexAnchorCadPadByIc.Remove(icIndex))
        {
            _effects.Status($"CAD Output FW Diff anchor clear: IC {icIndex + 1} has no anchor.");
            return;
        }

        // Keep the stored selection in step with the project anchors, so selecting the same CAD again commits it.
        if (_ports.ReadAnchor() == cadPadId)
        {
            using (_ports.SuppressAnchorChange())
            {
                using (_ports.SuppressUndo())
                {
                    _ports.SetAnchor(-1);
                }
            }
        }

        UpdateCadOutputFwDiffIndexing(_inputs.CadPads().ToList());
        _effects.InvalidateDownstream();
        _effects.NotifySourceChanged();
        _effects.MarkUnsaved();
        _effects.Status($"CAD Output FW Diff anchor cleared: IC {icIndex + 1} uses scan-order first CAD.");
    }

    public bool TryGetCadOutputFwDiffIndex(int cadPadId, out int dxfIndex)
    {
        return _cadOutputFwDiffIndexByCadId.TryGetValue(cadPadId, out dxfIndex);
    }

    public bool TryGetCadIcIndex(int cadPadId, out int icIndex)
    {
        return _cadIcIndexByCadId.TryGetValue(cadPadId, out icIndex);
    }

    public bool TryGetCadDisplayIndex(int cadPadId, out int displayIndex)
    {
        return _cadDisplayIndexByCadId.TryGetValue(cadPadId, out displayIndex);
    }

    public void SetLatestCadOutputFwDiffAssignmentDecisions(IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> decisions)
    {
        Maps.LatestAssignmentDecisionsByCadId = decisions;
        _ports.InvalidateSnapshot();
    }

    public void UpdateCadOutputFwDiffIndexing(IReadOnlyList<CadPad> visiblePads)
    {
        var ordered = DxfIndexAssigner.OrderPads(visiblePads, _inputs.Grid(), _inputs.ScanOrder());
        _ports.SetCadPads(new ObservableCollection<CadPad>(ordered));
        _ports.SetFilteredCad(new CadPadSet(ordered));
        RebuildVisibleDxfIndexMap(ordered);
        _ports.BumpCadFingerprint();
    }

    public void RebuildVisibleDxfIndexMap(IReadOnlyList<CadPad> ordered)
    {
        _cadOutputFwDiffIndexByCadId.Clear();
        _cadDisplayIndexByCadId.Clear();
        _cadIcIndexByCadId.Clear();
        if (ordered.Count == 0)
        {
            ResetVisibleDxfIndexMapToEmpty();
            return;
        }

        var displayOrdered = DxfIndexAssigner.OrderPads(ordered, _inputs.Grid(), ScanOrder.LeftToRight_TopToBottom);
        for (var i = 0; i < displayOrdered.Count; i++)
        {
            _cadDisplayIndexByCadId[displayOrdered[i].Id] = i + 1;
        }

        var padsByIc = BuildPadsByIc(ordered);
        MigrateLegacyAnchorToPerIc(padsByIc);
        var autoMode = _inputs.AutoMode();
        var strictMatchDiffByCadId = BuildCadStrictMatchDiffById(ordered);

        var assignment = CadOutputFwDiffIndexAssignmentService.Assign(
            ordered,
            _inputs.Grid(),
            _inputs.Project().Settings.Grid,
            autoMode,
            strictMatchDiffByCadId,
            _inputs.Project().CadOutputFwDiffIndexOverrides,
            _inputs.Project().CadOutputFwDiffIndexAnchorCadPadByIc);

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
            _effects.Warn("Auto CAD Output FW Diff assignment issues. mode={0}, duplicateOverrides={1}, invalidOverrides={2}, autoConflicts={3}, autoUnmatched={4}, overrideMismatch={5}.",
                autoMode,
                assignment.DuplicateOverrides,
                assignment.InvalidOverrides,
                assignment.AutoConflictCount,
                assignment.AutoUnmatchedCount,
                assignment.OverrideMismatchCount);
            if (assignment.ConflictSamples.Count > 0)
            {
                _effects.Warn("Auto CAD Output FW Diff conflicts sample: {0}", string.Join("; ", assignment.ConflictSamples));
            }

            if (assignment.UnmatchedSamples.Count > 0)
            {
                _effects.Warn("Auto CAD Output FW Diff unmatched sample: {0}", string.Join("; ", assignment.UnmatchedSamples));
            }
        }

        RefreshCadOutputFwDiffVisualHints(
            ordered,
            padsByIc,
            autoMode,
            assignment);
        RefreshStep4DuplicateDiffGroups(ordered);
        _ports.BumpIndexFingerprint();
        _ports.InvalidateSnapshot();
    }

    private void ResetVisibleDxfIndexMapToEmpty()
    {
        _ports.SetOverrideHints(new ObservableCollection<int>());
        _ports.SetAnchorHints(new ObservableCollection<int>());
        _ports.SetAssignmentSummary("CAD Output FW Diff assignment: no CAD output pads.");
        SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
        RefreshStep4DuplicateDiffGroups(Array.Empty<CadPad>());
        _ports.BumpIndexFingerprint();
        _ports.InvalidateSnapshot();
    }

    private Dictionary<int, int?> BuildCadStrictMatchDiffById(IReadOnlyList<CadPad> ordered)
    {
        var grid = _inputs.Grid();
        if (grid is null || _inputs.MatchLinks().Count == 0)
        {
            return ordered.ToDictionary(static pad => pad.Id, static _ => (int?)null);
        }

        return CadBestMatchSeedService.BuildSeedByCadId(
            ordered,
            _inputs.MatchLinks(),
            grid.Pads.ToDictionary(pad => pad.RegularPadId),
            _cadIcIndexByCadId,
            _inputs.ActiveMask());
    }

    private Dictionary<int, List<CadPad>> BuildPadsByIc(IReadOnlyList<CadPad> ordered)
    {
        var groups = new Dictionary<int, List<CadPad>>();
        foreach (var pad in ordered)
        {
            var ic = DxfIcResolver.Resolve(_inputs.Grid(), _inputs.Project().Settings.Grid, pad);
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
        var legacyAnchorCadId = _inputs.Project().CadOutputFwDiffIndexAnchorCadPadId;
        if (!legacyAnchorCadId.HasValue)
        {
            return;
        }

        var anchorsByIc = _inputs.Project().CadOutputFwDiffIndexAnchorCadPadByIc;
        if (anchorsByIc.Count > 0)
        {
            _inputs.Project().CadOutputFwDiffIndexAnchorCadPadId = null;
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
        _inputs.Project().CadOutputFwDiffIndexAnchorCadPadId = null;
    }

    private void RefreshCadOutputFwDiffVisualHints(
        IReadOnlyList<CadPad> ordered,
        Dictionary<int, List<CadPad>> padsByIc,
        CadOutputFwDiffAutoMode autoMode,
        CadOutputFwDiffIndexAssignmentResult assignment)
    {
        var hints = DxfIndexHintsBuilder.Build(
            ordered.Select(p => p.Id).ToHashSet(),
            _inputs.Project().CadOutputFwDiffIndexOverrides,
            _inputs.Project().CadOutputFwDiffIndexAnchorCadPadByIc,
            _inputs.ScanOrder(),
            padsByIc.Count,
            autoMode,
            assignment);
        _ports.SetOverrideHints(new ObservableCollection<int>(hints.OverrideCadIds));
        _ports.SetAnchorHints(new ObservableCollection<int>(hints.AnchorCadIdsForCanvas));
        _ports.SetAssignmentSummary(hints.Summary);
    }

    private void RefreshLatestCadOutputFwDiffAssignmentDecisions(
        IReadOnlyList<CadPad> ordered,
        bool allowCsvRepairAutoApply = false)
    {
        var grid = _inputs.Grid();
        if (grid is null || _inputs.MatchLinks().Count == 0)
        {
            SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
            return;
        }

        var regularPadById = grid.Pads.ToDictionary(static pad => pad.RegularPadId);
        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered,
            _inputs.MatchLinks(),
            regularPadById,
            _cadIcIndexByCadId,
            _inputs.ActiveMask(),
            _cadOutputFwDiffIndexByCadId,
            _inputs.Project().CadOutputFwDiffIndexOverrides);
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
        var activeMaskPadIds = _inputs.ActiveMask();
        if (activeMaskPadIds is null || activeMaskPadIds.Count == 0)
        {
            return decisions;
        }

        if (decisions.Count == 0)
        {
            return decisions;
        }

        var appliedCadIds = DxfCsvRepairRule.SelectRepairs(
            decisions,
            _inputs.Project().CadOutputFwDiffIndexOverrides,
            _cadOutputFwDiffIndexByCadId,
            _cadIcIndexByCadId);

        if (appliedCadIds.Count == 0)
        {
            return decisions;
        }

        var refreshed = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered,
            _inputs.MatchLinks(),
            regularPadById,
            _cadIcIndexByCadId,
            activeMaskPadIds,
            _cadOutputFwDiffIndexByCadId,
            _inputs.Project().CadOutputFwDiffIndexOverrides);
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

        _effects.Info(
            "Auto-applied csv local repair suggestions: count={0}, samples={1}.",
            appliedCadIds.Count,
            string.Join(", ", appliedCadIds.Take(5)));
        return refreshedByCadId;
    }

    public CadOutputFwDiffAssignmentDecisionSummarySnapshot GetCadOutputFwDiffAssignmentDecisionSummarySnapshot()
    {
        var snapshot = _ports.ReadSnapshot();
        if (snapshot.CadOutputFwDiffAssignmentDecisionsByCadId.Count == 0)
        {
            return CadOutputFwDiffAssignmentDecisionSummarySnapshot.Empty;
        }

        return DxfAssignmentSummaryBuilder.Build(snapshot.CadOutputFwDiffAssignmentDecisionsByCadId.Values.ToArray());
    }

    public void RefreshStep4DuplicateDiffGroups(
        IReadOnlyList<CadPad>? orderedVisiblePads = null,
        DxfDuplicateGroupInputs? workflowSnapshot = null)
    {
        var orderedPads = orderedVisiblePads ?? _inputs.CadPads().ToList();
        workflowSnapshot ??= _ports.ReadSnapshot();
        var result = DxfDuplicateGroupBuilder.Build(
            orderedPads,
            workflowSnapshot,
            _inputs.Grid());
        _step4DuplicateGroups.Replace(result.Groups);
        Step4Index.DuplicateDiffSummary = result.Summary;
        _notifications.Changed(nameof(Step4DuplicateDiffGroups));
        _notifications.Changed(nameof(HasStep4DuplicateDiffGroups));
    }

    public void SelectStep4DuplicateDiffGroup(Step4DuplicateDiffGroupViewModel? group)
    {
        if (group is null || group.CadPadIds.Count == 0)
        {
            return;
        }

        _effects.Locate(
            group.CadPadIds,
            Array.Empty<int>(),
            focusSelection: true,
            notVisibleStatus: $"Duplicate CAD Output FW Diff IC{group.IcIndex + 1}/diff{group.DiffIndex} is not visible.",
            successStatusBuilder: (cadCount, _) =>
                $"Duplicate CAD Output FW Diff focused: IC{group.IcIndex + 1}/diff{group.DiffIndex} ({cadCount} CAD pad(s)).");
    }

    private void OnStep4ShiftDeltaChanged()
    {
        _notifications.Changed(nameof(CanOffsetSelectedCadOutputFwDiffIndices));
        _notifications.Changed(nameof(Step4DiffShiftSelectionSummary));
    }

    public void OffsetSelectedCadOutputFwDiffIndices()
    {
        var workflowSnapshot = _ports.ReadSnapshot();
        var selectedCadIds = _inputs.SelectedCadIds()
            .Where(id => workflowSnapshot.CadOutputFwDiffIndexByCadId.ContainsKey(id))
            .OrderBy(static id => id)
            .ToList();
        if (selectedCadIds.Count == 0)
        {
            _effects.Status("CAD Output FW Diff offset: select CAD output pads first.");
            return;
        }

        var delta = (int)decimal.Truncate(Step4Index.ShiftDelta);
        var result = DxfDiffShiftService.ApplyShift(
            selectedCadIds,
            workflowSnapshot.CadOutputFwDiffIndexByCadId,
            workflowSnapshot.CadIcIndexByCadId,
            delta,
            _inputs.Project().CadOutputFwDiffIndexOverrides);
        var plan = result.Plan;
        if (!plan.IsSuccessful)
        {
            _effects.Status(plan.Message);
            return;
        }

        var overrides = _inputs.Project().CadOutputFwDiffIndexOverrides;
        var previousOverrides = result.PreviousOverrides;
        if (result.ChangedCount == 0)
        {
            _effects.Status("CAD Output FW Diff offset: selected CAD pads already match the requested offset.");
            return;
        }

        _effects.PushUndo(() =>
        {
            overrides.Clear();
            foreach (var pair in previousOverrides)
            {
                overrides[pair.Key] = pair.Value;
            }

            UpdateCadOutputFwDiffIndexing(_inputs.CadPads().ToList());
        }, "CAD Output FW Diff offset");

        UpdateCadOutputFwDiffIndexing(_inputs.CadPads().ToList());
        _effects.InvalidateDownstream();
        _effects.NotifySourceChanged();
        _effects.MarkUnsaved();
        _effects.Status(plan.Message);
        _effects.Info(
            "Diff idx batch shift applied. selected={0}, delta={1}, missing={2}.",
            plan.ShiftedDiffByCadId.Count,
            delta,
            plan.MissingCadPadIds.Count);
    }

}
