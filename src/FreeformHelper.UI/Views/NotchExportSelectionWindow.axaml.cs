using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

public sealed partial class NotchExportSelectionWindow : Window
{
    private UiEventRunner? _uiEvents => (DataContext as NotchExportSelectionViewModel)?.UiEvents;

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

    private void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("NotchExport.Export", _ => ConfirmExportAsync(), CancellationToken.None);
    }

    private async Task ConfirmExportAsync()
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

    private void CopyPayloadButton_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("NotchExport.CopyPayload", _ => CopyPayloadAsync(), CancellationToken.None);
    }

    private async Task CopyPayloadAsync()
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

    private void ShowFilterBuilderButton_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("NotchExport.FilterBuilder", _ => ShowFilterBuilderAsync(), CancellationToken.None);
    }

    private async Task ShowFilterBuilderAsync()
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

    private void OpenHeaderFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        var key = (sender as Control)?.Tag?.ToString();
        _uiEvents?.Run("NotchExport.HeaderFilter", _ => OpenHeaderFilterAsync(key), CancellationToken.None);
    }

    private async Task OpenHeaderFilterAsync(string? key)
    {
        if (DataContext is not NotchExportSelectionViewModel viewModel)
        {
            return;
        }

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
        if (_completionRaised || (confirmed && DataContext is NotchExportSelectionViewModel { Editing.IsProjectEditingEnabled: false }))
        {
            return;
        }

        _completionRaised = true;
        CompletionRequested?.Invoke(this, confirmed);
        Close();
    }
}
