using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views.SettingsSections;

public sealed partial class SettingsGeneralSectionView : UserControl
{
    public SettingsGeneralSectionView()
    {
        InitializeComponent();
    }

    private async void EditCascadeDetailsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new CascadeIcSettingsWindow
        {
            DataContext = DataContext,
        };
        await dialog.ShowDialog(owner);
    }
}
