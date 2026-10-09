using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

public partial class LeftDxfPanel : UserControl
{
    private UiEventRunner? _uiEvents => (DataContext as FreeformHelperViewModel)?.UiEvents;

    public event EventHandler<PointerPressedEventArgs>? LayerTogglePointerPressed;

    public LeftDxfPanel()
    {
        InitializeComponent();
    }

    private void OnLayerTogglePointerPressedInternal(object? sender, PointerPressedEventArgs e)
    {
        LayerTogglePointerPressed?.Invoke(sender, e);
    }

    private void ResetAllDxfEditsButton_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("Dxf.ResetAllEdits", _ => ResetAllDxfEditsAsync(), CancellationToken.None);
    }

    private async Task ResetAllDxfEditsAsync()
    {
        if (DataContext is not FreeformHelperViewModel viewModel ||
            !viewModel.HasAnyCadEdits ||
            !viewModel.ResetAllDxfEditsCommand.CanExecute(null))
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var dialog = new ConfirmDialog(
            "Reset DXF edits",
            "Reset all DXF edits, including hidden pads, combined pads, and layer moves?",
            "Reset",
            "Cancel");
        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed)
        {
            return;
        }

        viewModel.ResetAllDxfEditsCommand.Execute(null);
    }
}
