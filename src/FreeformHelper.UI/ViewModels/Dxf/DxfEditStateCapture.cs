// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfEditStateCapture
{
    internal static Dictionary<int, string> CapturePersistedCadLayerOverrides(CadPadSet? cad, CadPadSet? baselineCad)
    {
        if (cad is null || baselineCad is null)
        {
            return new Dictionary<int, string>();
        }

        var baselineLayersByCadId = baselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First().Layer);

        return cad.Pads
            .Where(pad =>
                baselineLayersByCadId.TryGetValue(pad.Id, out var baselineLayer) &&
                !string.Equals(baselineLayer, pad.Layer, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(static pad => pad.Id, static pad => pad.Layer);
    }

    internal static List<ProjectDxfCombinedCadGroup> CapturePersistedCombinedCadGroups(
        CadPadSet? cad, CombinedCadState combinedCad, IReadOnlyList<ProjectDxfCombinedCadGroup> unrestoredGroups)
    {
        var groups = new List<ProjectDxfCombinedCadGroup>();
        if (cad is not null && combinedCad.Groups.Count > 0)
        {
            AddCurrentCombinedCadGroups(groups, cad, combinedCad);
        }

        AppendUnrestoredCombinedCadGroups(groups, unrestoredGroups);
        return groups;
    }

    private static void AppendUnrestoredCombinedCadGroups(
        List<ProjectDxfCombinedCadGroup> groups, IReadOnlyList<ProjectDxfCombinedCadGroup> unrestoredGroups)
    {
        var usedGroupIds = groups.Select(static group => group.GroupId).ToHashSet();
        var nextGroupId = usedGroupIds.Count == 0 ? 1 : usedGroupIds.Max() + 1;
        foreach (var kept in unrestoredGroups)
        {
            var groupId = kept.GroupId;
            if (groupId > 0 && !usedGroupIds.Add(groupId))
            {
                // A group made after a DXF reload may reuse this id. Give the kept group a free one.
                groupId = nextGroupId;
                usedGroupIds.Add(groupId);
            }

            nextGroupId = Math.Max(nextGroupId, groupId + 1);
            groups.Add(new ProjectDxfCombinedCadGroup
            {
                GroupId = groupId,
                SourceCadIds = kept.SourceCadIds,
                OutputPads = kept.OutputPads,
            });
        }
    }

    private static void AddCurrentCombinedCadGroups(
        List<ProjectDxfCombinedCadGroup> groups, CadPadSet cad, CombinedCadState combinedCad)
    {
        var currentPadsById = cad.Pads.ToDictionary(static pad => pad.Id);
        foreach (var pair in combinedCad.Groups.OrderBy(static pair => pair.Key))
        {
            var outputPads = pair.Value.OutputCadIds
                .Select(outputCadId => currentPadsById.TryGetValue(outputCadId, out var pad)
                    ? ProjectCadPadSnapshot.FromCadPad(pad)
                    : null)
                .Where(static pad => pad is not null)
                .Cast<ProjectCadPadSnapshot>()
                .ToList();
            if (pair.Value.SourceCadIds.Count == 0 || outputPads.Count == 0)
            {
                continue;
            }

            groups.Add(new ProjectDxfCombinedCadGroup
            {
                GroupId = pair.Key,
                SourceCadIds = pair.Value.SourceCadIds.Distinct().OrderBy(static id => id).ToList(),
                OutputPads = outputPads,
            });
        }
    }

    internal static Dictionary<int, ProjectCadGeometrySnapshot> CapturePersistedCadGeometryOverrides(
        CadPadSet? cad, CadPadSet? baselineCad)
    {
        if (cad is null || baselineCad is null)
        {
            return new Dictionary<int, ProjectCadGeometrySnapshot>();
        }

        var baselinePadsByCadId = baselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        return cad.Pads
            .Where(pad =>
                baselinePadsByCadId.TryGetValue(pad.Id, out var baselinePad) &&
                !DxfEditRules.ArePolygonVerticesEquivalent(baselinePad.Polygon, pad.Polygon))
            .ToDictionary(static pad => pad.Id, ProjectCadGeometrySnapshot.FromCadPad);
    }
}
