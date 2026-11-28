using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
        // A toggle button handles its own press before a handler on the button runs.
        // Listen in the tunnel phase so Ctrl+click can show only one layer.
        AddHandler(PointerPressedEvent, OnLayerTogglePointerPressedTunnel, RoutingStrategies.Tunnel);
    }

    private void OnLayerTogglePointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return;
        }

        var button = source as ToggleButton ?? source.FindAncestorOfType<ToggleButton>();
        if (button is null || !button.Classes.Contains("layerToggle"))
        {
            return;
        }

        LayerTogglePointerPressed?.Invoke(button, e);
    }

    private void ResetAllDxfEditsButton_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("Dxf.ResetAllEdits", _ => ResetAllDxfEditsAsync(), CancellationToken.None);
    }

    private async Task ResetAllDxfEditsAsync()
    {
        if (DataContext is not FreeformHelperViewModel viewModel ||
            !viewModel.DxfEdit.HasAnyCadEdits ||
            !viewModel.DxfEdit.ResetAllDxfEditsCommand.CanExecute(null))
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
            "Reset all DXF edits?",
            "Reset",
            "Cancel",
            confirmTip: "Reset all DXF edits in this project.",
            cancelTip: "Keep the current DXF edits.",
            consequenceText: "Hidden pads, duplicated pads, combined pads, layer moves, and rotations will be reset in this project.");
        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed)
        {
            return;
        }

        viewModel.DxfEdit.ResetAllDxfEditsCommand.Execute(null);
    }
}
