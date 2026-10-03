using Avalonia.Controls;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class NotchExportColumnFilterWindow : Window
{
    public NotchExportColumnFilterWindow()
    {
        InitializeComponent();
    }

    private void SelectAllButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NotchExportColumnFilterDialogViewModel viewModel)
        {
            viewModel.SelectAll();
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NotchExportColumnFilterDialogViewModel viewModel)
        {
            viewModel.ClearAll();
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
