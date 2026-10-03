using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FreeformHelper.UI.Views.RightWorkflowSections;

public sealed partial class RightWorkflowInspectorView : UserControl
{
    public RightWorkflowInspectorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
