// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services.CadEditing;

internal sealed partial class CadEditWorkflow
{
    internal void CombineSelectedCadPads()
    {
        var sourcePads = ResolveCombineSources();
        if (sourcePads is null) return;
        var undoSnapshot = document.CaptureUndo();
        var previous = publication.Capture();
        var unionResult = Application.Services.CadPadUnionService.Union(sourcePads.Select(p => p.Polygon));
        if (unionResult.OuterPolygons.Count == 0)
        {
            effects.SetStatus("DXF combine: failed to build combined shape.");
            _editLogger.Warn(CultureInfo.InvariantCulture, "DXF combine failed: union output empty.");
            return;
        }

        var combinedPads = AcceptCombinedPads(sourcePads, unionResult.OuterPolygons);

        publication.Relayered(previous);
        publication.Combined(previous);
        publication.Hidden(previous);
        document.InvalidateFilteredCad();
        RefreshAfterVisibilityEdit();

        effects.SetStatus($"DXF combine: merged {sourcePads.Count} pads into {combinedPads.Count}.");
        _editLogger.Info(CultureInfo.InvariantCulture, "DXF combine applied. source={0}, outputPads={1}, hiddenTotal={2}, combinedTotal={3}, holes={4}.",
            sourcePads.Count,
            combinedPads.Count,
            document.Edit.HiddenCadPadIds.Count,
            document.Combined.OutputCadIds.Count,
            unionResult.HolePathCount);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undoSnapshot), "DXF combine CAD pads");
    }

    private List<CadPad>? ResolveCombineSources()
    {
        if (!TryGetLoadedCad("DXF combine: import DXF first.", out var cad))
        {
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF combine skipped: no DXF loaded.");
            return null;
        }

        var selected = interaction.SelectedCadIds()
            .Where(id => !document.IsEffectivelyHidden(id))
            .Distinct()
            .ToList();
        if (selected.Count < 2)
        {
            effects.SetStatus("DXF combine: select at least 2 CAD pads.");
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF combine skipped: less than 2 pads selected.");
            return null;
        }

        var selectedSet = selected.ToHashSet();
        var sourcePads = cad.Pads
            .Where(p => selectedSet.Contains(p.Id) && !document.IsEffectivelyHidden(p.Id))
            .ToList();
        if (sourcePads.Count < 2)
        {
            effects.SetStatus("DXF combine: select at least 2 CAD output pads.");
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF combine skipped: less than 2 visible source pads.");
            return null;
        }

        return sourcePads;
    }

    internal void ClearCombinedCadPads()
    {
        if (document.Cad is null || document.Combined.OutputCadIds.Count == 0)
        {
            effects.SetStatus("DXF combine: no combined pad.");
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF combine clear skipped: no combined pads.");
            return;
        }

        var selectedGroupIds = interaction.SelectedCadIds()
            .Select(id => document.TryResolveCombinedGroupId(id, out var groupId) ? groupId : 0)
            .Where(static groupId => groupId > 0)
            .Distinct()
            .OrderBy(static groupId => groupId)
            .ToList();
        if (selectedGroupIds.Count == 0)
        {
            effects.SetStatus("DXF combine: select combined CAD pads first.");
            _editLogger.Debug(CultureInfo.InvariantCulture, "DXF combine clear skipped: no combined CAD pad selected.");
            return;
        }

        ClearCombinedCadGroups(
            selectedGroupIds,
            undoLabel: "DXF clear selected combined CAD pads",
            statusPrefix: "DXF combine: cleared selected groups",
            logContext: "DXF combine cleared selected groups");
    }

    internal void ClearCombinedCadGroups(
        List<int> groupIds,
        string undoLabel,
        string statusPrefix,
        string logContext)
    {
        if (document.Cad is null || groupIds.Count == 0)
        {
            return;
        }

        var validGroupIds = groupIds
            .Where(document.Combined.Groups.ContainsKey)
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
        if (validGroupIds.Count == 0)
        {
            return;
        }

        var undoSnapshot = document.CaptureUndo();
        var previous = publication.Capture();
        var (removed, restoredSources) = RemoveCombinedGroups(validGroupIds);

        document.InvalidateFilteredCad();
        publication.Relayered(previous);
        publication.Rotated(previous);
        publication.Combined(previous);
        publication.Hidden(previous);
        RefreshAfterVisibilityEdit();

        PublishClearCombinedStatus(statusPrefix, logContext, validGroupIds.Count, removed, restoredSources);
        effects.MarkUnsaved();
        effects.PushUndo(() => RestoreUndo(undoSnapshot), undoLabel);
    }

    private List<CadPad> AcceptCombinedPads(List<CadPad> sourcePads, IReadOnlyList<Polygon2> polygons)
    {
        var groupId = document.Combined.NextGroupId++;
        var combinedPads = DxfCombinedGroupRules.CreatePads(
            sourcePads[0].Layer, polygons, groupId, document.Cad, document.Combined);

        var sourcePadIds = sourcePads.Select(static p => p.Id).Distinct().ToList();
        document.Combined.Groups[groupId] = new CombinedCadGroupState(
            sourcePadIds,
            combinedPads.Select(static pad => pad.Id).ToList());
        document.Cad = new CadPadSet(document.Cad!.Pads.Concat(combinedPads).ToList());
        foreach (var sourceId in sourcePadIds)
        {
            document.Edit.HiddenCadPadIds.Add(sourceId);
            document.Combined.GroupIdBySourceCadId[sourceId] = groupId;
        }
        return combinedPads;
    }

    private (int Removed, int Restored) RemoveCombinedGroups(List<int> validGroupIds)
    {
        var (outputIds, sourceIds) = DxfCombinedGroupRules.CollectGroupMemberIds(document.Combined, validGroupIds);
        document.Cad = new CadPadSet(document.Cad!.Pads.Where(pad => !outputIds.Contains(pad.Id)).ToList());
        var restoredSources = DxfCombinedGroupRules.RemoveGroupMembers(
            document.Combined, document.Edit.HiddenCadPadIds, outputIds, sourceIds);

        foreach (var groupId in validGroupIds)
        {
            document.Combined.Groups.Remove(groupId);
        }

        if (document.Combined.Groups.Count == 0)
        {
            document.Combined.NextGroupId = 1;
        }
        return (outputIds.Count, restoredSources);
    }

    private void PublishClearCombinedStatus(string statusPrefix, string logContext, int groups, int removed, int restoredSources)
    {
        effects.SetStatus($"{statusPrefix}: groups={groups}, combined={removed}, restored={restoredSources}.");
        _editLogger.Info(
            CultureInfo.InvariantCulture,
            "{0}. groups={1}, combinedRemoved={2}, restoredSources={3}, hiddenTotal={4}.",
            logContext,
            groups,
            removed,
            restoredSources,
            document.Edit.HiddenCadPadIds.Count);
    }
}
