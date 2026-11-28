using Avalonia.Input;

namespace FreeformHelper.UI.Services;

/// <summary>
/// A global shortcut action of the main window.
/// </summary>
internal enum MainWindowShortcut
{
    None,
    RestoreNotchExportSelection,
    DeleteSelectedCadPads,
    Undo,
    SaveProject,
    SelectAllPads,
    Fit,
}

/// <summary>
/// Maps a key press to a global shortcut action of the main window. The view runs the action.
/// </summary>
internal static class MainWindowShortcutRouter
{
    /// <summary>
    /// Returns the shortcut for the key and modifiers, or <see cref="MainWindowShortcut.None"/>.
    /// Select all and Fit stay with the canvas when the key comes from the canvas.
    /// </summary>
    public static MainWindowShortcut Resolve(Key key, KeyModifiers modifiers, bool fromCanvas)
    {
        return (key, modifiers) switch
        {
            (Key.E, KeyModifiers.Control | KeyModifiers.Shift) => MainWindowShortcut.RestoreNotchExportSelection,
            (Key.Delete, KeyModifiers.None) => MainWindowShortcut.DeleteSelectedCadPads,
            (Key.Z, KeyModifiers.Control) => MainWindowShortcut.Undo,
            (Key.S, KeyModifiers.Control) => MainWindowShortcut.SaveProject,
            (Key.A, KeyModifiers.Control) when !fromCanvas => MainWindowShortcut.SelectAllPads,
            (Key.F, KeyModifiers.None) when !fromCanvas => MainWindowShortcut.Fit,
            _ => MainWindowShortcut.None,
        };
    }
}
