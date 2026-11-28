// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.CadEditing;

internal sealed record CadEditDelta(int Relayered, int Rotated);

internal sealed record CadEditUndoSnapshot(
    CadPadSet? Cad,
    List<int> HiddenCadIds,
    List<int> AutoHiddenDuplicateCadIds,
    List<List<int>> HiddenUndoBatchesTopToBottom,
    Dictionary<int, int> DuplicateCadPadIdToCanonicalId,
    CombinedCadState Combined);

internal sealed record PersistedDxfEditProjectState(
    HashSet<int> HiddenCadPadIds,
    HashSet<int> VisibleDuplicateCadPadIds,
    Dictionary<int, string> CadLayerOverrides,
    Dictionary<int, ProjectCadGeometrySnapshot> CadGeometryOverrides,
    List<ProjectDxfCombinedCadGroup> CombinedCadGroups);

// UI-thread owned, operation-local notification history. Advance only at the original Sync points.
internal sealed class CadEditNotificationSnapshot(int deleted, int duplicate, int combined, int relayered, int rotated)
{
    public int Deleted { get; set; } = deleted;
    public int Duplicate { get; set; } = duplicate;
    public int Combined { get; set; } = combined;
    public int Relayered { get; set; } = relayered;
    public int Rotated { get; set; } = rotated;
    public bool HasAny => Deleted > 0 || Combined > 0 || Relayered > 0 || Rotated > 0;
}

// Stable workflow ports. They carry no document state and expose no ViewModel types.
internal sealed record CadEditPublication(
    Func<CadEditNotificationSnapshot> Capture,
    Action<CadEditNotificationSnapshot?> Relayered,
    Action<CadEditNotificationSnapshot?> Rotated,
    Action<CadEditNotificationSnapshot?> Hidden,
    Action<CadEditNotificationSnapshot?> Combined,
    Action<CadEditNotificationSnapshot?, bool> Duplicate);

internal sealed record CadEditLayers(
    Func<Dictionary<string, bool>> CaptureSelection,
    Action PreserveDependentSelections,
    Action<CadPadSet> Update,
    Action<Dictionary<string, bool>, string> RestoreSelection,
    Func<CadPadSet, bool> MatchesCurrentLayers,
    Action<string> SelectTargetLayer);

internal sealed record CadEditEffects(
    Action ClosePadInfo,
    Action ClearSelection,
    Action Filter,
    Action RebuildUnlessLayerFilterDoes,
    Action<string> SetStatus,
    Action MarkUnsaved,
    Action<Action, string> PushUndo);
