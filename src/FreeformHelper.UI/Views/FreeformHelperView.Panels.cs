using Avalonia.Controls;

namespace FreeformHelper.UI.Views;

/// <summary>
/// This partial class of <see cref="FreeformHelperView"/> manages the visibility
/// and width of the left and right panels within the main layout grid.
/// It provides methods to show or hide these panels and updates associated UI elements.
/// </summary>
public sealed partial class FreeformHelperView
{
    /// <summary>
    /// Sets the visibility of the left panel and adjusts the layout grid's column definition.
    /// </summary>
    /// <param name="isVisible">True to show the left panel, false to hide it.</param>
    private void SetLeftPanelVisible(bool isVisible)
    {
        var grid = LayoutGrid; // Assuming LayoutGrid is a named Grid control in the XAML.
        if (grid.ColumnDefinitions.Count < 5) return; // Ensure the grid has enough columns.

        // Adjust the width of the column corresponding to the left panel.
        var width = GetResourceDouble("PanelLeftWidth", 280.0);
        grid.ColumnDefinitions[0].Width = isVisible ? new GridLength(width) : new GridLength(0);
        LeftPanel.IsVisible = isVisible; // Set the visibility of the actual panel control.

        CanvasOverlayControls.ShowLeftPanelButtonVisible = !isVisible; // Toggle visibility of the "Show Left Panel" button.
    }

    /// <summary>
    /// Sets the visibility of the right panel and adjusts the layout grid's column definition.
    /// </summary>
    /// <param name="isVisible">True to show the right panel, false to hide it.</param>
    private void SetRightPanelVisible(bool isVisible)
    {
        var grid = LayoutGrid; // Assuming LayoutGrid is a named Grid control in the XAML.
        if (grid.ColumnDefinitions.Count < 5) return; // Ensure the grid has enough columns.

        // Adjust the width of the column corresponding to the right panel.
        var width = GetResourceDouble("PanelRightWidth", 360.0);
        grid.ColumnDefinitions[4].Width = isVisible ? new GridLength(width) : new GridLength(0);
        RightPanel.IsVisible = isVisible; // Set the visibility of the actual panel control.

        CanvasOverlayControls.ShowRightPanelButtonVisible = !isVisible; // Toggle visibility of the "Show Right Panel" button.
    }

    // --- Event Handlers for Panel Visibility Buttons ---

    /// <summary>
    /// Event handler for the "Hide Left Panel" button click.
    /// </summary>
    private void HideLeftPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetLeftPanelVisible(false);

    /// <summary>
    /// Event handler for the "Show Left Panel" button click.
    /// </summary>
    private void ShowLeftPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetLeftPanelVisible(true);

    /// <summary>
    /// Event handler for the "Hide Right Panel" button click.
    /// </summary>
    private void HideRightPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetRightPanelVisible(false);

    /// <summary>
    /// Event handler for the "Show Right Panel" button click.
    /// </summary>
    private void ShowRightPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetRightPanelVisible(true);
}
