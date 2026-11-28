// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.CadEditing;

/// <summary>
/// UI-thread owned edit history within one CAD document session: the hidden pad ids, the pad ids that a duplicate check hid,
/// the duplicate to canonical pad map, the hidden undo batches and the baseline CAD.
/// </summary>
internal sealed class CadEditState
{
    /// <summary>Gets the ids of the CAD pads the user hid.</summary>
    internal HashSet<int> HiddenCadPadIds { get; } = new();

    /// <summary>Gets the ids of the duplicate CAD pads that the duplicate check hid.</summary>
    internal HashSet<int> AutoHiddenDuplicateCadPadIds { get; } = new();

    /// <summary>Gets the canonical pad id of each duplicate CAD pad.</summary>
    internal Dictionary<int, int> DuplicateCadPadIdToCanonicalId { get; } = new();

    /// <summary>Gets the batches of hidden pad ids that the user can undo, the newest batch on top.</summary>
    internal Stack<List<int>> HiddenUndoStack { get; } = new();

    /// <summary>Gets or sets the CAD pads as they were loaded, before any DXF edit.</summary>
    internal CadPadSet? BaselineCad { get; set; }

    /// <summary>Gets or sets the saved combined groups that the current CAD could not restore. A project save writes them back.</summary>
    internal List<ProjectDxfCombinedCadGroup> UnrestoredCombinedGroups { get; set; } = [];
}
