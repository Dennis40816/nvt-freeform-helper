// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>Connects the mask workflow to live grid, mapping, project and publication owners.</summary>
internal sealed record RegularMaskEffects(
    Func<RegularGrid?> GetGrid,
    Func<bool> HasCad,
    Func<IReadOnlyList<CadPad>?> GetVisiblePads,
    Action<IReadOnlyList<CadPad>> UpdateMapping,
    Action InvalidateCanvas,
    Action InvalidateDownstream,
    Action PublishSimulationChange,
    Action InvalidateSnapshot,
    Action RefreshDuplicateGroups,
    Action ClearProjectEmbeddedMask,
    Action<string> SetStatus,
    Func<IDisposable> EnterStateChange,
    Func<bool> IsStateChangeActive);
