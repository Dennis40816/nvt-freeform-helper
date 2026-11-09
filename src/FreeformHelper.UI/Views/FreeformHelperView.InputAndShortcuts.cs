using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is FreeformHelperViewModel { IsProjectEditingEnabled: false })
        {
            return;
        }

        if (e.Handled)
        {
            return;
        }

        if (MainWindowInputSources.IsTextEntry(e.Source))
        {
            return;
        }

        if (TryHandleGlobalShortcut(e))
        {
            e.Handled = true;
        }
    }

    private bool TryHandleGlobalShortcut(KeyEventArgs e)
    {
        if (DataContext is not FreeformHelperViewModel viewModel)
        {
            return false;
        }

        var shortcut = MainWindowShortcutRouter.Resolve(e.Key, e.KeyModifiers, MainWindowInputSources.IsCanvas(e.Source));
        switch (shortcut)
        {
            case MainWindowShortcut.RestoreNotchExportSelection:
                return RestoreNotchExportSelectionWindowIfHidden();
            case MainWindowShortcut.DeleteSelectedCadPads:
                return TryExecute(viewModel.DeleteSelectedCadPadsCommand);
            case MainWindowShortcut.Undo:
                return TryExecute(viewModel.UndoCommand);
            case MainWindowShortcut.SaveProject:
                _uiEvents?.Run("Workspace.SaveShortcut", _ => SaveProjectFromShortcutAsync(viewModel), CancellationToken.None);
                return true;
            case MainWindowShortcut.SelectAllPads:
                _canvas?.SelectAllPads();
                return true;
            case MainWindowShortcut.Fit:
                viewModel.FitCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private static bool TryExecute(System.Windows.Input.ICommand command)
    {
        if (!command.CanExecute(null))
        {
            return false;
        }

        command.Execute(null);
        return true;
    }

    private async Task SaveProjectFromShortcutAsync(FreeformHelperViewModel viewModel)
    {
        var save = viewModel.TryStartSaveProjectCommandAsync();
        if (save is null)
        {
            return;
        }

        var ok = await save;
        var message = string.IsNullOrWhiteSpace(viewModel.StatusText)
            ? ok ? "Project saved." : "Save project failed."
            : viewModel.StatusText;
        var type = ok
            ? Avalonia.Controls.Notifications.NotificationType.Success
            : message.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                ? Avalonia.Controls.Notifications.NotificationType.Information
                : Avalonia.Controls.Notifications.NotificationType.Warning;
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.ShowTopToast(message, type);
        }
    }

    /// <summary>
    /// Attempts to close the pad information popover if a pointer pressed event occurred outside its bounds.
    /// </summary>
    /// <param name="e">The <see cref="PointerPressedEventArgs"/> event data.</param>
    /// <returns>True if the pad info was closed or a pending close was handled, false otherwise.</returns>
    private bool ClosePadInfoIfOutside(PointerPressedEventArgs e)
    {
        // If no overlay or popover, or they are not visible, then there's nothing to close.
        if (_padInfoOverlay is null || _padInfoPopover is null || !_padInfoOverlay.IsVisible)
        {
            return false;
        }

        // Check if the click occurred within the pad info popover itself.
        if (e.Source is Visual visual)
        {
            if (visual.FindAncestorOfType<PadInfoPopover>() is not null)
            {
                _padInfoPopover.HideCloseConfirm(); // Hide any pending close confirmation.
                SetPadAreaHitTest(true); // Re-enable hit testing on pads.
                return false; // Click was inside, so don't close.
            }
        }

        // If the click was outside the popover, request to close it.
        return RequestClosePadInfo();
    }

    private void CloseRootMenusIfOutside(object? source)
    {
        if (source is not Visual visual)
        {
            CloseAllRootMenus();
            return;
        }

        if (visual.FindAncestorOfType<Menu>() is not null)
        {
            return;
        }

        if (visual.FindAncestorOfType<MenuItem>() is not null)
        {
            return;
        }

        CloseAllRootMenus();
    }
}
