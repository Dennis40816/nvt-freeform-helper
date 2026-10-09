using Avalonia.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private Task ShowDxfOverlapReportWindowAsync(DxfOverlapReportViewModel viewModel)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var window = new DxfOverlapReportWindow(new DxfOverlapReportSeams(((FreeformHelperViewModel)DataContext!).UiEvents))
        {
            DataContext = viewModel
        };

        if (owner is not null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        return Task.CompletedTask;
    }
}
