using Avalonia.Controls;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private readonly GridLength?[] _savedPanelWidths = new GridLength?[5];

    private void SetLeftPanelVisible(bool isVisible)
    {
        SetPanelVisible(0, LeftPanel, isVisible, ref _savedPanelWidths[0]);
        CanvasOverlayControls.ShowLeftPanelButtonVisible = !isVisible;
    }

    private void SetRightPanelVisible(bool isVisible)
    {
        SetPanelVisible(4, RightPanel, isVisible, ref _savedPanelWidths[4]);
        CanvasOverlayControls.ShowRightPanelButtonVisible = !isVisible;
    }

    private void SetPanelVisible(int column, Control panel, bool isVisible, ref GridLength? savedWidth)
    {
        if (LayoutGrid.ColumnDefinitions.Count <= column) return;
        var definition = LayoutGrid.ColumnDefinitions[column];
        if (!isVisible && panel.IsVisible) savedWidth = definition.Width;
        if (isVisible && savedWidth is { } width) definition.Width = width;
        if (!isVisible) definition.Width = new GridLength(0);
        panel.IsVisible = isVisible;
    }

    private void HideLeftPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetLeftPanelVisible(false);
    private void ShowLeftPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetLeftPanelVisible(true);
    private void HideRightPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetRightPanelVisible(false);
    private void ShowRightPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetRightPanelVisible(true);
}
