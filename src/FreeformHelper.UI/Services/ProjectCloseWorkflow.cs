// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>
/// Resolves whether a close confirmation and any requested save allow the window to close.
/// </summary>
internal static class ProjectCloseWorkflow
{
    internal static async Task<bool> CanCloseAsync(bool? confirmChoice, Func<Task<bool>?> tryStartSave)
    {
        if (confirmChoice is null)
        {
            return false;
        }

        if (confirmChoice.Value)
        {
            var save = tryStartSave();
            if (save is null || !await save)
            {
                return false;
            }
        }

        return true;
    }
}
