// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfCombinedGroupRules
{
    internal static (HashSet<int> OutputIds, HashSet<int> SourceIds) CollectGroupMemberIds(
        CombinedCadState combinedCad, List<int> validGroupIds)
    {
        var outputIds = new HashSet<int>();
        var sourceIds = new HashSet<int>();
        foreach (var groupId in validGroupIds)
        {
            if (!combinedCad.Groups.TryGetValue(groupId, out var group))
            {
                continue;
            }

            foreach (var outputId in group.OutputCadIds)
            {
                outputIds.Add(outputId);
            }

            foreach (var sourceId in group.SourceCadIds)
            {
                sourceIds.Add(sourceId);
            }
        }

        return (outputIds, sourceIds);
    }

    internal static int RemoveGroupMembers(
        CombinedCadState combinedCad, HashSet<int> hiddenCadPadIds, HashSet<int> outputIds, HashSet<int> sourceIds)
    {
        foreach (var outputId in outputIds)
        {
            combinedCad.GroupIdByOutputCadId.Remove(outputId);
            hiddenCadPadIds.Remove(outputId);
        }
        var restoredSources = 0;
        foreach (var sourceId in sourceIds)
        {
            if (hiddenCadPadIds.Remove(sourceId))
            {
                restoredSources++;
            }

            combinedCad.GroupIdBySourceCadId.Remove(sourceId);
        }

        return restoredSources;
    }

    internal static int AllocatePadId(CadPadSet? cad, CombinedCadState combinedCad)
    {
        if (cad is null)
        {
            return combinedCad.NextPadId++;
        }

        var used = cad.Pads.Select(p => p.Id).ToHashSet();
        while (used.Contains(combinedCad.NextPadId))
        {
            combinedCad.NextPadId++;
        }

        return combinedCad.NextPadId++;
    }

    internal static List<CadPad> CreatePads(
        string layer, IReadOnlyList<Domain.Geometry.Polygon2> polygons, int groupId, CadPadSet? cad, CombinedCadState combinedCad)
    {
        var combinedPads = new List<CadPad>(polygons.Count);
        foreach (var polygon in polygons)
        {
            var combinedId = AllocatePadId(cad, combinedCad);
            combinedPads.Add(new CadPad(
                combinedId,
                $"COMB_{combinedId}",
                layer,
                polygon));
            combinedCad.GroupIdByOutputCadId[combinedId] = groupId;
        }

        return combinedPads;
    }
}
