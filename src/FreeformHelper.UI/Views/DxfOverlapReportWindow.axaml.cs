using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class DxfOverlapReportWindow : Window
{
    public DxfOverlapReportWindow()
    {
        InitializeComponent();
    }

    private async void CopyAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DxfOverlapReportViewModel vm)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(vm.ReportText);
    }
}
