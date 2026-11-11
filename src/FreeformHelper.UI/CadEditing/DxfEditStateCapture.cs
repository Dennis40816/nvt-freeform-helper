// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.CadEditing;

internal static class DxfEditStateCapture
{
    internal static void Capture(CadDocumentSession document, ProjectFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (document.Cad is null)
        {
            // Without a CAD source, live state cannot invalidate the saved DXF edits.
            return;
        }

        var validIds = document.Cad.Pads.Select(static pad => pad.Id).ToHashSet();
        file.HiddenCadPadIds = document.Edit.HiddenCadPadIds.Where(validIds.Contains).ToHashSet();
        file.VisibleDuplicateCadPadIds = document.GetRestoredDuplicateIds().Where(validIds.Contains).ToHashSet();
        file.DxfCadLayerOverrides = CapturePersistedCadLayerOverrides(document.Cad, document.Edit.BaselineCad);
        file.DxfCadGeometryOverrides = CapturePersistedCadGeometryOverrides(document.Cad, document.Edit.BaselineCad);
        file.DxfCombinedCadGroups = CapturePersistedCombinedCadGroups(document.Cad, document.Combined, document.Edit.UnrestoredCombinedGroups);
    }

    internal static PersistedDxfEditProjectState CaptureProjectState(ProjectFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new PersistedDxfEditProjectState(
            file.HiddenCadPadIds.ToHashSet(),
            file.VisibleDuplicateCadPadIds.ToHashSet(),
            file.DxfCadLayerOverrides.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            file.DxfCadGeometryOverrides.ToDictionary(
                static pair => pair.Key,
                static pair => new ProjectCadGeometrySnapshot { Vertices = pair.Value.Vertices.ToList() }),
            file.DxfCombinedCadGroups.Select(CloneGroup).ToList());
    }

    private static ProjectDxfCombinedCadGroup CloneGroup(ProjectDxfCombinedCadGroup group) => new()
    {
        GroupId = group.GroupId,
        SourceCadIds = group.SourceCadIds.ToList(),
        OutputPads = group.OutputPads.Select(static pad => new ProjectCadPadSnapshot
        {
            Id = pad.Id,
            Name = pad.Name,
            Layer = pad.Layer,
            Vertices = pad.Vertices.ToList(),
        }).ToList(),
    };

    internal static Dictionary<int, string> CapturePersistedCadLayerOverrides(CadPadSet? cad, CadPadSet? baselineCad)
    {
        if (cad is null || baselineCad is null)
        {
            return new Dictionary<int, string>();
        }

        return CadEditDeltaRules.EnumerateRelayeredPads(cad, baselineCad)
            .ToDictionary(static pad => pad.Id, static pad => pad.Layer);
    }

    internal static int CountRelayeredPads(CadPadSet cad, CadPadSet baselineCad) =>
        CadEditDeltaRules.CountRelayeredPads(cad, baselineCad);

    internal static int CountRotatedPads(CadPadSet cad, CadPadSet baselineCad) =>
        CadEditDeltaRules.CountRotatedPads(cad, baselineCad);

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

        return CadEditDeltaRules.EnumerateRotatedPads(cad, baselineCad)
            .ToDictionary(static pad => pad.Id, ProjectCadGeometrySnapshot.FromCadPad);
    }
}
