using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private void OpenSettingsWindow_Click(object? sender, RoutedEventArgs e)
    {
        OpenSettingsWindowCore(section: null, source: "toolbar");
    }

    private void OnRightPanelOpenSettingsRequested(object? sender, OpenSettingsRequestedEventArgs e)
    {
        OpenSettingsWindowCore(e.Section, source: "right-panel");
    }

    private void OpenSettingsWindowCore(SettingsWindowSection? section, string source)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (DataContext is not ViewModels.FreeformHelperViewModel viewModel)
        {
            return;
        }

        _logger.Info(CultureInfo.InvariantCulture, "Open settings window requested: source={0}, section={1}.",
            source,
            section?.ToString() ?? "General");

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            _settingsWindow.NavigateToSection(section);
            return;
        }

        _settingsWindow = new SettingsWindow
        {
            DataContext = viewModel.CreateSettingsWindowViewModel()
        };
        _settingsWindow.Bind(IsEnabledProperty, new Binding(nameof(FreeformHelperViewModel.IsProjectEditingEnabled))
        {
            Source = viewModel,
        });
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;

        if (owner is not null)
        {
            _settingsWindow.Show(owner);
        }
        else
        {
            _settingsWindow.Show();
        }

        _settingsWindow.NavigateToSection(section);
    }

    private void CloseAllRootMenus()
    {
        foreach (var item in GetRootMenuItems())
        {
            if (item.IsSubMenuOpen)
            {
                item.IsSubMenuOpen = false;
            }
        }
    }

    private IReadOnlyList<MenuItem> GetRootMenuItems()
    {
        _mainMenu ??= this.FindControl<Menu>("MainMenu");
        if (_mainMenu?.Items is null)
        {
            return Array.Empty<MenuItem>();
        }

        return _mainMenu.Items.OfType<MenuItem>().ToList();
    }

    private Task ShowIndexMappingReportWindowAsync(IndexMappingReportViewModel viewModel)
    {
        viewModel.Editing.AttachProject(DataContext as FreeformHelperViewModel);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (_indexMappingReportWindow is not null && _indexMappingReportWindow.IsVisible)
        {
            _indexMappingReportWindow.DataContext = viewModel;
            _indexMappingReportWindow.Activate();
            return Task.CompletedTask;
        }

        if (owner is null)
        {
            return Task.CompletedTask;
        }

        var window = new IndexMappingReportWindow
        {
            DataContext = viewModel
        };
        _indexMappingReportWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_indexMappingReportWindow, window))
            {
                _indexMappingReportWindow = null;
            }
        };

        window.Show(owner);
        return Task.CompletedTask;
    }

    private Task ShowNotchDetailWindowAsync(NotchDetailViewModel viewModel)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return Task.CompletedTask;
        }

        var window = new NotchDetailWindow
        {
            DataContext = viewModel
        };
        window.Show(owner);
        return Task.CompletedTask;
    }
}
