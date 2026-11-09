// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>
/// Determines whether a project needs a save prompt before closing.
/// </summary>
public static class ProjectClosePolicy
{
    /// <summary>
    /// Returns true for unsaved changes or loaded CAD content that has never been saved.
    /// </summary>
    public static bool ShouldPromptSave(bool hasUnsavedChanges, bool hasLoadedCad, string? lastSavedPath)
    {
        return hasUnsavedChanges || (hasLoadedCad && string.IsNullOrWhiteSpace(lastSavedPath));
    }
}
