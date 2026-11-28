// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls.Notifications;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Resolves the message and notification type for a save shortcut result.
/// </summary>
internal static class SaveShortcutToastPolicy
{
    public static SaveShortcutToast Resolve(bool ok, string? statusText)
    {
        var message = string.IsNullOrWhiteSpace(statusText)
            ? ok ? "Project saved." : "Save project failed."
            : statusText;
        var type = ok
            ? NotificationType.Success
            : message.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                ? NotificationType.Information
                : NotificationType.Warning;

        return new SaveShortcutToast(message, type);
    }
}

/// <summary>
/// Carries the resolved save shortcut toast message and notification type.
/// </summary>
internal readonly record struct SaveShortcutToast(string Message, NotificationType Type);
