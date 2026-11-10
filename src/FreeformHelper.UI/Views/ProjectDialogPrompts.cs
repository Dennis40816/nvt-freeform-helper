// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Builds and shows the confirmation dialogs used by project save and load.
/// A missing owner window gives the safe answer for each prompt.
/// </summary>
internal static class ProjectDialogPrompts
{
    public static async Task<bool> ConfirmEmbedDxfAsync(Window? owner)
    {
        if (owner is null)
        {
            return false;
        }

        var dialog = new ConfirmDialog(
            "Embed DXF", "Embed the current DXF into the project file?",
            "Embed", "Skip",
            confirmTip: "Embed DXF into the project file.",
            cancelTip: "Cancel and keep external DXF reference.",
            consequenceText: "This increases file size but keeps the project self-contained.");

        return await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>Returns true to save first, false to discard, null to cancel the load.</summary>
    public static async Task<bool?> ConfirmSaveBeforeLoadAsync(Window? owner, string? backupPath)
    {
        if (owner is null)
        {
            return null;
        }

        var dialog = new ConfirmDialog(
            "Unsaved project",
            "Save the current project before loading another one?",
            "Save and load",
            "Load without saving",
            emphasizeCancel: true,
            confirmTip: "Save the current project, then load the selected one.",
            cancelTip: "Discard the unsaved changes and load the selected project.",
            consequenceText: string.IsNullOrWhiteSpace(backupPath)
                ? "Project has not been saved, and the backup copy could not be written."
                : $"Project has not been saved. A backup copy was written to {backupPath}. It stays there until the next backup replaces it.",
            dismissText: "Cancel",
            dismissTip: "Keep the current project open.");
        return await dialog.ShowDialog<bool?>(owner);
    }
}
