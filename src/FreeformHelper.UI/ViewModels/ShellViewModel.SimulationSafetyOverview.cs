using System.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class ShellViewModel
{
    private SimulationWorkspaceViewModel? _observedSimulationSafetyWorkspace;

    private void AttachSimulationSafetyOverviewSync()
    {
        Simulation.PropertyChanged += OnSimulationHostSafetyPropertyChanged;
        SyncObservedSimulationSafetyWorkspace();
        RefreshFreeformHelperSimulationSafetyOverview();
    }

    private void DetachSimulationSafetyOverviewSync()
    {
        Simulation.PropertyChanged -= OnSimulationHostSafetyPropertyChanged;
        if (_observedSimulationSafetyWorkspace is not null)
        {
            _observedSimulationSafetyWorkspace.PropertyChanged -= OnSimulationWorkspaceSafetyPropertyChanged;
            _observedSimulationSafetyWorkspace = null;
        }
    }

    private void OnSimulationHostSafetyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SimulationHostViewModel.CurrentWorkspace), StringComparison.Ordinal))
        {
            SyncObservedSimulationSafetyWorkspace();
            RefreshFreeformHelperSimulationSafetyOverview();
            return;
        }

        if (e.PropertyName is nameof(SimulationHostViewModel.SourceRevision)
            or nameof(SimulationHostViewModel.BuildFailureText))
        {
            RefreshFreeformHelperSimulationSafetyOverview();
        }
    }

    private void OnSimulationWorkspaceSafetyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SimulationWorkspaceViewModel.SimulationSafetyStatusText)
            or nameof(SimulationWorkspaceViewModel.SimulationSafetySummaryText)
            or nameof(SimulationWorkspaceViewModel.SimulationMaxAfterText)
            or nameof(SimulationWorkspaceViewModel.SimulationSafetyViolationCountText)
            or nameof(SimulationWorkspaceViewModel.SimulationHighRiskDiffListText))
        {
            RefreshFreeformHelperSimulationSafetyOverview();
        }
    }

    private void SyncObservedSimulationSafetyWorkspace()
    {
        if (ReferenceEquals(_observedSimulationSafetyWorkspace, Simulation.CurrentWorkspace))
        {
            return;
        }

        if (_observedSimulationSafetyWorkspace is not null)
        {
            _observedSimulationSafetyWorkspace.PropertyChanged -= OnSimulationWorkspaceSafetyPropertyChanged;
        }

        _observedSimulationSafetyWorkspace = Simulation.CurrentWorkspace;
        if (_observedSimulationSafetyWorkspace is not null)
        {
            _observedSimulationSafetyWorkspace.PropertyChanged += OnSimulationWorkspaceSafetyPropertyChanged;
        }
    }

    private void RefreshFreeformHelperSimulationSafetyOverview()
    {
        var workspace = Simulation.CurrentWorkspace;
        FreeformHelper.ApplySimulationSafetyOverview(
            workspace?.GetSimulationSafetyAuditSnapshot(),
            Simulation.IsWorkspaceStale,
            Simulation.BuildFailureText);
    }
}
