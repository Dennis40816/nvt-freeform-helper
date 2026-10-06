using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class NotchExportSelectionWindow : Window
{
    private bool _completionRaised;

    public NotchExportSelectionWindow()
    {
        InitializeComponent();
    }

    public event EventHandler<bool>? CompletionRequested;

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        RequestClose(false);
    }

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NotchExportSelectionViewModel viewModel &&
            viewModel.TryGetExportBlockMessage(out var title, out var message))
        {
            var dialog = new WarningDialog(title, message);
            await dialog.ShowDialog(this);
            return;
        }

        RequestClose(true);
    }

    private async void CopyPayloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotchExportSelectionViewModel viewModel)
        {
            return;
        }

        var text = viewModel.SelectedRowCodePreviewText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }

    private async void ShowFilterBuilderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotchExportSelectionViewModel viewModel)
        {
            return;
        }

        var helper = new NotchExportFilterBuilderWindow();
        helper.SetInitialQuery(viewModel.SearchKeyword);
        var confirmed = await helper.ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        viewModel.SearchKeyword = helper.GeneratedQuery;
    }

    private async void OpenHeaderFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotchExportSelectionViewModel viewModel)
        {
            return;
        }

        var key = (sender as Control)?.Tag?.ToString();
        if (!viewModel.TryBuildColumnFilterDialog(key, out var dialogViewModel) || dialogViewModel is null)
        {
            return;
        }

        var dialog = new NotchExportColumnFilterWindow
        {
            DataContext = dialogViewModel,
        };

        var confirmed = await dialog.ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        viewModel.ApplyColumnFilterSelection(
            dialogViewModel.Field,
            dialogViewModel.GetSelectedKeys(),
            dialogViewModel.GetAllKeys());
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_completionRaised)
        {
            CompletionRequested?.Invoke(this, false);
            _completionRaised = true;
        }

        base.OnClosed(e);
    }

    private void RequestClose(bool confirmed)
    {
        if (_completionRaised)
        {
            return;
        }

        _completionRaised = true;
        CompletionRequested?.Invoke(this, confirmed);
        Close();
    }
}
