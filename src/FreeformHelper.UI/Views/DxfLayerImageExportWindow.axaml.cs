using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views;

public sealed partial class DxfLayerImageExportWindow : Window
{
    public DxfLayerImageExportWindow()
    {
        InitializeComponent();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
