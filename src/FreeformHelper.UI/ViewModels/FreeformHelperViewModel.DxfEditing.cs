using System.Text;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Handles in-app DXF edit operations (hide/restore) and DXF export workflows.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private sealed record CombinedCadGroupState(
        List<int> SourceCadIds,
        List<int> OutputCadIds);

    private sealed record CadEditUndoSnapshot(
        CadPadSet? Cad,
        List<int> HiddenCadIds,
        List<int> AutoHiddenDuplicateCadIds,
        List<List<int>> HiddenUndoBatchesTopToBottom,
        List<int> CombinedCadIds,
        List<int> CombinedSourceCadIds,
        Dictionary<int, int> DuplicateCadPadIdToCanonicalId,
        Dictionary<int, int> CombinedGroupIdByOutputCadId,
        Dictionary<int, int> CombinedGroupIdBySourceCadId,
        Dictionary<int, CombinedCadGroupState> CombinedGroups,
        int NextCombinedCadGroupId,
        int NextCombinedCadPadId);

    private CadEditUndoSnapshot CaptureCadEditUndoSnapshot()
    {
        return new CadEditUndoSnapshot(
            _cad,
            _hiddenCadPadIds.ToList(),
            _autoHiddenDuplicateCadPadIds.ToList(),
            _hiddenCadUndoStack.Select(batch => batch.ToList()).ToList(),
            _combinedCadPadIds.ToList(),
            _combinedSourceCadPadIds.ToList(),
            _duplicateCadPadIdToCanonicalId.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            _combinedCadGroupIdByOutputCadId.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            _combinedCadGroupIdBySourceCadId.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            _combinedCadGroups.ToDictionary(
                static pair => pair.Key,
                static pair => new CombinedCadGroupState(
                    pair.Value.SourceCadIds.ToList(),
                    pair.Value.OutputCadIds.ToList())),
            _nextCombinedCadGroupId,
            _nextCombinedCadPadId);
    }

    private void RestoreCadEditUndoSnapshot(CadEditUndoSnapshot snapshot)
    {
        _cad = snapshot.Cad;

        _hiddenCadPadIds.Clear();
        foreach (var id in snapshot.HiddenCadIds)
        {
            _hiddenCadPadIds.Add(id);
        }

        _autoHiddenDuplicateCadPadIds.Clear();
        foreach (var id in snapshot.AutoHiddenDuplicateCadIds)
        {
            _autoHiddenDuplicateCadPadIds.Add(id);
        }

        _hiddenCadUndoStack.Clear();
        for (var index = snapshot.HiddenUndoBatchesTopToBottom.Count - 1; index >= 0; index--)
        {
            _hiddenCadUndoStack.Push(snapshot.HiddenUndoBatchesTopToBottom[index].ToList());
        }

        _combinedCadPadIds.Clear();
        foreach (var id in snapshot.CombinedCadIds)
        {
            _combinedCadPadIds.Add(id);
        }

        _combinedSourceCadPadIds.Clear();
        foreach (var id in snapshot.CombinedSourceCadIds)
        {
            _combinedSourceCadPadIds.Add(id);
        }

        _duplicateCadPadIdToCanonicalId.Clear();
        foreach (var pair in snapshot.DuplicateCadPadIdToCanonicalId)
        {
            _duplicateCadPadIdToCanonicalId[pair.Key] = pair.Value;
        }

        _combinedCadGroupIdByOutputCadId.Clear();
        foreach (var pair in snapshot.CombinedGroupIdByOutputCadId)
        {
            _combinedCadGroupIdByOutputCadId[pair.Key] = pair.Value;
        }

        _combinedCadGroupIdBySourceCadId.Clear();
        foreach (var pair in snapshot.CombinedGroupIdBySourceCadId)
        {
            _combinedCadGroupIdBySourceCadId[pair.Key] = pair.Value;
        }

        _combinedCadGroups.Clear();
        foreach (var pair in snapshot.CombinedGroups)
        {
            _combinedCadGroups[pair.Key] = new CombinedCadGroupState(
                pair.Value.SourceCadIds.ToList(),
                pair.Value.OutputCadIds.ToList());
        }

        _nextCombinedCadGroupId = snapshot.NextCombinedCadGroupId;
        _nextCombinedCadPadId = snapshot.NextCombinedCadPadId;
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState();
        SyncRotatedCadState();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        SyncCombinedCadState();
        SyncHiddenCadIdsToProject();
        SyncDuplicateCadState();
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

        if (_hiddenCadPadIds.Count == 0 && _autoHiddenDuplicateCadPadIds.Count == 0)
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
        if (_hiddenCadUndoStack.Count == 0)
        {
            return;
        }

        var batchesTopToBottom = _hiddenCadUndoStack
            .Select(static batch => batch.ToList())
            .ToList();
        _hiddenCadUndoStack.Clear();

        for (var index = batchesTopToBottom.Count - 1; index >= 0; index--)
        {
            var batch = batchesTopToBottom[index];
            batch.RemoveAll(id => id == cadPadId);
            if (batch.Count > 0)
            {
                _hiddenCadUndoStack.Push(batch);
            }
        }
    }
}
