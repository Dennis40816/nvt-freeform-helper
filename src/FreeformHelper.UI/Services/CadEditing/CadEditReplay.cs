// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.UseCases.ProjectLoad;
using NLog;

namespace FreeformHelper.UI.Services.CadEditing;

// UI-thread project replay. Baseline installation remains before this ordered edit replay.
internal sealed class CadEditReplay(
    CadDocumentSession document, CadEditWorkflow workflow, CadEditPublication publication, CadEditLayers layers)
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    internal DxfCombinedGroupReplayResult Apply(PersistedDxfEditProjectState state, string? cadFallbackWarning = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var previous = publication.Capture();
        if (document.Cad is null)
        {
            return ClearWithoutCad(state, previous);
        }

        var selection = layers.CaptureSelection();
        layers.PreserveDependentSelections();
        workflow.ClearRuntime(resetBaseline: false, previous);
        ApplyLayerOverrides(state.CadLayerOverrides);
        ApplyGeometryOverrides(state.CadGeometryOverrides);
        var replay = ReplayGroups(state.CombinedCadGroups);
        replay.CadFallbackWarning = cadFallbackWarning;
        document.Edit.UnrestoredCombinedGroups = [.. replay.SkippedGroups];
        workflow.SetHiddenPads(state.HiddenCadPadIds.Concat(replay.CombinedSourceCadIds), previous);
        workflow.ApplyVisibleDuplicates(state.VisibleDuplicateCadPadIds);
        document.Edit.HiddenUndoStack.Clear();
        document.InvalidateFilteredCad();
        publication.Relayered(previous);
        publication.Rotated(previous);
        publication.Combined(previous);
        layers.Update(document.Cad);
        layers.RestoreSelection(selection, string.Empty);
        return replay;
    }

    private DxfCombinedGroupReplayResult ClearWithoutCad(PersistedDxfEditProjectState state, CadEditNotificationSnapshot previous)
    {
        workflow.ClearRuntime(resetBaseline: false, previous);
        document.ClearAutoHiddenDuplicates();
        publication.Relayered(previous);
        publication.Rotated(previous);
        publication.Hidden(previous);
        publication.Combined(previous);
        publication.Duplicate(previous, false);
        var replay = new DxfCombinedGroupReplayResult();
        replay.SkippedGroups.AddRange(state.CombinedCadGroups);
        document.Edit.UnrestoredCombinedGroups = [.. state.CombinedCadGroups];
        return replay;
    }

    internal DxfCombinedGroupReplayResult KeepWithoutCad(ProjectFile file)
    {
        var replay = ClearWithoutCad(DxfEditStateCapture.CaptureProjectState(file), publication.Capture());
        if (!string.IsNullOrWhiteSpace(file.LastDxfPath))
        {
            replay.CadFallbackWarning =
                $"Warning: The DXF file was not found: {file.LastDxfPath}. The saved DXF edits are kept in the project.";
        }

        return replay;
    }

    private void ApplyLayerOverrides(Dictionary<int, string> overrides)
    {
        if (document.Cad is not null && overrides.Count > 0)
        {
            document.Cad = CadEditTransforms.ApplyLayerOverrides(document.Cad, overrides);
        }
    }

    private void ApplyGeometryOverrides(Dictionary<int, ProjectCadGeometrySnapshot> overrides)
    {
        if (document.Cad is null || overrides.Count == 0)
        {
            return;
        }

        document.Cad = CadEditTransforms.ApplyGeometryOverrides(document.Cad, id =>
            overrides.TryGetValue(id, out var geometry) && geometry.Vertices.Count >= 3 ? geometry.ToPolygon() : null);
    }

    private DxfCombinedGroupReplayResult ReplayGroups(List<ProjectDxfCombinedCadGroup> groups)
    {
        var replay = new DxfCombinedGroupReplayResult();
        if (document.Cad is null || groups.Count == 0)
        {
            return replay;
        }

        var pads = document.Cad.Pads.ToList();
        var ids = pads.Select(static pad => pad.Id).ToHashSet();
        var maxGroupId = 0;
        var nextPadId = document.Combined.NextPadId;
        foreach (var group in groups.OrderBy(static group => group.GroupId))
        {
            maxGroupId = Math.Max(maxGroupId, group.GroupId);
            if (!TryRestoreGroup(group, pads, ids, replay, ref nextPadId, out var groupId))
            {
                replay.SkippedGroups.Add(group);
                continue;
            }

            maxGroupId = Math.Max(maxGroupId, groupId);
        }

        document.Combined.NextGroupId = Math.Max(document.Combined.NextGroupId, maxGroupId + 1);
        document.Combined.NextPadId = nextPadId;
        document.Cad = new CadPadSet(pads);
        return replay;
    }

    private bool TryRestoreGroup(
        ProjectDxfCombinedCadGroup group, List<CadPad> pads, HashSet<int> ids,
        DxfCombinedGroupReplayResult replay, ref int nextPadId, out int groupId)
    {
        groupId = 0;
        if (group.SourceCadIds.Count == 0 || group.OutputPads.Count == 0)
        {
            return false;
        }

        var sourceIds = group.SourceCadIds.Distinct().OrderBy(static id => id).ToList();
        if (sourceIds.Any(id => !ids.Contains(id)))
        {
            _logger.Warn(CultureInfo.InvariantCulture,
                "DXF edit replay skipped combined group {0}: source pad missing in baseline.", group.GroupId);
            return false;
        }

        if (!TryBuildOutputs(group, ids, ref nextPadId, out var outputs))
        {
            return false;
        }

        groupId = group.GroupId > 0 ? group.GroupId : document.Combined.NextGroupId++;
        pads.AddRange(outputs);
        document.Combined.Groups[groupId] = new CombinedCadGroupState(sourceIds, outputs.Select(static pad => pad.Id).ToList());
        foreach (var output in outputs)
        {
            document.Combined.GroupIdByOutputCadId[output.Id] = groupId;
        }

        foreach (var sourceId in sourceIds)
        {
            replay.CombinedSourceCadIds.Add(sourceId);
            document.Combined.GroupIdBySourceCadId[sourceId] = groupId;
        }

        return true;
    }

    private static bool TryBuildOutputs(
        ProjectDxfCombinedCadGroup group, HashSet<int> ids, ref int nextPadId, out List<CadPad> outputs)
    {
        outputs = new List<CadPad>(group.OutputPads.Count);
        foreach (var snapshot in group.OutputPads)
        {
            if (!TryReadOutput(snapshot, group.GroupId, ids, out var pad))
            {
                foreach (var accepted in outputs)
                {
                    ids.Remove(accepted.Id);
                }

                // Keep the characterized allocator advance even when accepted outputs are rolled back.
                return false;
            }

            outputs.Add(pad);
            nextPadId = Math.Max(nextPadId, pad.Id + 1);
        }

        return outputs.Count > 0;
    }

    private static bool TryReadOutput(ProjectCadPadSnapshot snapshot, int groupId, HashSet<int> ids, [NotNullWhen(true)] out CadPad? pad)
    {
        try
        {
            pad = snapshot.ToCadPad();
        }
        catch (ArgumentException ex)
        {
            _logger.Warn(ex, "DXF edit replay skipped combined group {0}: output pad snapshot invalid.", groupId);
            pad = null;
            return false;
        }

        if (ids.Add(pad.Id))
        {
            return true;
        }

        _logger.Warn(CultureInfo.InvariantCulture,
            "DXF edit replay skipped combined group {0}: output pad id {1} conflicts.", groupId, pad.Id);
        return false;
    }
}
