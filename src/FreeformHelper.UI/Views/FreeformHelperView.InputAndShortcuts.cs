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
        var viewModel = DataContext as FreeformHelperViewModel;
        if (MainWindowShortcutExecution.TryHandle(
            viewModel?.IsProjectEditingEnabled ?? true, e.Handled, e.Key, e.KeyModifiers, e.Source,
            viewModel?.DxfEdit.DeleteSelectedCadPadsCommand, viewModel?.UndoCommand, viewModel?.FitCommand,
            () => _canvas?.SelectAllPads(),
            RestoreNotchExportSelectionWindowIfHidden,
            viewModel is null
                ? null
                : () => _uiEvents?.Run("Workspace.SaveShortcut", _ => SaveProjectFromShortcutAsync(viewModel), CancellationToken.None)))
        {
            e.Handled = true;
        }
    }

    private async Task SaveProjectFromShortcutAsync(FreeformHelperViewModel viewModel)
    {
        var save = viewModel.TryStartSaveProjectCommandAsync();
        if (save is null)
        {
            return;
        }

        var ok = await save;
        var (message, type) = SaveShortcutToastPolicy.Resolve(ok, viewModel.StatusText);
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
