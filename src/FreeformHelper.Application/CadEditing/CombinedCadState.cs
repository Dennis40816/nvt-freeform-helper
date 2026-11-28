// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.CadEditing;

/// <summary>
/// UI-thread owned. Holds the combined CAD pad edit state: output pads, source pads, groups and the next identifiers.
/// </summary>
public sealed class CombinedCadState
{
    /// <summary>The first id reserved for synthetic combined pads.</summary>
    public const int FirstPadId = 1_500_000_000;

    /// <summary>Gets the group id of each combined output pad. The keys are the combined output pad ids.</summary>
    public Dictionary<int, int> GroupIdByOutputCadId { get; } = new();

    /// <summary>Gets the group id of each source pad that a combine consumed.</summary>
    public Dictionary<int, int> GroupIdBySourceCadId { get; } = new();

    /// <summary>Gets the source and output membership of each accepted group.</summary>
    public Dictionary<int, CombinedCadGroupState> Groups { get; } = new();

    /// <summary>Gets or sets the next group id; clearing groups restarts it.</summary>
    public int NextGroupId { get; set; } = 1;

    /// <summary>Gets or sets the next pad id; only a full clear restarts it.</summary>
    public int NextPadId { get; set; } = FirstPadId;

    /// <summary>Gets the ids of the combined output pads.</summary>
    public Dictionary<int, int>.KeyCollection OutputCadIds => GroupIdByOutputCadId.Keys;

    /// <summary>Forgets every group and restarts group numbering. The next pad id keeps its value.</summary>
    public void ClearGroups()
    {
        GroupIdByOutputCadId.Clear();
        GroupIdBySourceCadId.Clear();
        Groups.Clear();
        NextGroupId = 1;
    }

    /// <summary>Forgets every group and restarts both numberings.</summary>
    public void Clear()
    {
        ClearGroups();
        NextPadId = FirstPadId;
    }

    /// <summary>Clones all group lists, indexes and allocator positions.</summary>
    public CombinedCadState Clone()
    {
        var copy = new CombinedCadState();
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>Restores group lists, indexes and allocators from another state.</summary>
    public void CopyFrom(CombinedCadState other)
    {
        GroupIdByOutputCadId.Clear();
        foreach (var pair in other.GroupIdByOutputCadId)
        {
            GroupIdByOutputCadId[pair.Key] = pair.Value;
        }

        GroupIdBySourceCadId.Clear();
        foreach (var pair in other.GroupIdBySourceCadId)
        {
            GroupIdBySourceCadId[pair.Key] = pair.Value;
        }

        Groups.Clear();
        foreach (var pair in other.Groups)
        {
            Groups[pair.Key] = new CombinedCadGroupState(
                pair.Value.SourceCadIds.ToList(),
                pair.Value.OutputCadIds.ToList());
        }

        NextGroupId = other.NextGroupId;
        NextPadId = other.NextPadId;
    }
}

/// <summary>A combined group with independently cloned source and output memberships.</summary>
/// <param name="SourceCadIds">The consumed source pad ids.</param>
/// <param name="OutputCadIds">The generated output pad ids.</param>
public sealed record CombinedCadGroupState(
    List<int> SourceCadIds,
    List<int> OutputCadIds);
