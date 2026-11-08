using Avalonia.Input;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Maps the shortcut keys of the simulation canvas to canvas view mode changes.
/// </summary>
internal static class SimulationCanvasKeyMap
{
    /// <summary>
    /// Applies the view mode change that belongs to <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key that was pressed on the canvas.</param>
    /// <param name="viewModel">The workspace whose canvas view mode changes.</param>
    /// <returns><c>true</c> when the key has a canvas shortcut; otherwise <c>false</c>.</returns>
    public static bool TryApply(Key key, SimulationWorkspaceViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        switch (key)
        {
            case Key.B:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Before);
                return true;
            case Key.A:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.After);
                return true;
            case Key.D:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);
                return true;
            case Key.C:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.ChangedOnly);
                return true;
            case Key.V:
                viewModel.ToggleBeforeAfterCanvasView();
                return true;
            default:
                return false;
        }
    }
}
