// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.CadEditing;

namespace FreeformHelper.UI.Services.CadEditing;

internal sealed partial class DxfEditWorkflow
{
    public void ResetAllDxfEdits()
    {
        if (!publication.Capture().HasAny)
        {
            effects.SetStatus("DXF edit: no hidden/combined/layer edits.");
            _logger.Debug(CultureInfo.InvariantCulture, "DXF edit reset-all skipped: nothing to reset.");
            return;
        }

        var undoSnapshot = document.CaptureUndo();
        var previous = publication.Capture();
        var restored = document.Edit.HiddenCadPadIds.Count;
        var removedCombined = document.Combined.OutputCadIds.Count;
        var restoredLayerMoves = document.EditDelta.Relayered;
        var restoredRotations = document.EditDelta.Rotated;
        ResetDocumentEdits(previous);

        effects.SetStatus($"DXF edit: reset hidden ({restored}), combined ({removedCombined}), layer edits ({restoredLayerMoves}), rotated ({restoredRotations}).");
        _logger.Info(
            CultureInfo.InvariantCulture,
            "DXF edit reset all edits. hidden={0}, combined={1}, relayered={2}, rotated={3}.",
            restored,
            removedCombined,
            restoredLayerMoves,
            restoredRotations);
        effects.MarkUnsaved();
        effects.PushUndo(() => engine.RestoreUndo(undoSnapshot), "DXF reset all edits");
    }

    private int RemoveCombinedPadsFromCadSet()
    {
        if (document.Cad is null || document.Combined.OutputCadIds.Count == 0)
        {
            return 0;
        }

        var removed = document.Combined.OutputCadIds.Count;
        document.Cad = new CadPadSet(document.Cad.Pads.Where(p => !document.Combined.OutputCadIds.Contains(p.Id)).ToList());
        document.Combined.ClearGroups();
        return removed;
    }

    private void ResetDocumentEdits(CadEditNotificationSnapshot previous)
    {
        if (document.Edit.BaselineCad is not null)
        {
            document.Cad = new CadPadSet(document.Edit.BaselineCad.Pads.ToList());
        }
        else
        {
            RemoveCombinedPadsFromCadSet();
        }

        document.Edit.HiddenCadPadIds.Clear();
        document.Edit.HiddenUndoStack.Clear();
        document.Combined.Clear();
        document.InvalidateFilteredCad();
        publication.Relayered(previous);
        publication.Rotated(previous);
        publication.Hidden(previous);
        publication.Combined(previous);
        publication.Duplicate(previous, true);
        if (document.Cad is not null)
        {
            var layerSelectionSnapshot = layers.CaptureSelection();
            layers.PreserveDependentSelections();
            layers.Update(document.Cad);
            layers.RestoreSelection(layerSelectionSnapshot, string.Empty);
        }

        effects.Filter();
        effects.RebuildUnlessLayerFilterDoes();

    }
}
