using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views;

public sealed partial class CoordinateArtifactDetailWindow : Window
{
    public CoordinateArtifactDetailWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
