using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.UI.Controls;
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

        if (IsTextEntryControlSource(e.Source))
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

        var fromCanvas = IsCanvasInputSource(e.Source);

        if (e.Key == Key.E && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            return RestoreNotchExportSelectionWindowIfHidden();
        }

        if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None)
        {
            if (!viewModel.DeleteSelectedCadPadsCommand.CanExecute(null))
            {
                return false;
            }

            viewModel.DeleteSelectedCadPadsCommand.Execute(null);
            return true;
        }

        if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
        {
            if (!viewModel.UndoCommand.CanExecute(null))
            {
                return false;
            }

            viewModel.UndoCommand.Execute(null);
            return true;
        }

        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            _uiEvents?.Run("Workspace.SaveShortcut", _ => SaveProjectFromShortcutAsync(viewModel), CancellationToken.None);
            return true;
        }

        if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)
        {
            if (fromCanvas)
            {
                return false;
            }

            _canvas?.SelectAllPads();
            return true;
        }

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)
        {
            if (fromCanvas)
            {
                return false;
            }

            viewModel.FitCommand.Execute(null);
            return true;
        }

        return false;
    }

    private async Task SaveProjectFromShortcutAsync(FreeformHelperViewModel viewModel)
    {
        var save = viewModel.TryStartSaveProjectCommand();
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

    /// <summary>
    /// Determines if the focus should be cleared from input controls and transferred to the canvas.
    /// This is typically true if the clicked element is not an input control.
    /// </summary>
    /// <param name="source">The source element of the pointer event.</param>
    /// <returns>True if focus should be cleared, false otherwise.</returns>
    private static bool ShouldClearFocus(object? source)
    {
        if (source is not Visual visual)
        {
            return true;
        }

        if (visual is ConsolePanel || visual.FindAncestorOfType<ConsolePanel>() is not null)
        {
            return false;
        }

        // If the clicked element or any of its ancestors is an input control, don't clear focus.
        if (visual.FindAncestorOfType<TextBox>() is not null) return false;
        if (visual.FindAncestorOfType<ComboBox>() is not null) return false;
        if (visual.FindAncestorOfType<NumberScrubber>() is not null) return false;

        return true; // Otherwise, clear focus (e.g., focus canvas).
    }

    private static bool IsTextEntryControlSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        if (visual is TextBox or ComboBox or NumberScrubber)
        {
            return true;
        }

        if (visual.FindAncestorOfType<TextBox>() is not null) return true;
        if (visual.FindAncestorOfType<ComboBox>() is not null) return true;
        if (visual.FindAncestorOfType<NumberScrubber>() is not null) return true;

        return false;
    }

    private static bool IsCanvasInputSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        if (visual is PadCanvas)
        {
            return true;
        }

        return visual.FindAncestorOfType<PadCanvas>() is not null;
    }
}
