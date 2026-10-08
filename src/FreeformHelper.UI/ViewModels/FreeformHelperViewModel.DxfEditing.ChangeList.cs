using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Icons;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private async Task OpenHiddenDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Hidden);

    private async Task OpenDuplicateDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Duplicate);

    private async Task OpenCombinedDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Combined);

    private async Task OpenMovedDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Moved);

    private async Task ShowDxfEditChangeListAsync(DxfEditChangeKind initialKind)
    {
        if (OpenDxfEditChangeListAsync is null)
        {
            SetStatus("DXF edit details: dialog handler not wired.");
            return;
        }

        var viewModel = new DxfEditChangeListViewModel(
            initialKind,
            BuildDxfEditChangeEntries,
            FocusDxfEditChangeEntry,
            ApplyDxfEditChangeEntry);
        viewModel.Editing.AttachProject(this);
        await OpenDxfEditChangeListAsync(viewModel);
    }

    private List<DxfEditChangeListEntry> BuildDxfEditChangeEntries()
    {
        var entries = new List<DxfEditChangeListEntry>();
        var currentPadsById = _cad?.Pads.ToDictionary(static pad => pad.Id) ?? new Dictionary<int, CadPad>();
        var baselinePadsById = _dxfEditBaselineCad?.Pads.ToDictionary(static pad => pad.Id) ?? new Dictionary<int, CadPad>();

        foreach (var cadPadId in _hiddenCadPadIds.OrderBy(static id => id))
        {
            var pad = currentPadsById.GetValueOrDefault(cadPadId) ?? baselinePadsById.GetValueOrDefault(cadPadId);
            if (pad is null)
            {
                continue;
            }

            if (_combinedCadGroupIdBySourceCadId.TryGetValue(cadPadId, out var combineGroupId))
            {
                var sourceCount = _combinedCadGroups.TryGetValue(combineGroupId, out var group)
                    ? group.SourceCadIds.Count
                    : 0;
                entries.Add(new DxfEditChangeListEntry(
                    DxfEditChangeKind.Hidden,
                    pad.Id,
                    pad.Id,
                    pad.Name,
                    "Hidden by combine",
                    $"Layer {pad.Layer}",
                    $"Group {combineGroupId}",
                    sourceCount > 0
                        ? $"Combine group {combineGroupId} hides this source pad. Source pads: {sourceCount}."
                        : $"Combine group {combineGroupId} hides this source pad.",
                    IconGlyphs.Remove,
                    "Clear group",
                    "Clear this combine group and restore its source pads.",
                    canFocus: false,
                    canApply: true));
                continue;
            }

            entries.Add(new DxfEditChangeListEntry(
                DxfEditChangeKind.Hidden,
                pad.Id,
                pad.Id,
                pad.Name,
                "Hidden",
                $"Layer {pad.Layer}",
                "Manual hide",
                "Hidden manually from the current DXF edit session.",
                IconGlyphs.Undo,
                "Restore",
                "Restore this hidden CAD pad.",
                canFocus: false,
                canApply: true));
        }

        foreach (var duplicateCadId in _duplicateCadPadIdToCanonicalId.Keys.OrderBy(static id => id))
        {
            if (!currentPadsById.TryGetValue(duplicateCadId, out var duplicatePad))
            {
                continue;
            }

            var canonicalCadId = _duplicateCadPadIdToCanonicalId[duplicateCadId];
            var canonicalPad = currentPadsById.GetValueOrDefault(canonicalCadId) ?? baselinePadsById.GetValueOrDefault(canonicalCadId);
            var isAutoHidden = _autoHiddenDuplicateCadPadIds.Contains(duplicateCadId);
            var currentLayerText = canonicalPad is null
                ? $"Layer {duplicatePad.Layer}"
                : $"Layer {duplicatePad.Layer} · Canonical CAD {canonicalCadId}";
            var detailText = canonicalPad is null
                ? "Exact same-layer duplicate pad detected during DXF load."
                : $"Exact same-layer duplicate of CAD {canonicalCadId} ({canonicalPad.Name}).";
            entries.Add(new DxfEditChangeListEntry(
                DxfEditChangeKind.Duplicate,
                duplicateCadId,
                isAutoHidden ? canonicalCadId : duplicateCadId,
                duplicatePad.Name,
                isAutoHidden ? "Auto-hidden duplicate" : "Restored duplicate",
                currentLayerText,
                canonicalPad is null
                    ? "Exact duplicate"
                    : $"Canonical CAD {canonicalCadId}",
                detailText,
                isAutoHidden ? IconGlyphs.Undo : IconGlyphs.VisibilityOff,
                isAutoHidden ? "Restore" : "Hide again",
                isAutoHidden
                    ? "Restore this duplicate CAD pad into the active working set."
                    : "Auto-hide this duplicate CAD pad again.",
                canFocus: true,
                canApply: true));
        }

        foreach (var combinedCadId in _combinedCadPadIds.OrderBy(static id => id))
        {
            if (!currentPadsById.TryGetValue(combinedCadId, out var pad))
            {
                continue;
            }

            var groupId = _combinedCadGroupIdByOutputCadId.GetValueOrDefault(combinedCadId);
            var sourceCount = groupId > 0 && _combinedCadGroups.TryGetValue(groupId, out var group)
                ? group.SourceCadIds.Count
                : 0;
            entries.Add(new DxfEditChangeListEntry(
                DxfEditChangeKind.Combined,
                pad.Id,
                pad.Id,
                pad.Name,
                "Synthetic",
                $"Layer {pad.Layer}",
                sourceCount > 0 ? $"Group {groupId} · {sourceCount} src" : "Synthetic pad",
                sourceCount > 0
                    ? $"Combine group {groupId} output. Source pads: {sourceCount}."
                    : "Synthetic CAD pad generated by combine.",
                IconGlyphs.Remove,
                "Clear group",
                "Clear this combine group and restore its source pads.",
                canFocus: true,
                canApply: groupId > 0));
        }

        foreach (var currentPad in currentPadsById.Values.OrderBy(static pad => pad.Id))
        {
            if (!baselinePadsById.TryGetValue(currentPad.Id, out var baselinePad) ||
                string.Equals(baselinePad.Layer, currentPad.Layer, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(new DxfEditChangeListEntry(
                DxfEditChangeKind.Moved,
                currentPad.Id,
                currentPad.Id,
                currentPad.Name,
                "Moved",
                $"Layer {currentPad.Layer}",
                $"{baselinePad.Layer} -> {currentPad.Layer}",
                $"Original layer: {baselinePad.Layer} -> Current layer: {currentPad.Layer}.",
                IconGlyphs.Undo,
                "Restore layer",
                $"Move this CAD pad back to layer '{baselinePad.Layer}'.",
                canFocus: !IsCadPadEffectivelyHidden(currentPad.Id),
                canApply: true));
        }

        foreach (var currentPad in currentPadsById.Values.OrderBy(static pad => pad.Id))
        {
            if (!baselinePadsById.TryGetValue(currentPad.Id, out var baselinePad) ||
                ArePolygonVerticesEquivalent(baselinePad.Polygon, currentPad.Polygon))
            {
                continue;
            }

            entries.Add(new DxfEditChangeListEntry(
                DxfEditChangeKind.Rotated,
                currentPad.Id,
                currentPad.Id,
                currentPad.Name,
                "Rotated",
                $"Layer {currentPad.Layer}",
                "Geometry differs from DXF baseline",
                "Polygon vertices were rotated relative to the imported DXF baseline. Restore resets this pad geometry only.",
                IconGlyphs.Undo,
                "Restore geometry",
                "Restore this CAD pad geometry back to DXF baseline.",
                canFocus: !IsCadPadEffectivelyHidden(currentPad.Id),
                canApply: true));
        }

        return entries;
    }

    private void FocusDxfEditChangeEntry(DxfEditChangeListEntry entry)
    {
        var focusCadPadId = entry.FocusCadPadId ?? entry.CadPadId;
        TryLocateSelection(
            new[] { focusCadPadId },
            Array.Empty<int>(),
            focusSelection: true,
            notVisibleStatus: $"CAD {focusCadPadId} is not visible.",
            successStatusBuilder: (cadCount, _) =>
                $"DXF edit focused: CAD {focusCadPadId} (CAD output={cadCount}).");
    }

    private void ApplyDxfEditChangeEntry(DxfEditChangeListEntry entry)
    {
        switch (entry.Kind)
        {
            case DxfEditChangeKind.Hidden:
                if (_combinedCadGroupIdBySourceCadId.ContainsKey(entry.CadPadId))
                {
                    ClearCombinedCadGroupForPad(entry.CadPadId);
                }
                else
                {
                    RestoreHiddenCadPad(entry.CadPadId);
                }

                break;
            case DxfEditChangeKind.Duplicate:
                if (_autoHiddenDuplicateCadPadIds.Contains(entry.CadPadId))
                {
                    RestoreDuplicateCadPad(entry.CadPadId);
                }
                else
                {
                    RehideDuplicateCadPad(entry.CadPadId);
                }

                break;
            case DxfEditChangeKind.Combined:
                ClearCombinedCadGroupForPad(entry.CadPadId);
                break;
            case DxfEditChangeKind.Moved:
                RestoreMovedCadPadLayer(entry.CadPadId);
                break;
            case DxfEditChangeKind.Rotated:
                RestoreRotatedCadPadGeometry(entry.CadPadId);
                break;
        }
    }

    private void RestoreHiddenCadPad(int cadPadId)
    {
        if (_cad is null || !_hiddenCadPadIds.Contains(cadPadId))
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

    private void ClearCombinedCadGroupForPad(int cadPadId)
    {
        if (!TryResolveCombinedCadGroupId(cadPadId, out var groupId) || _cad is null)
        {
            return;
        }

        ClearCombinedCadGroups(
            [groupId],
            undoLabel: "DXF clear combined group",
            statusPrefix: $"DXF combine: cleared group {groupId}",
            logContext: "DXF combine cleared group");
    }

    private void RestoreMovedCadPadLayer(int cadPadId)
    {
        if (_cad is null || _dxfEditBaselineCad is null)
        {
            return;
        }

        var baselinePad = _dxfEditBaselineCad.Pads.FirstOrDefault(pad => pad.Id == cadPadId);
        if (baselinePad is null)
        {
            return;
        }

        var changedCount = 0;
        var updatedPads = new List<CadPad>(_cad.Pads.Count);
        foreach (var pad in _cad.Pads)
        {
            if (pad.Id != cadPadId)
            {
                updatedPads.Add(pad);
                continue;
            }

            if (string.Equals(pad.Layer, baselinePad.Layer, StringComparison.OrdinalIgnoreCase))
            {
                updatedPads.Add(pad);
                continue;
            }

            updatedPads.Add(new CadPad(pad.Id, pad.Name, baselinePad.Layer, pad.Polygon));
            changedCount++;
        }

        if (changedCount == 0)
        {
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        var layerSelectionSnapshot = CaptureLayerSelectionSnapshot();
        _cad = new CadPadSet(updatedPads);
        _cachedFilteredCadForBuild = null;
        PreserveDxfDependentLayerSelections();
        UpdateLayerToggles(_cad);
        RestoreLayerSelectionAfterCadLayerEdit(layerSelectionSnapshot, baselinePad.Layer);
        SyncRelayeredCadState();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        SetStatus($"DXF edit: restored CAD {cadPadId} to layer '{baselinePad.Layer}'.");
        Logger.Info(
            CultureInfo.InvariantCulture,
            "DXF layer restore applied. cadId={0}, layer={1}.",
            cadPadId,
            baselinePad.Layer);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF restore CAD layer");
    }

    private bool TryResolveCombinedCadGroupId(int cadPadId, out int groupId)
    {
        if (_combinedCadGroupIdByOutputCadId.TryGetValue(cadPadId, out groupId))
        {
            return true;
        }

        return _combinedCadGroupIdBySourceCadId.TryGetValue(cadPadId, out groupId);
    }
}
