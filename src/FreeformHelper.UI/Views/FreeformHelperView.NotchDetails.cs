using Avalonia.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
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
