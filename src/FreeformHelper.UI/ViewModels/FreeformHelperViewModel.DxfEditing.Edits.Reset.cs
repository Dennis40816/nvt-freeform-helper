using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void ResetAllDxfEdits()
    {
        if (!HasAnyCadEdits)
        {
            SetStatus("DXF edit: no hidden/combined/layer edits.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF edit reset-all skipped: nothing to reset.");
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        var previous = new CadEditNotificationSnapshot(this);
        var restored = _hiddenCadPadIds.Count;
        var removedCombined = _combinedCad.OutputCadIds.Count;
        var restoredLayerMoves = RelayeredCadPadCount;
        var restoredRotations = RotatedCadPadCount;
        if (_dxfEditBaselineCad is not null)
        {
            _cad = new CadPadSet(_dxfEditBaselineCad.Pads.ToList());
        }
        else
        {
            RemoveCombinedPadsFromCadSet();
        }

        _hiddenCadPadIds.Clear();
        _hiddenCadUndoStack.Clear();
        _combinedCad.Clear();
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState(previous);
        SyncRotatedCadState(previous);
        SyncHiddenCadIdsToProject(previous);
        SyncCombinedCadState(previous);
        SyncDuplicateCadState(previous);
        if (_cad is not null)
        {
            var layerSelectionSnapshot = CaptureLayerSelectionSnapshot();
            PreserveDxfDependentLayerSelections();
            UpdateLayerToggles(_cad);
            RestoreLayerSelectionAfterCadLayerEdit(layerSelectionSnapshot, ensureVisibleLayerName: string.Empty);
        }

        FilterCadPadsByLayer();
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        SetStatus($"DXF edit: reset hidden ({restored}), combined ({removedCombined}), layer edits ({restoredLayerMoves}), rotated ({restoredRotations}).");
        Logger.Info(
            CultureInfo.InvariantCulture,
            "DXF edit reset all edits. hidden={0}, combined={1}, relayered={2}, rotated={3}.",
            restored,
            removedCombined,
            restoredLayerMoves,
            restoredRotations);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF reset all edits");
    }

    private int RemoveCombinedPadsFromCadSet()
    {
        if (_cad is null || _combinedCad.OutputCadIds.Count == 0)
        {
            return 0;
        }

        var removed = _combinedCad.OutputCadIds.Count;
        _cad = new CadPadSet(_cad.Pads.Where(p => !_combinedCad.OutputCadIds.Contains(p.Id)).ToList());
        _combinedCad.ClearGroups();
        return removed;
    }
}
