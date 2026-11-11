using System.Globalization;
using FreeformHelper.Application.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void DeleteSelectedCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF edit: import DXF first.", out _))
        {
            _logger.Debug(CultureInfo.InvariantCulture, "DXF edit hide skipped: no DXF loaded.");
            return;
        }

        var selected = DxfVisibilityRules.SelectVisibleSelectedCadIds(_selectedCadIds, CadDocument.IsEffectivelyHidden);
        if (selected.Count == 0)
        {
            SetStatus("DXF edit: select CAD pads first.");
            _logger.Debug(CultureInfo.InvariantCulture, "DXF edit hide skipped: no CAD pad selected.");
            return;
        }

        var undoSnapshot = CadDocument.CaptureUndo();
        var previous = CaptureCadEditNotifications();
        foreach (var id in selected)
        {
            _cadEdit.HiddenCadPadIds.Add(id);
        }

        _cadEdit.HiddenUndoStack.Push(selected);
        SyncHiddenCadIdsToProject(previous);
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus($"DXF edit: hid {selected.Count} CAD pad(s).");
        _logger.Info(CultureInfo.InvariantCulture, "DXF edit hid {0} pad(s). Hidden total={1}.", selected.Count, _cadEdit.HiddenCadPadIds.Count);
        MarkUnsaved();
        PushUndo(() => _cadEditWorkflow.RestoreUndo(undoSnapshot), "DXF hide CAD pads");
    }

    private void RestoreLastDeletedCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF edit: import DXF first.", out _))
        {
            _logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-last skipped: no DXF loaded.");
            return;
        }

        while (_cadEdit.HiddenUndoStack.Count > 0)
        {
            var batch = _cadEdit.HiddenUndoStack.Pop();
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
        _logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-last skipped: no deleted CAD pads.");
    }

    private void RestoreAllHiddenCadPads()
    {
        if (!TryGetLoadedCad(() => _cad, "DXF edit: import DXF first.", out _))
        {
            _logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-hidden-all skipped: no DXF loaded.");
            return;
        }

        var hiddenCadIds = DxfVisibilityRules.SelectManualHiddenCadPadIds(_cadEdit.HiddenCadPadIds, _combinedCad);
        if (hiddenCadIds.Count == 0)
        {
            SetStatus("DXF edit: no manual hidden CAD pads to restore.");
            _logger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-hidden-all skipped: no manual hidden CAD pads.");
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

        var changedIds = DxfVisibilityRules.SelectChangedDuplicateCadPadIds(
            cadPadIds, _cadEdit.DuplicateCadPadIdToCanonicalId, _cadEdit.AutoHiddenDuplicateCadPadIds, autoHide);
        if (changedIds.Count == 0)
        {
            return;
        }

        var undoSnapshot = CadDocument.CaptureUndo();
        foreach (var duplicateId in changedIds)
        {
            if (autoHide)
            {
                _cadEdit.AutoHiddenDuplicateCadPadIds.Add(duplicateId);
            }
            else
            {
                _cadEdit.AutoHiddenDuplicateCadPadIds.Remove(duplicateId);
            }
        }

        CadDocument.InvalidateFilteredCad();
        SyncDuplicateCadState();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus(statusTextFactory(changedIds.Count));
        _logger.Info(
            CultureInfo.InvariantCulture,
            "DXF duplicate visibility updated. autoHide={0}, changed={1}, autoHiddenTotal={2}.",
            autoHide,
            changedIds.Count,
            _cadEdit.AutoHiddenDuplicateCadPadIds.Count);
        MarkUnsaved();
        PushUndo(() => _cadEditWorkflow.RestoreUndo(undoSnapshot), undoLabel);
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

        var restorableIds = DxfVisibilityRules.SelectRestorableHiddenCadPadIds(
            cadPadIds, _cadEdit.HiddenCadPadIds, _combinedCad);
        if (restorableIds.Count == 0)
        {
            return 0;
        }

        var undoSnapshot = CadDocument.CaptureUndo();
        var previous = CaptureCadEditNotifications();
        foreach (var id in restorableIds)
        {
            _cadEdit.HiddenCadPadIds.Remove(id);
            if (removeFromUndoBatches)
            {
                CadDocument.RemoveHiddenIdFromUndoBatches(id);
            }
        }

        CadDocument.InvalidateFilteredCad();
        SyncHiddenCadIdsToProject(previous);
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus(statusTextFactory(restorableIds.Count));
        _logger.Info(
            CultureInfo.InvariantCulture,
            "{0}. restored={1}, hiddenTotal={2}.",
            logContext,
            restorableIds.Count,
            _cadEdit.HiddenCadPadIds.Count);
        MarkUnsaved();
        PushUndo(() => _cadEditWorkflow.RestoreUndo(undoSnapshot), undoLabel);
        return restorableIds.Count;
    }
}
