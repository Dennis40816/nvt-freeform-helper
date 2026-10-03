using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void DeleteSelectedCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF edit: import DXF first.", out _))
        {
            Logger.Debug(CultureInfo.InvariantCulture, "DXF edit hide skipped: no DXF loaded.");
            return;
        }

        var selected = _selectedCadIds
            .Where(id => !IsCadPadEffectivelyHidden(id))
            .ToList();
        if (selected.Count == 0)
        {
            SetStatus("DXF edit: select CAD pads first.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF edit hide skipped: no CAD pad selected.");
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        foreach (var id in selected)
        {
            _hiddenCadPadIds.Add(id);
        }

        _hiddenCadUndoStack.Push(selected);
        SyncHiddenCadIdsToProject();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus($"DXF edit: hid {selected.Count} CAD pad(s).");
        Logger.Info(CultureInfo.InvariantCulture, "DXF edit hid {0} pad(s). Hidden total={1}.", selected.Count, _hiddenCadPadIds.Count);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF hide CAD pads");
    }

    private void RestoreLastDeletedCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF edit: import DXF first.", out _))
        {
            Logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-last skipped: no DXF loaded.");
            return;
        }

        while (_hiddenCadUndoStack.Count > 0)
        {
            var batch = _hiddenCadUndoStack.Pop();
            var restored = RestoreHiddenCadPadsCore(
                batch,
                undoLabel: "DXF restore hidden CAD pads",
                statusTextFactory: count => $"DXF edit: restored {count} CAD pad(s).",
                logContext: "DXF edit restored last batch",
                removeFromUndoBatches: false);
            if (restored <= 0)
            {
                continue;
            }
            return;
        }

        SetStatus("DXF edit: no deleted CAD pads to restore.");
        Logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-last skipped: no deleted CAD pads.");
    }

    private void RestoreAllHiddenCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF edit: import DXF first.", out _))
        {
            Logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-hidden-all skipped: no DXF loaded.");
            return;
        }

        var hiddenCadIds = GetManualHiddenCadPadIds();
        if (hiddenCadIds.Count == 0)
        {
            SetStatus("DXF edit: no manual hidden CAD pads to restore.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-hidden-all skipped: no manual hidden CAD pads.");
            return;
        }

        RestoreHiddenCadPadsCore(
            hiddenCadIds,
            undoLabel: "DXF restore all hidden CAD pads",
            statusTextFactory: count => $"DXF edit: restored all manual hidden CAD pads ({count}).",
            logContext: "DXF edit restored all manual hidden CAD pads",
            removeFromUndoBatches: true);
    }

    private void RestoreDuplicateCadPad(int cadPadId)
    {
        SetDuplicateCadPadAutoHiddenState([cadPadId], autoHide: false, "DXF restore duplicate CAD pad", count =>
            $"DXF duplicate: restored {count} CAD pad(s).");
    }

    private void RehideDuplicateCadPad(int cadPadId)
    {
        SetDuplicateCadPadAutoHiddenState([cadPadId], autoHide: true, "DXF rehide duplicate CAD pad", count =>
            $"DXF duplicate: auto-hidden {count} CAD pad(s) again.");
    }

    private void SetDuplicateCadPadAutoHiddenState(
        IEnumerable<int> cadPadIds,
        bool autoHide,
        string undoLabel,
        Func<int, string> statusTextFactory)
    {
        if (_cad is null)
        {
            return;
        }

        var duplicateIds = cadPadIds
            .Where(_duplicateCadPadIdToCanonicalId.ContainsKey)
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
        if (duplicateIds.Count == 0)
        {
            return;
        }

        var changedIds = new List<int>();
        foreach (var duplicateId in duplicateIds)
        {
            var isCurrentlyAutoHidden = _autoHiddenDuplicateCadPadIds.Contains(duplicateId);
            if (autoHide == isCurrentlyAutoHidden)
            {
                continue;
            }

            changedIds.Add(duplicateId);
        }

        if (changedIds.Count == 0)
        {
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        foreach (var duplicateId in changedIds)
        {
            if (autoHide)
            {
                _autoHiddenDuplicateCadPadIds.Add(duplicateId);
            }
            else
            {
                _autoHiddenDuplicateCadPadIds.Remove(duplicateId);
            }
        }

        _cachedFilteredCadForBuild = null;
        SyncDuplicateCadState();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus(statusTextFactory(changedIds.Count));
        Logger.Info(
            CultureInfo.InvariantCulture,
            "DXF duplicate visibility updated. autoHide={0}, changed={1}, autoHiddenTotal={2}.",
            autoHide,
            changedIds.Count,
            _autoHiddenDuplicateCadPadIds.Count);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), undoLabel);
    }

    private List<int> GetManualHiddenCadPadIds()
    {
        return _hiddenCadPadIds
            .Where(id => !_combinedCadGroupIdBySourceCadId.ContainsKey(id))
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
    }

    private int RestoreHiddenCadPadsCore(
        IEnumerable<int> cadPadIds,
        string undoLabel,
        Func<int, string> statusTextFactory,
        string logContext,
        bool removeFromUndoBatches)
    {
        if (_cad is null)
        {
            return 0;
        }

        var restorableIds = cadPadIds
            .Where(id => _hiddenCadPadIds.Contains(id) && !_combinedCadGroupIdBySourceCadId.ContainsKey(id))
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
        if (restorableIds.Count == 0)
        {
            return 0;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        foreach (var id in restorableIds)
        {
            _hiddenCadPadIds.Remove(id);
            if (removeFromUndoBatches)
            {
                RemoveHiddenCadIdFromUndoBatches(id);
            }
        }

        _cachedFilteredCadForBuild = null;
        SyncHiddenCadIdsToProject();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus(statusTextFactory(restorableIds.Count));
        Logger.Info(
            CultureInfo.InvariantCulture,
            "{0}. restored={1}, hiddenTotal={2}.",
            logContext,
            restorableIds.Count,
            _hiddenCadPadIds.Count);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), undoLabel);
        return restorableIds.Count;
    }
}
