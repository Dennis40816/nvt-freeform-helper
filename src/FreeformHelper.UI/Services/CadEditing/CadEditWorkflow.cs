// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.CadEditing;
using NLog;

namespace FreeformHelper.UI.Services.CadEditing;

// UI-thread orchestration. The ports retain presentation order without owning presentation state.
internal sealed class CadEditWorkflow(
    CadDocumentSession document, CadEditPublication publication, CadEditLayers layers, CadEditEffects effects)
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    internal void RestoreUndo(CadEditUndoSnapshot snapshot)
    {
        var previous = publication.Capture();
        document.RestoreUndo(snapshot);
        publication.Relayered(previous);
        publication.Rotated(previous);
        effects.ClosePadInfo();
        effects.ClearSelection();
        publication.Combined(previous);
        publication.Hidden(previous);
        publication.Duplicate(previous, true);
        RestoreUndoLayers();
        effects.RebuildUnlessLayerFilterDoes();
        effects.MarkUnsaved();
    }

    private void RestoreUndoLayers()
    {
        if (document.Cad is not null && !layers.MatchesCurrentLayers(document.Cad))
        {
            var selection = layers.CaptureSelection();
            layers.PreserveDependentSelections();
            layers.Update(document.Cad);
            layers.RestoreSelection(selection, string.Empty);
        }
        else
        {
            effects.Filter();
        }
    }

    internal void ClearRuntime(bool resetBaseline, CadEditNotificationSnapshot previous)
    {
        document.ClearRuntimeEdits();
        if (resetBaseline)
        {
            ResetBaseline(document.Cad, previous);
        }
    }

    internal void ResetBaseline(CadPadSet? cad, CadEditNotificationSnapshot previous)
    {
        document.ResetBaseline(cad);
        publication.Relayered(previous);
        publication.Rotated(previous);
    }

    internal void SetHiddenPads(IEnumerable<int> ids, CadEditNotificationSnapshot? previous = null)
    {
        previous ??= publication.Capture();
        document.SetHiddenPads(ids);
        publication.Hidden(previous);
        publication.Combined(previous);
        if (document.Cad is not null)
        {
            _logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads set from project. Hidden total={0}.", document.Edit.HiddenCadPadIds.Count);
        }
    }

    internal void ResetHiddenPads(CadEditNotificationSnapshot previous)
    {
        ClearRuntime(resetBaseline: true, previous);
        document.ClearAutoHiddenDuplicates();
        publication.Hidden(previous);
        publication.Combined(previous);
        _logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads reset.");
    }

    internal void SetAutoHiddenDuplicates(CadPadExactDuplicateSanitizationResult sanitization)
    {
        document.SetAutoHiddenDuplicates(sanitization);
        if (document.Edit.AutoHiddenDuplicateCadPadIds.Count > 0)
        {
            _logger.Debug(
                CultureInfo.InvariantCulture,
                "DXF duplicate pads auto-hidden. total={0}, ids={1}.",
                document.Edit.AutoHiddenDuplicateCadPadIds.Count,
                string.Join(", ", document.Edit.AutoHiddenDuplicateCadPadIds.OrderBy(static id => id)));
        }
    }

    internal void ApplyVisibleDuplicates(IEnumerable<int> ids)
    {
        if (document.ApplyVisibleDuplicates(ids))
        {
            publication.Duplicate(null, true);
        }
    }

    internal void Rotate(CadPadSet cad, HashSet<int> selectedIds, double rotationDegrees)
    {
        var undo = document.CaptureUndo();
        var result = CadPadRotationService.Rotate(cad, selectedIds, rotationDegrees);
        if (result.ChangedCount == 0)
        {
            effects.SetStatus("DXF rotate: no CAD pad rotated.");
            return;
        }

        AcceptGeometry(result.Cad);
        effects.SetStatus($"DXF rotate: rotated {result.ChangedCount} CAD pad(s) by {result.AppliedDegrees:0.###}°.");
        _logger.Info(CultureInfo.InvariantCulture, "DXF rotate applied. changedPads={0}, degrees={1:0.###}.",
            result.ChangedCount, result.AppliedDegrees);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undo), "DXF rotate CAD pads");
    }

    internal void RestoreGeometry(int cadPadId)
    {
        var baseline = document.Edit.BaselineCad?.Pads.FirstOrDefault(pad => pad.Id == cadPadId);
        if (document.Cad is null || baseline is null)
        {
            return;
        }

        var result = CadEditTransforms.RestoreGeometry(document.Cad, baseline);
        if (result.ChangedCount == 0)
        {
            return;
        }

        var undo = document.CaptureUndo();
        AcceptGeometry(result.Cad);
        effects.SetStatus($"DXF edit: restored CAD {cadPadId} geometry.");
        _logger.Info(CultureInfo.InvariantCulture, "DXF geometry restore applied. cadId={0}.", cadPadId);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undo), "DXF restore CAD geometry");
    }

    private void AcceptGeometry(CadPadSet cad)
    {
        document.Cad = cad;
        document.InvalidateFilteredCad();
        publication.Relayered(null);
        publication.Rotated(null);
        effects.ClosePadInfo();
        effects.ClearSelection();
        effects.Filter();
        effects.RebuildUnlessLayerFilterDoes();
    }

    internal void MoveToLayer(CadPadSet cad, HashSet<int> selectedIds, string targetLayer, string operationName)
    {
        var result = CadEditTransforms.MoveToLayer(cad, selectedIds, targetLayer);
        if (result.ChangedCount == 0)
        {
            effects.SetStatus($"DXF edit: selected pads are already in layer '{targetLayer}'.");
            return;
        }

        var undo = document.CaptureUndo();
        AcceptLayerChange(result.Cad, targetLayer);
        layers.SelectTargetLayer(targetLayer);
        publication.Relayered(null);
        effects.ClosePadInfo();
        effects.ClearSelection();
        effects.SetStatus($"DXF edit: moved {result.ChangedCount} CAD pad(s) to layer '{targetLayer}'.");
        _logger.Info(CultureInfo.InvariantCulture, "DXF layer move applied. op={0}, targetLayer={1}, changedPads={2}.",
            operationName, targetLayer, result.ChangedCount);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undo), "DXF move CAD pads to layer");
    }

    internal void RestoreLayer(int cadPadId)
    {
        var baseline = document.Edit.BaselineCad?.Pads.FirstOrDefault(pad => pad.Id == cadPadId);
        if (document.Cad is null || baseline is null)
        {
            return;
        }

        var result = CadEditTransforms.MoveToLayer(document.Cad, [cadPadId], baseline.Layer);
        if (result.ChangedCount == 0)
        {
            return;
        }

        var undo = document.CaptureUndo();
        AcceptLayerChange(result.Cad, baseline.Layer);
        publication.Relayered(null);
        effects.ClosePadInfo();
        effects.ClearSelection();
        effects.SetStatus($"DXF edit: restored CAD {cadPadId} to layer '{baseline.Layer}'.");
        _logger.Info(CultureInfo.InvariantCulture, "DXF layer restore applied. cadId={0}, layer={1}.", cadPadId, baseline.Layer);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undo), "DXF restore CAD layer");
    }

    private void AcceptLayerChange(CadPadSet cad, string visibleLayer)
    {
        var selection = layers.CaptureSelection();
        document.Cad = cad;
        document.InvalidateFilteredCad();
        layers.PreserveDependentSelections();
        layers.Update(cad);
        layers.RestoreSelection(selection, visibleLayer);
    }
}
