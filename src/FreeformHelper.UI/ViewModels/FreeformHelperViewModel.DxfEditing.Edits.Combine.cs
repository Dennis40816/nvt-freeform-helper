using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void CombineSelectedCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF combine: import DXF first.", out var cad))
        {
            Logger.Debug(CultureInfo.InvariantCulture, "DXF combine skipped: no DXF loaded.");
            return;
        }

        var selected = _selectedCadIds
            .Where(id => !IsCadPadEffectivelyHidden(id))
            .Distinct()
            .ToList();
        if (selected.Count < 2)
        {
            SetStatus("DXF combine: select at least 2 CAD pads.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF combine skipped: less than 2 pads selected.");
            return;
        }

        var selectedSet = selected.ToHashSet();
        var sourcePads = cad.Pads
            .Where(p => selectedSet.Contains(p.Id) && !IsCadPadEffectivelyHidden(p.Id))
            .ToList();
        if (sourcePads.Count < 2)
        {
            SetStatus("DXF combine: select at least 2 CAD output pads.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF combine skipped: less than 2 visible source pads.");
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        var unionResult = Application.Services.CadPadUnionService.Union(sourcePads.Select(p => p.Polygon));
        if (unionResult.OuterPolygons.Count == 0)
        {
            SetStatus("DXF combine: failed to build combined shape.");
            Logger.Warn(CultureInfo.InvariantCulture, "DXF combine failed: union output empty.");
            return;
        }

        var layer = sourcePads[0].Layer;
        var combinedPads = new List<CadPad>(unionResult.OuterPolygons.Count);
        var groupId = _nextCombinedCadGroupId++;
        foreach (var polygon in unionResult.OuterPolygons)
        {
            var combinedId = AllocateCombinedCadPadId();
            combinedPads.Add(new CadPad(
                combinedId,
                $"COMB_{combinedId}",
                layer,
                polygon));
            _combinedCadPadIds.Add(combinedId);
            _combinedCadGroupIdByOutputCadId[combinedId] = groupId;
        }

        var sourcePadIds = sourcePads.Select(static p => p.Id).Distinct().ToList();
        _combinedCadGroups[groupId] = new CombinedCadGroupState(
            sourcePadIds,
            combinedPads.Select(static pad => pad.Id).ToList());

        _cad = new CadPadSet(cad.Pads.Concat(combinedPads).ToList());
        foreach (var sourceId in sourcePadIds)
        {
            _hiddenCadPadIds.Add(sourceId);
            _combinedSourceCadPadIds.Add(sourceId);
            _combinedCadGroupIdBySourceCadId[sourceId] = groupId;
        }

        SyncRelayeredCadState();
        SyncCombinedCadState();
        SyncHiddenCadIdsToProject();
        _cachedFilteredCadForBuild = null;
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus($"DXF combine: merged {sourcePads.Count} pads into {combinedPads.Count}.");
        Logger.Info(CultureInfo.InvariantCulture, "DXF combine applied. source={0}, outputPads={1}, hiddenTotal={2}, combinedTotal={3}, holes={4}.",
            sourcePads.Count,
            combinedPads.Count,
            _hiddenCadPadIds.Count,
            _combinedCadPadIds.Count,
            unionResult.HolePathCount);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF combine CAD pads");
    }

    private void ClearCombinedCadPads()
    {
        if (_cad is null || _combinedCadPadIds.Count == 0)
        {
            SetStatus("DXF combine: no combined pad.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF combine clear skipped: no combined pads.");
            return;
        }

        var selectedGroupIds = _selectedCadIds
            .Select(id => TryResolveCombinedCadGroupId(id, out var groupId) ? groupId : 0)
            .Where(static groupId => groupId > 0)
            .Distinct()
            .OrderBy(static groupId => groupId)
            .ToList();
        if (selectedGroupIds.Count == 0)
        {
            SetStatus("DXF combine: select combined CAD pads first.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF combine clear skipped: no combined CAD pad selected.");
            return;
        }

        ClearCombinedCadGroups(
            selectedGroupIds,
            undoLabel: "DXF clear selected combined CAD pads",
            statusPrefix: "DXF combine: cleared selected groups",
            logContext: "DXF combine cleared selected groups");
    }

    private void ClearCombinedCadGroups(
        List<int> groupIds,
        string undoLabel,
        string statusPrefix,
        string logContext)
    {
        if (_cad is null || groupIds.Count == 0)
        {
            return;
        }

        var validGroupIds = groupIds
            .Where(_combinedCadGroups.ContainsKey)
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
        if (validGroupIds.Count == 0)
        {
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        var outputIds = new HashSet<int>();
        var sourceIds = new HashSet<int>();
        foreach (var groupId in validGroupIds)
        {
            if (!_combinedCadGroups.TryGetValue(groupId, out var group))
            {
                continue;
            }

            foreach (var outputId in group.OutputCadIds)
            {
                outputIds.Add(outputId);
            }

            foreach (var sourceId in group.SourceCadIds)
            {
                sourceIds.Add(sourceId);
            }
        }

        _cad = new CadPadSet(_cad.Pads.Where(pad => !outputIds.Contains(pad.Id)).ToList());
        foreach (var outputId in outputIds)
        {
            _combinedCadPadIds.Remove(outputId);
            _combinedCadGroupIdByOutputCadId.Remove(outputId);
            _hiddenCadPadIds.Remove(outputId);
        }

        var restoredSources = 0;
        foreach (var sourceId in sourceIds)
        {
            if (_hiddenCadPadIds.Remove(sourceId))
            {
                restoredSources++;
            }

            _combinedSourceCadPadIds.Remove(sourceId);
            _combinedCadGroupIdBySourceCadId.Remove(sourceId);
        }

        foreach (var groupId in validGroupIds)
        {
            _combinedCadGroups.Remove(groupId);
        }

        if (_combinedCadGroups.Count == 0)
        {
            _nextCombinedCadGroupId = 1;
        }

        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState();
        SyncRotatedCadState();
        SyncCombinedCadState();
        SyncHiddenCadIdsToProject();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus($"{statusPrefix}: groups={validGroupIds.Count}, combined={outputIds.Count}, restored={restoredSources}.");
        Logger.Info(
            CultureInfo.InvariantCulture,
            "{0}. groups={1}, combinedRemoved={2}, restoredSources={3}, hiddenTotal={4}.",
            logContext,
            validGroupIds.Count,
            outputIds.Count,
            restoredSources,
            _hiddenCadPadIds.Count);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), undoLabel);
    }

    private int AllocateCombinedCadPadId()
    {
        if (_cad is null)
        {
            return _nextCombinedCadPadId++;
        }

        var used = _cad.Pads.Select(p => p.Id).ToHashSet();
        while (used.Contains(_nextCombinedCadPadId))
        {
            _nextCombinedCadPadId++;
        }

        return _nextCombinedCadPadId++;
    }
}
