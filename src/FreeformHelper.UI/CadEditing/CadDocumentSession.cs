// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.CadEditing;

// One workspace's accepted document and edit history. All mutation is UI-thread only.
// Cad and BaselineCad are distinct versions, not competing copies of the current document.
internal sealed class CadDocumentSession
{
    internal CadPadSet? Cad;

    internal CadEditState Edit { get; } = new();

    internal CombinedCadState Combined { get; } = new();

    // Preserve the existing acceptance points: indexing may publish an ordered projection.
    internal CadPadSet? FilteredCad { get; private set; }

    // Counts are accepted at separate notification phases, never recomputed by binding reads.
    internal CadEditDelta EditDelta { get; set; } = new(0, 0);

    internal void InvalidateFilteredCad() => FilteredCad = null;

    internal void PublishFilteredCad(CadPadSet cad) => FilteredCad = cad;

    internal void ResetBaseline(CadPadSet? cad)
    {
        Edit.BaselineCad = cad is null ? null : new CadPadSet(cad.Pads.ToList());
    }

    internal void ClearRuntimeEdits()
    {
        Edit.HiddenCadPadIds.Clear();
        Edit.HiddenUndoStack.Clear();
        Combined.Clear();
    }

    internal bool IsEffectivelyHidden(int cadPadId) =>
        Edit.HiddenCadPadIds.Contains(cadPadId) || Edit.AutoHiddenDuplicateCadPadIds.Contains(cadPadId);

    internal HashSet<int> BuildEffectiveHiddenIds()
    {
        var hidden = Edit.HiddenCadPadIds.ToHashSet();
        hidden.UnionWith(Edit.AutoHiddenDuplicateCadPadIds);
        return hidden;
    }

    internal CadPadSet? BuildActiveCad()
    {
        if (Cad is null || (Edit.HiddenCadPadIds.Count == 0 && Edit.AutoHiddenDuplicateCadPadIds.Count == 0))
        {
            return Cad;
        }

        return new CadPadSet(Cad.Pads.Where(pad => !IsEffectivelyHidden(pad.Id)).ToList());
    }

    internal void ClearAutoHiddenDuplicates()
    {
        Edit.AutoHiddenDuplicateCadPadIds.Clear();
        Edit.DuplicateCadPadIdToCanonicalId.Clear();
    }

    internal void SetAutoHiddenDuplicates(CadPadExactDuplicateSanitizationResult sanitization)
    {
        ArgumentNullException.ThrowIfNull(sanitization);
        ClearAutoHiddenDuplicates();
        if (Cad is null)
        {
            return;
        }

        var validIds = Cad.Pads.Select(static pad => pad.Id).ToHashSet();
        Edit.AutoHiddenDuplicateCadPadIds.UnionWith(sanitization.ExcludedPadIds.Where(validIds.Contains));
        foreach (var pair in sanitization.DuplicateToCanonicalPadId)
        {
            if (Edit.AutoHiddenDuplicateCadPadIds.Contains(pair.Key) && validIds.Contains(pair.Value))
            {
                Edit.DuplicateCadPadIdToCanonicalId[pair.Key] = pair.Value;
            }
        }
    }

    internal HashSet<int> GetRestoredDuplicateIds()
    {
        var restored = Edit.DuplicateCadPadIdToCanonicalId.Keys.ToHashSet();
        restored.ExceptWith(Edit.AutoHiddenDuplicateCadPadIds);
        return restored;
    }

    internal void SetHiddenPads(IEnumerable<int> hiddenIds)
    {
        Edit.HiddenCadPadIds.Clear();
        if (Cad is not null)
        {
            var validIds = Cad.Pads.Select(static pad => pad.Id).ToHashSet();
            Edit.HiddenCadPadIds.UnionWith(hiddenIds.Where(validIds.Contains));
        }
    }

    internal bool ApplyVisibleDuplicates(IEnumerable<int> duplicateIds)
    {
        if (Cad is null)
        {
            return false;
        }

        var ids = duplicateIds.Where(Edit.DuplicateCadPadIdToCanonicalId.ContainsKey).Distinct().ToList();
        Edit.AutoHiddenDuplicateCadPadIds.ExceptWith(ids);
        // Even already-visible duplicates retain the original publication phase.
        return ids.Count > 0;
    }

    internal CadEditUndoSnapshot CaptureUndo() => new(
        Cad,
        Edit.HiddenCadPadIds.ToList(),
        Edit.AutoHiddenDuplicateCadPadIds.ToList(),
        Edit.HiddenUndoStack.Select(static batch => batch.ToList()).ToList(),
        Edit.DuplicateCadPadIdToCanonicalId.ToDictionary(static pair => pair.Key, static pair => pair.Value),
        Combined.Clone());

    internal void RestoreUndo(CadEditUndoSnapshot snapshot)
    {
        Cad = snapshot.Cad;
        Edit.HiddenCadPadIds.Clear();
        Edit.HiddenCadPadIds.UnionWith(snapshot.HiddenCadIds);
        Edit.AutoHiddenDuplicateCadPadIds.Clear();
        Edit.AutoHiddenDuplicateCadPadIds.UnionWith(snapshot.AutoHiddenDuplicateCadIds);
        Edit.HiddenUndoStack.Clear();
        for (var index = snapshot.HiddenUndoBatchesTopToBottom.Count - 1; index >= 0; index--)
        {
            Edit.HiddenUndoStack.Push(snapshot.HiddenUndoBatchesTopToBottom[index].ToList());
        }

        Edit.DuplicateCadPadIdToCanonicalId.Clear();
        foreach (var pair in snapshot.DuplicateCadPadIdToCanonicalId)
        {
            Edit.DuplicateCadPadIdToCanonicalId[pair.Key] = pair.Value;
        }

        Combined.CopyFrom(snapshot.Combined);
        InvalidateFilteredCad();
    }

    internal void RemoveHiddenIdFromUndoBatches(int cadPadId)
    {
        if (Edit.HiddenUndoStack.Count == 0)
        {
            return;
        }

        var batches = Edit.HiddenUndoStack.Select(static batch => batch.ToList()).ToList();
        Edit.HiddenUndoStack.Clear();
        for (var index = batches.Count - 1; index >= 0; index--)
        {
            var batch = batches[index];
            batch.RemoveAll(id => id == cadPadId);
            if (batch.Count > 0)
            {
                Edit.HiddenUndoStack.Push(batch);
            }
        }
    }
}
