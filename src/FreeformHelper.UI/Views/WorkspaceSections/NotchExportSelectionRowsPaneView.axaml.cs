using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public partial class NotchExportSelectionRowsPaneView : UserControl
{
    private UiEventRunner? _uiEvents => (DataContext as NotchExportSelectionViewModel)?.UiEvents;

    public NotchExportSelectionRowsPaneView()
    {
        InitializeComponent();
    }

    private void InspectRow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            (e.Source is Control source &&
             (source is CheckBox || source.GetVisualAncestors().OfType<CheckBox>().Any())))
        {
            return;
        }

        if (DataContext is NotchExportSelectionViewModel viewModel &&
            sender is Control { DataContext: NotchExportRowItemViewModel row } target)
        {
            viewModel.SelectPreviewRowCommand.Execute(row);
            target.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.Focus();
            e.Handled = true;
        }
    }

    private void OpenHeaderFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        var key = (sender as Control)?.Tag?.ToString();
        _uiEvents?.Run("NotchExport.RowsHeaderFilter", _ => OpenHeaderFilterAsync(key), CancellationToken.None);
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
