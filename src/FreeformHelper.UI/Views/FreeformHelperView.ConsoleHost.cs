using System.ComponentModel;
using Avalonia.Controls;
using NLog;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Shell-level console hosting for <see cref="FreeformHelperView"/>: the row height of the console panel.
/// The console content lives in <see cref="ConsolePanel"/>.
/// </summary>
public sealed partial class FreeformHelperView
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
        if (_consoleRow is null)
        {
            return;
        }

        if (isExpanded)
        {
            var heightResource = this.FindResource("ConsolePanelRowHeight");
            if (heightResource is GridLength gridLength)
            {
                _consoleRow.Height = gridLength;
            }
            else if (heightResource is double heightDouble)
            {
                _consoleRow.Height = new GridLength(heightDouble);
            }

            var minResource = this.FindResource("ConsolePanelMinHeight");
            if (minResource is double minDouble)
            {
                _consoleRow.MinHeight = minDouble;
            }
        }
        else
        {
            _consoleRow.Height = GridLength.Auto;
            var collapsedMin = GetResourceDouble("ConsoleCollapsedMinHeight");
            _consoleRow.MinHeight = collapsedMin > 0 ? collapsedMin : 0;
        }
    }

    private double GetResourceDouble(string key, double fallback = 0.0)
    {
        if (this.TryFindResource(key, out var value))
        {
            switch (value)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
            }
        }

        return fallback;
    }
}
