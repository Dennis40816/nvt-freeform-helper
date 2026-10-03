using Avalonia.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public partial class RightWorkflowPanel : UserControl
{
    public event EventHandler<OpenSettingsRequestedEventArgs>? OpenSettingsRequested;

    public RightWorkflowPanel()
    {
        InitializeComponent();
    }

    private void OnStepOpenSettingsRequested(object? sender, OpenSettingsRequestedEventArgs e)
    {
        OpenSettingsRequested?.Invoke(this, e);
    }
}

public sealed class OpenSettingsRequestedEventArgs : EventArgs
{
    public OpenSettingsRequestedEventArgs(SettingsWindowSection? section)
    {
        Section = section;
    }

    public SettingsWindowSection? Section { get; }
}
