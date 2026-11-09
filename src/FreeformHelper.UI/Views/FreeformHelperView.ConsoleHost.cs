using System.ComponentModel;
using FreeformHelper.UI.Services;
using NLog;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Shell-level console hosting for <see cref="FreeformHelperView"/>: the row height of the console panel.
/// The console content lives in <see cref="ConsolePanel"/>.
/// </summary>
public sealed partial class FreeformHelperView
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ViewModels.ShellViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(ViewModels.ShellViewModel.IsConsoleExpanded))
        {
            SyncConsoleRowHeight(vm.IsConsoleExpanded);
        }
    }

    private void EnsureShellViewModel()
    {
        if (_shellViewModel is not null)
        {
            return;
        }

        _shellViewModel = _topLevel?.DataContext as ViewModels.ShellViewModel
            ?? DataContext as ViewModels.ShellViewModel;
        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void SyncConsoleRowHeight(bool isExpanded)
    {
        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(this, _consoleRow, isExpanded);
    }
}
