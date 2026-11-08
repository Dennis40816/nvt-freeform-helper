using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Nvt.Core.Avalonia.Panels;

namespace FreeformHelper.UI.Views;

public sealed partial class CoordinateArtifactDetailWindow : Window
{
    public CoordinateArtifactDetailWindow()
    {
        InitializeComponent();
        CoordinatesSection.PropertyChanged += Section_PropertyChanged;
        SourceDetailSection.PropertyChanged += Section_PropertyChanged;
    }

    private void Section_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != CollapsiblePanel.IsExpandedProperty || sender is not CollapsiblePanel { IsExpanded: true } section)
        {
            return;
        }

        var other = ReferenceEquals(section, CoordinatesSection) ? SourceDetailSection : CoordinatesSection;
        other.SetCurrentValue(CollapsiblePanel.IsExpandedProperty, false);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
