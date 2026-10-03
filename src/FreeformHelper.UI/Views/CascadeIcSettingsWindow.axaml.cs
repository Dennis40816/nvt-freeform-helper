using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views;

public sealed partial class CascadeIcSettingsWindow : Window
{
    public CascadeIcSettingsWindow()
    {
        InitializeComponent();
    }

    private void DoneButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
