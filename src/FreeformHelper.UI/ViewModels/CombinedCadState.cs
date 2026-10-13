namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the combined CAD pad edit state: output pads, source pads, groups and the next identifiers.
/// </summary>
internal sealed class CombinedCadState
{
    internal const int FirstPadId = 1_500_000_000;

    /// <summary>Gets the group id of each combined output pad. The keys are the combined output pad ids.</summary>
    internal Dictionary<int, int> GroupIdByOutputCadId { get; } = new();

    /// <summary>Gets the group id of each source pad that a combine consumed.</summary>
    internal Dictionary<int, int> GroupIdBySourceCadId { get; } = new();

    internal Dictionary<int, CombinedCadGroupState> Groups { get; } = new();

    internal int NextGroupId { get; set; } = 1;

    internal int NextPadId { get; set; } = FirstPadId;

    /// <summary>Gets the ids of the combined output pads.</summary>
    internal Dictionary<int, int>.KeyCollection OutputCadIds => GroupIdByOutputCadId.Keys;

    /// <summary>Forgets every group and restarts group numbering. The next pad id keeps its value.</summary>
    internal void ClearGroups()
    {
        GroupIdByOutputCadId.Clear();
        GroupIdBySourceCadId.Clear();
        Groups.Clear();
        NextGroupId = 1;
    }

    /// <summary>Forgets every group and restarts both numberings.</summary>
    internal void Clear()
    {
        ClearGroups();
        NextPadId = FirstPadId;
    }

    internal CombinedCadState Clone()
    {
        var copy = new CombinedCadState();
        copy.CopyFrom(this);
        return copy;
    }

    internal void CopyFrom(CombinedCadState other)
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

internal sealed record CombinedCadGroupState(
    List<int> SourceCadIds,
    List<int> OutputCadIds);
