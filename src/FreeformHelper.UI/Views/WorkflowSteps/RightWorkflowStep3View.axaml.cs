using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views.WorkflowSteps;

public partial class RightWorkflowStep3View : UserControl
{
    public event EventHandler<OpenSettingsRequestedEventArgs>? OpenSettingsRequested;

    public RightWorkflowStep3View()
    {
        InitializeComponent();
    }

    private void AutoPlayLabel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (AutoPlaySwitch.IsEffectivelyEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Move focus too, so the next Space acts on this switch and not on a guard switch focused before.
            AutoPlaySwitch.Focus(NavigationMethod.Pointer);
            AutoPlaySwitch.SetCurrentValue(ToggleSwitch.IsCheckedProperty, AutoPlaySwitch.IsChecked != true);
            e.Handled = true;
        }
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
