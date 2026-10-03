using Avalonia.Controls;
using Avalonia.Interactivity;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private async Task<NotchTable?> ShowNotchExportSelectionWindowAsync(NotchExportSelectionViewModel viewModel)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return viewModel.BuildSelectedTable();
        }

        var helperViewModel = DataContext as FreeformHelperViewModel;
        var window = new NotchExportSelectionWindow
        {
            DataContext = viewModel
        };
        _notchExportSelectionWindow = window;
        _notchExportSelectionOwner = owner;
        _isNotchExportSelectionHidden = false;
        UpdateNotchExportRestoreHintVisibility();
        viewModel.AttachWindowActions(HideNotchExportSelectionWindowForInspect);

        var completion = new TaskCompletionSource<NotchTable?>();

        EventHandler<bool>? completionHandler = null;
        EventHandler<FreeformHelper.UI.Interaction.SelectionChangedEventArgs>? selectionHandler = null;

        void Cleanup()
        {
            window.CompletionRequested -= completionHandler;
            if (helperViewModel is not null)
            {
                helperViewModel.InteractionState.SelectionChanged -= selectionHandler;
            }

            viewModel.AttachWindowActions(null);
            if (ReferenceEquals(_notchExportSelectionWindow, window))
            {
                _notchExportSelectionWindow = null;
                _notchExportSelectionOwner = null;
                _isNotchExportSelectionHidden = false;
                UpdateNotchExportRestoreHintVisibility();
            }
        }

        completionHandler = (_, confirmed) =>
        {
            Cleanup();
            completion.TrySetResult(confirmed ? viewModel.BuildSelectedTable() : null);
            if (window.IsVisible)
            {
                window.Close();
            }
        };

        selectionHandler = (_, args) => viewModel.ApplyWorkspaceSelection(args.CadIds, args.RegularIndices);

        window.CompletionRequested += completionHandler;
        if (helperViewModel is not null)
        {
            helperViewModel.InteractionState.SelectionChanged += selectionHandler;
            viewModel.ApplyWorkspaceSelection(
                helperViewModel.InteractionState.SelectedCadIds,
                helperViewModel.InteractionState.SelectedRegularIndices);
        }

        window.Show(owner);
        return await completion.Task;
    }

    private void HideNotchExportSelectionWindowForInspect()
    {
        if (_notchExportSelectionWindow is null || !_notchExportSelectionWindow.IsVisible)
        {
            return;
        }

        _notchExportSelectionWindow.Hide();
        _isNotchExportSelectionHidden = true;
        UpdateNotchExportRestoreHintVisibility();
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.ShowTopToast(
                "Export panel hidden. Use top-right Restore or Ctrl+Shift+E.",
                Avalonia.Controls.Notifications.NotificationType.Information);
        }
    }

    private SimulationSafetyAuditResult? ResolveCurrentSimulationSafetyAudit()
    {
        EnsureShellViewModel();
        return _shellViewModel?.Simulation.CurrentWorkspace?.GetSimulationSafetyAuditSnapshot();
    }

    private bool RestoreNotchExportSelectionWindowIfHidden()
    {
        if (_notchExportSelectionWindow is null || !_isNotchExportSelectionHidden)
        {
            return false;
        }

        if (_notchExportSelectionOwner is not null && _notchExportSelectionOwner.IsVisible)
        {
            _notchExportSelectionWindow.Show(_notchExportSelectionOwner);
        }
        else
        {
            _notchExportSelectionWindow.Show();
        }

        _notchExportSelectionWindow.Activate();
        _isNotchExportSelectionHidden = false;
        UpdateNotchExportRestoreHintVisibility();
        return true;
    }

    private void RestoreNotchExportSelectionWindow_Click(object? sender, RoutedEventArgs e)
    {
        RestoreNotchExportSelectionWindowIfHidden();
    }

    private void UpdateNotchExportRestoreHintVisibility()
    {
        if (_notchExportRestoreHint is null)
        {
            return;
        }

        _notchExportRestoreHint.IsVisible = _isNotchExportSelectionHidden;
    }
}
