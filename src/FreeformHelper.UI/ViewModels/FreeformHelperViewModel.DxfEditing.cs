using System.Text;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Handles in-app DXF edit operations (hide/restore) and DXF export workflows.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private sealed record CadEditUndoSnapshot(
        CadPadSet? Cad,
        List<int> HiddenCadIds,
        List<int> AutoHiddenDuplicateCadIds,
        List<List<int>> HiddenUndoBatchesTopToBottom,
        Dictionary<int, int> DuplicateCadPadIdToCanonicalId,
        CombinedCadState Combined);

    private CadEditUndoSnapshot CaptureCadEditUndoSnapshot()
    {
        return new CadEditUndoSnapshot(
            _cad,
            _cadEdit.HiddenCadPadIds.ToList(),
            _cadEdit.AutoHiddenDuplicateCadPadIds.ToList(),
            _cadEdit.HiddenUndoStack.Select(batch => batch.ToList()).ToList(),
            _cadEdit.DuplicateCadPadIdToCanonicalId.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            _combinedCad.Clone());
    }

    private void RestoreCadEditUndoSnapshot(CadEditUndoSnapshot snapshot)
    {
        var previous = new CadEditNotificationSnapshot(this);
        _cad = snapshot.Cad;
        _cadEdit.HiddenCadPadIds.Clear();
        foreach (var id in snapshot.HiddenCadIds)
        {
            _cadEdit.HiddenCadPadIds.Add(id);
        }

        _cadEdit.AutoHiddenDuplicateCadPadIds.Clear();
        foreach (var id in snapshot.AutoHiddenDuplicateCadIds)
        {
            _cadEdit.AutoHiddenDuplicateCadPadIds.Add(id);
        }

        _cadEdit.HiddenUndoStack.Clear();
        for (var index = snapshot.HiddenUndoBatchesTopToBottom.Count - 1; index >= 0; index--)
        {
            _cadEdit.HiddenUndoStack.Push(snapshot.HiddenUndoBatchesTopToBottom[index].ToList());
        }

        _cadEdit.DuplicateCadPadIdToCanonicalId.Clear();
        foreach (var pair in snapshot.DuplicateCadPadIdToCanonicalId)
        {
            _cadEdit.DuplicateCadPadIdToCanonicalId[pair.Key] = pair.Value;
        }

        _combinedCad.CopyFrom(snapshot.Combined);
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState(previous);
        SyncRotatedCadState(previous);
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        SyncCombinedCadState(previous);
        SyncHiddenCadIdsToProject(previous);
        SyncDuplicateCadState(previous);
        if (_cad is not null)
        {
            var currentLayers = LayerToggles
                .Select(toggle => toggle.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedLayers = BuildCadLayerUiSnapshot(_cad, layerSelections: null).ToggleStates
                .Select(static state => state.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!currentLayers.SetEquals(expectedLayers))
            {
                var layerSelectionSnapshot = CaptureLayerSelectionSnapshot();
                PreserveDxfDependentLayerSelections();
                UpdateLayerToggles(_cad);
                RestoreLayerSelectionAfterCadLayerEdit(layerSelectionSnapshot, ensureVisibleLayerName: string.Empty);
            }
            else
            {
                FilterCadPadsByLayer();
            }
        }
        else
        {
            FilterCadPadsByLayer();
        }
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        MarkUnsaved();
    }

    private CadPadSet? BuildActiveCadPadSet()
    {
        if (_cad is null)
        {
            return null;
        }

        if (_cadEdit.HiddenCadPadIds.Count == 0 && _cadEdit.AutoHiddenDuplicateCadPadIds.Count == 0)
        {
            return _cad;
        }

        var visible = _cad.Pads
            .Where(p => !IsCadPadEffectivelyHidden(p.Id))
            .ToList();
        return new CadPadSet(visible);
    }

    private static string SanitizeFileNameToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "layer";
        }

        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        return builder.Length == 0 ? "layer" : builder.ToString();
    }

    private void RemoveHiddenCadIdFromUndoBatches(int cadPadId)
    {
        if (_cadEdit.HiddenUndoStack.Count == 0)
        {
            return;
        }

        var batchesTopToBottom = _cadEdit.HiddenUndoStack
            .Select(static batch => batch.ToList())
            .ToList();
        _cadEdit.HiddenUndoStack.Clear();

        for (var index = batchesTopToBottom.Count - 1; index >= 0; index--)
        {
            var batch = batchesTopToBottom[index];
            batch.RemoveAll(id => id == cadPadId);
            if (batch.Count > 0)
            {
                _cadEdit.HiddenUndoStack.Push(batch);
            }
        }
    }
}
