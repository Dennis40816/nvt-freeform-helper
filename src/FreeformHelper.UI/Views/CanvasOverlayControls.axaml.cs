using Avalonia;
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

    /// <summary>
    /// Defines the <see cref="TopToolbarInset"/> AvaloniaProperty.
    /// </summary>
    public static readonly StyledProperty<double> TopToolbarInsetProperty =
        AvaloniaProperty.Register<CanvasOverlayControls, double>(nameof(TopToolbarInset));

    /// <summary>
    /// Gets the height from the top of the canvas to the bottom of the quick toolbar (layer toggles).
    /// The canvas reads it so that a fit leaves this band free.
    /// </summary>
    public double TopToolbarInset
    {
        get => GetValue(TopToolbarInsetProperty);
        private set => SetValue(TopToolbarInsetProperty, value);
    }

    public CanvasOverlayControls()
    {
        InitializeComponent();
        CanvasLeftOverlay.GetObservable(BoundsProperty)
            .Subscribe(bounds => TopToolbarInset = CanvasLeftOverlay.Margin.Top + bounds.Height);
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
