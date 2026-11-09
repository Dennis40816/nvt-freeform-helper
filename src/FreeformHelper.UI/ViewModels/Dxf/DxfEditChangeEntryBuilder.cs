// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Icons;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfEditChangeEntryBuilder
{
    internal static List<DxfEditChangeListEntry> Build(
        CadPadSet? currentCad,
        CadEditState cadEdit,
        CombinedCadState combinedCad,
        Func<int, bool> isEffectivelyHidden,
        Func<Polygon2, Polygon2, bool> arePolygonsEquivalent)
    {
        var entries = new List<DxfEditChangeListEntry>();
        var currentPadsById = currentCad?.Pads.ToDictionary(static pad => pad.Id) ?? new Dictionary<int, CadPad>();
        var baselinePadsById = cadEdit.BaselineCad?.Pads.ToDictionary(static pad => pad.Id) ?? new Dictionary<int, CadPad>();

        AppendHiddenDxfEditChangeEntries(entries, currentPadsById, baselinePadsById, cadEdit, combinedCad);
        AppendDuplicateDxfEditChangeEntries(entries, currentPadsById, baselinePadsById, cadEdit);
        AppendCombinedDxfEditChangeEntries(entries, currentPadsById, combinedCad);
        AppendMovedDxfEditChangeEntries(entries, currentPadsById, baselinePadsById, isEffectivelyHidden);
        AppendRotatedDxfEditChangeEntries(entries, currentPadsById, baselinePadsById, isEffectivelyHidden, arePolygonsEquivalent);
        return entries;
    }

    private static void AppendHiddenDxfEditChangeEntries(
        List<DxfEditChangeListEntry> entries,
        Dictionary<int, CadPad> currentPadsById,
        Dictionary<int, CadPad> baselinePadsById,
        CadEditState cadEdit,
        CombinedCadState combinedCad)
    {
        foreach (var cadPadId in cadEdit.HiddenCadPadIds.OrderBy(static id => id))
        {
            var pad = currentPadsById.GetValueOrDefault(cadPadId) ?? baselinePadsById.GetValueOrDefault(cadPadId);
            if (pad is null)
            {
                continue;
            }

            if (combinedCad.GroupIdBySourceCadId.TryGetValue(cadPadId, out var combineGroupId))
            {
                var sourceCount = combinedCad.Groups.TryGetValue(combineGroupId, out var group)
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
    }

    private static void AppendDuplicateDxfEditChangeEntries(
        List<DxfEditChangeListEntry> entries,
        Dictionary<int, CadPad> currentPadsById,
        Dictionary<int, CadPad> baselinePadsById,
        CadEditState cadEdit)
    {
        foreach (var duplicateCadId in cadEdit.DuplicateCadPadIdToCanonicalId.Keys.OrderBy(static id => id))
        {
            if (!currentPadsById.TryGetValue(duplicateCadId, out var duplicatePad))
            {
                continue;
            }

            var canonicalCadId = cadEdit.DuplicateCadPadIdToCanonicalId[duplicateCadId];
            var canonicalPad = currentPadsById.GetValueOrDefault(canonicalCadId) ?? baselinePadsById.GetValueOrDefault(canonicalCadId);
            var isAutoHidden = cadEdit.AutoHiddenDuplicateCadPadIds.Contains(duplicateCadId);
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
    }

    private static void AppendCombinedDxfEditChangeEntries(
        List<DxfEditChangeListEntry> entries,
        Dictionary<int, CadPad> currentPadsById,
        CombinedCadState combinedCad)
    {
        foreach (var combinedCadId in combinedCad.OutputCadIds.OrderBy(static id => id))
        {
            if (!currentPadsById.TryGetValue(combinedCadId, out var pad))
            {
                continue;
            }

            var groupId = combinedCad.GroupIdByOutputCadId.GetValueOrDefault(combinedCadId);
            var sourceCount = groupId > 0 && combinedCad.Groups.TryGetValue(groupId, out var group)
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
    }

    private static void AppendMovedDxfEditChangeEntries(
        List<DxfEditChangeListEntry> entries,
        Dictionary<int, CadPad> currentPadsById,
        Dictionary<int, CadPad> baselinePadsById,
        Func<int, bool> isEffectivelyHidden)
    {
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
                canFocus: !isEffectivelyHidden(currentPad.Id),
                canApply: true));
        }
    }

    private static void AppendRotatedDxfEditChangeEntries(
        List<DxfEditChangeListEntry> entries,
        Dictionary<int, CadPad> currentPadsById,
        Dictionary<int, CadPad> baselinePadsById,
        Func<int, bool> isEffectivelyHidden,
        Func<Polygon2, Polygon2, bool> arePolygonsEquivalent)
    {
        foreach (var currentPad in currentPadsById.Values.OrderBy(static pad => pad.Id))
        {
            if (!baselinePadsById.TryGetValue(currentPad.Id, out var baselinePad) ||
                arePolygonsEquivalent(baselinePad.Polygon, currentPad.Polygon))
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
                canFocus: !isEffectivelyHidden(currentPad.Id),
                canApply: true));
        }
    }
}
