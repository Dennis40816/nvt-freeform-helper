// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Windows.Input;
using Avalonia.Input;
using FreeformHelper.UI.Views;

namespace FreeformHelper.UI.Services;

/// <summary>Accepts main-window key input and executes its resolved shortcut.</summary>
internal static class MainWindowShortcutExecution
{
    /// <summary>
    /// Returns whether the shortcut was handled. Null commands represent an absent shortcut context;
    /// a null save callback represents an absent event runner.
    /// </summary>
    public static bool TryHandle(
        bool isProjectEditingEnabled,
        bool alreadyHandled,
        Key key,
        KeyModifiers modifiers,
        object? source,
        ICommand? deleteSelectedCadPadsCommand,
        ICommand? undoCommand,
        ICommand? fitCommand,
        Action selectAllPads,
        Func<bool> restoreNotchExportSelection,
        Action? saveProject)
    {
        if (!isProjectEditingEnabled)
        {
            return false;
        }

        if (alreadyHandled)
        {
            return false;
        }

        if (MainWindowInputSources.IsTextEntry(source))
        {
            return false;
        }

        if (deleteSelectedCadPadsCommand is null || undoCommand is null || fitCommand is null)
        {
            return false;
        }

        var shortcut = MainWindowShortcutRouter.Resolve(key, modifiers, MainWindowInputSources.IsCanvas(source));
        switch (shortcut)
        {
            case MainWindowShortcut.RestoreNotchExportSelection:
                return restoreNotchExportSelection();
            case MainWindowShortcut.DeleteSelectedCadPads:
                return TryExecute(deleteSelectedCadPadsCommand);
            case MainWindowShortcut.Undo:
                return TryExecute(undoCommand);
            case MainWindowShortcut.SaveProject:
                saveProject?.Invoke();
                return true;
            case MainWindowShortcut.SelectAllPads:
                selectAllPads();
                return true;
            case MainWindowShortcut.Fit:
                fitCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private static bool TryExecute(ICommand command)
    {
        if (!command.CanExecute(null))
        {
            return false;
        }

        command.Execute(null);
        return true;
    }
}
