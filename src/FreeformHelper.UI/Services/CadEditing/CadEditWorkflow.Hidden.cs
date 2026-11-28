// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services.CadEditing;

internal sealed partial class CadEditWorkflow
{
    internal void DeleteSelectedCadPads()
    {
        if (!TryGetLoadedCad("DXF edit: import DXF first.", out _))
        {
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF edit hide skipped: no DXF loaded.");
            return;
        }

        var selected = DxfVisibilityRules.SelectVisibleSelectedCadIds(interaction.SelectedCadIds(), document.IsEffectivelyHidden);
        if (selected.Count == 0)
        {
            effects.SetStatus("DXF edit: select CAD pads first.");
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF edit hide skipped: no CAD pad selected.");
            return;
        }

        var undoSnapshot = document.CaptureUndo();
        var previous = publication.Capture();
        foreach (var id in selected)
        {
            document.Edit.HiddenCadPadIds.Add(id);
        }

        document.Edit.HiddenUndoStack.Push(selected);
        publication.Hidden(previous);
        RefreshAfterVisibilityEdit();

        effects.SetStatus($"DXF edit: hid {selected.Count} CAD pad(s).");
        _editLogger.Info(CultureInfo.InvariantCulture, "DXF edit hid {0} pad(s). Hidden total={1}.", selected.Count, document.Edit.HiddenCadPadIds.Count);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undoSnapshot), "DXF hide CAD pads");
    }

    internal void RestoreLastDeletedCadPads()
    {
        if (!TryGetLoadedCad("DXF edit: import DXF first.", out _))
        {
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-last skipped: no DXF loaded.");
            return;
        }

        while (document.Edit.HiddenUndoStack.Count > 0)
        {
            // Preserve the characterized restore-last behavior: capture undo only after popping this batch.
            var batch = document.Edit.HiddenUndoStack.Pop();
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

        effects.SetStatus("DXF edit: no deleted CAD pads to restore.");
        _editLogger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-last skipped: no deleted CAD pads.");
    }

    internal void RestoreAllHiddenCadPads()
    {
        if (!TryGetLoadedCad("DXF edit: import DXF first.", out _))
        {
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-hidden-all skipped: no DXF loaded.");
            return;
        }

        var hiddenCadIds = DxfVisibilityRules.SelectManualHiddenCadPadIds(document.Edit.HiddenCadPadIds, document.Combined);
        if (hiddenCadIds.Count == 0)
        {
            effects.SetStatus("DXF edit: no manual hidden CAD pads to restore.");
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF edit restore-hidden-all skipped: no manual hidden CAD pads.");
            return;
        }

        RestoreHiddenCadPadsCore(
            hiddenCadIds,
            undoLabel: "DXF restore all hidden CAD pads",
            statusTextFactory: count => $"DXF edit: restored all manual hidden CAD pads ({count}).",
            logContext: "DXF edit restored all manual hidden CAD pads",
            removeFromUndoBatches: true);
    }

    private void SetDuplicateCadPadAutoHiddenState(
        IEnumerable<int> cadPadIds,
        bool autoHide,
        string undoLabel,
        Func<int, string> statusTextFactory)
    {
        if (document.Cad is null)
        {
            return;
        }

        var changedIds = DxfVisibilityRules.SelectChangedDuplicateCadPadIds(
            cadPadIds, document.Edit.DuplicateCadPadIdToCanonicalId, document.Edit.AutoHiddenDuplicateCadPadIds, autoHide);
        if (changedIds.Count == 0)
        {
            return;
        }

        var undoSnapshot = document.CaptureUndo();
        SetDuplicateVisibility(changedIds, autoHide);

        document.InvalidateFilteredCad();
        publication.Duplicate(null, true);
        RefreshAfterVisibilityEdit();

        effects.SetStatus(statusTextFactory(changedIds.Count));
        _editLogger.Info(
            CultureInfo.InvariantCulture,
            "DXF duplicate visibility updated. autoHide={0}, changed={1}, autoHiddenTotal={2}.",
            autoHide,
            changedIds.Count,
            document.Edit.AutoHiddenDuplicateCadPadIds.Count);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undoSnapshot), undoLabel);
    }

    private int RestoreHiddenCadPadsCore(
        IEnumerable<int> cadPadIds,
        string undoLabel,
        Func<int, string> statusTextFactory,
        string logContext,
        bool removeFromUndoBatches)
    {
        if (document.Cad is null)
        {
            return 0;
        }

        var restorableIds = DxfVisibilityRules.SelectRestorableHiddenCadPadIds(
            cadPadIds, document.Edit.HiddenCadPadIds, document.Combined);
        if (restorableIds.Count == 0)
        {
            return 0;
        }

        var undoSnapshot = document.CaptureUndo();
        var previous = publication.Capture();
        RemoveHiddenIds(restorableIds, removeFromUndoBatches);

        document.InvalidateFilteredCad();
        publication.Hidden(previous);
        RefreshAfterVisibilityEdit();

        effects.SetStatus(statusTextFactory(restorableIds.Count));
        _editLogger.Info(
            CultureInfo.InvariantCulture,
            "{0}. restored={1}, hiddenTotal={2}.",
            logContext,
            restorableIds.Count,
            document.Edit.HiddenCadPadIds.Count);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undoSnapshot), undoLabel);
        return restorableIds.Count;
    }

    private void RemoveHiddenIds(IEnumerable<int> ids, bool removeFromUndoBatches)
    {
        foreach (var id in ids)
        {
            document.Edit.HiddenCadPadIds.Remove(id);
            if (removeFromUndoBatches)
            {
                document.RemoveHiddenIdFromUndoBatches(id);
            }
        }
    }

    private void RefreshAfterVisibilityEdit()
    {
        effects.ClosePadInfo();
        effects.ClearSelection();
        effects.Filter();
        effects.RebuildUnlessLayerFilterDoes();
    }

    private void SetDuplicateVisibility(IEnumerable<int> changedIds, bool autoHide)
    {
        foreach (var duplicateId in changedIds)
        {
            if (autoHide) document.Edit.AutoHiddenDuplicateCadPadIds.Add(duplicateId);
            else document.Edit.AutoHiddenDuplicateCadPadIds.Remove(duplicateId);
        }
    }

    internal bool TryGetLoadedCad(string missingStatus, out CadPadSet cad)
    {
        cad = document.Cad!;
        if (cad is not null) return true;
        effects.SetStatus(missingStatus);
        return false;
    }
    internal void ToggleDuplicateEntry(int cadPadId)
    {
        if (document.Edit.AutoHiddenDuplicateCadPadIds.Contains(cadPadId))
        {
            SetDuplicateCadPadAutoHiddenState([cadPadId], autoHide: false, "DXF restore duplicate CAD pad",
                count => $"DXF duplicate: restored {count} CAD pad(s).");
        }
        else
        {
            SetDuplicateCadPadAutoHiddenState([cadPadId], autoHide: true, "DXF rehide duplicate CAD pad",
                count => $"DXF duplicate: auto-hidden {count} CAD pad(s) again.");
        }
    }
    internal void RestoreHiddenCadPad(int cadPadId)
    {
        if (document.Cad is null || !document.Edit.HiddenCadPadIds.Contains(cadPadId))
        {
            return;
        }

        RestoreHiddenCadPadsCore(
            [cadPadId],
            undoLabel: "DXF restore hidden CAD pad",
            statusTextFactory: _ => $"DXF edit: restored CAD {cadPadId}.",
            logContext: $"DXF edit restored CAD pad {cadPadId}",
            removeFromUndoBatches: true);
    }
}
