using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views;

public partial class CanvasOverlayControls : UserControl
{
    public event EventHandler<RoutedEventArgs>? ShowLeftPanelRequested;
    public event EventHandler<RoutedEventArgs>? ShowRightPanelRequested;

    public bool ShowLeftPanelButtonVisible
    {
        get => ShowLeftPanelButton.IsVisible;
        set => ShowLeftPanelButton.IsVisible = value;
    }

    public bool ShowRightPanelButtonVisible
    {
        get => ShowRightPanelButton.IsVisible;
        set => ShowRightPanelButton.IsVisible = value;
    }

    public CanvasOverlayControls()
    {
        InitializeComponent();
    }

    private void ShowLeftPanelButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowLeftPanelRequested?.Invoke(this, e);
    }

    private void ShowRightPanelButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowRightPanelRequested?.Invoke(this, e);
    }
}
