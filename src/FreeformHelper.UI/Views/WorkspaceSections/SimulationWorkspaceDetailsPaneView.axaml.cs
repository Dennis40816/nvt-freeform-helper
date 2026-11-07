using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public partial class SimulationWorkspaceDetailsPaneView : UserControl
{
    public SimulationWorkspaceDetailsPaneView()
    {
        InitializeComponent();
    }

    private void RiskDiff_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: SimulationSafetyRiskDiffViewModel risk })
        {
            this.GetVisualAncestors().OfType<SimulationWorkspaceView>().FirstOrDefault()?.FocusRiskPad(risk.RegularPadId);
        }
    }
}
