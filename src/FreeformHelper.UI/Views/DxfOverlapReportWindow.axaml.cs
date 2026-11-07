using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

public sealed partial class DxfOverlapReportWindow : Window
{
    private UiEventRunner? _uiEvents => _seams?.UiEvents;

    private readonly DxfOverlapReportSeams? _seams;

    public DxfOverlapReportWindow() : this(null)
    {
    }

    internal DxfOverlapReportWindow(DxfOverlapReportSeams? seams)
    {
        _seams = seams;
        InitializeComponent();
    }

    private void CopyAll_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("DxfOverlap.CopyAll", _ => CopyAllAsync(), CancellationToken.None);
    }

    private async Task CopyAllAsync()
    {
        if (DataContext is not DxfOverlapReportViewModel vm)
        {
            return;
        }

        vm.SetCopyCompleted(false);
        if (_seams?.CopyTextAsync is { } copyText)
        {
            await copyText(vm.ReportText);
        }
        else
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is null)
            {
                return;
            }

            await topLevel.Clipboard.SetTextAsync(vm.ReportText);
        }

        vm.SetCopyCompleted(true);
    }
}

internal sealed record DxfOverlapReportSeams(UiEventRunner UiEvents, Func<string, Task>? CopyTextAsync = null);
