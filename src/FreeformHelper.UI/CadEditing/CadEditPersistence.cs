// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.CadEditing;

// Writes through to the current project; a replacement project is resolved at publication time.
internal sealed record CadEditPersistence(
    Action<HashSet<int>> SetHiddenIds,
    Action<HashSet<int>> SetVisibleDuplicateIds);
