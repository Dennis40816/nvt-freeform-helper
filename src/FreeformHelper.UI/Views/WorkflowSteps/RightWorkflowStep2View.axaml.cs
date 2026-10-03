using Avalonia.Controls;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views.WorkflowSteps;

public partial class RightWorkflowStep2View : UserControl
{
    public event EventHandler<OpenSettingsRequestedEventArgs>? OpenSettingsRequested;

    public RightWorkflowStep2View()
    {
        InitializeComponent();
    }

    private void OpenSettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var section = sender is Control control
            ? ParseSection(control.Tag)
            : null;
        OpenSettingsRequested?.Invoke(this, new OpenSettingsRequestedEventArgs(section));
    }

    private static SettingsWindowSection? ParseSection(object? tag)
    {
        if (tag is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Enum.TryParse<SettingsWindowSection>(raw, ignoreCase: true, out var section)
            ? section
            : null;
    }
}
