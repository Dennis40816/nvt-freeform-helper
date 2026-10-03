using Avalonia.Controls;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public partial class NotchExportSelectionRowsPaneView : UserControl
{
    public NotchExportSelectionRowsPaneView()
    {
        InitializeComponent();
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

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new NotchExportColumnFilterWindow
        {
            DataContext = dialogViewModel,
        };

        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed)
        {
            return;
        }

        viewModel.ApplyColumnFilterSelection(
            dialogViewModel.Field,
            dialogViewModel.GetSelectedKeys(),
            dialogViewModel.GetAllKeys());
    }
}
