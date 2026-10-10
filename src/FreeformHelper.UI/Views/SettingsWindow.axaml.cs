using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

public sealed partial class SettingsWindow : Window
{
    private UiEventRunner? _uiEvents => _viewModel?.UiEvents;

    private SettingsWindowViewModel? _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.RequestClose -= OnRequestClose;
        }

        _viewModel = DataContext as SettingsWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.RequestClose += OnRequestClose;
        }

        base.OnDataContextChanged(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.RequestClose -= OnRequestClose;
            _viewModel = null;
        }

        base.OnClosed(e);
    }

    private void OnRequestClose()
    {
        Close();
    }

    private void ResetAllSettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("Settings.ResetAll", _ => ResetAllSettingsAsync(), CancellationToken.None);
    }

    private async Task ResetAllSettingsAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window ?? this;
        var dialog = new ConfirmDialog(
            "Reset all settings",
            "Reset all settings to defaults? This also overwrites app-level general settings for next startup.",
            "Reset",
            "Cancel");
        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed)
        {
            return;
        }

        _viewModel.ResetAllSettingsToDefaults();
    }

    public void NavigateToSection(SettingsWindowSection? section)
    {
        if (section is null || _viewModel is null)
        {
            return;
        }

        _viewModel.SelectedSection = section.Value;
        Dispatcher.UIThread.Post(ScrollToTop, DispatcherPriority.Background);
    }

    private void ScrollToTop()
    {
        var scroll = this.FindControl<ScrollViewer>("SettingsWindowScroll");
        if (scroll is null)
        {
            return;
        }

        scroll.Offset = new Vector(scroll.Offset.X, 0);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SelectedSection = SettingsWindowSection.General;
        }

        Dispatcher.UIThread.Post(ScrollToTop, DispatcherPriority.Background);
    }
}
