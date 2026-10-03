namespace FreeformHelper.UI.ViewModels;

public sealed partial class ShellViewModel
{
    private void ShowWorkspace()
    {
        CurrentViewModel = FreeformHelper;
        IsWorkspaceActive = true;
        IsHowToUseActive = false;
        IsDevActive = false;
        IsSimulationActive = false;
        IsCoordinateActive = false;
    }

    /// <summary>
    /// Activates the How To Use view and deactivates other views.
    /// </summary>
    private void ShowHowToUse()
    {
        CurrentViewModel = HowToUse;
        IsHowToUseActive = true;
        IsWorkspaceActive = false;
        IsDevActive = false;
        IsSimulationActive = false;
        IsCoordinateActive = false;
    }

    /// <summary>
    /// Activates the Dev view and deactivates other views.
    /// </summary>
    private void ShowDev()
    {
        CurrentViewModel = Dev;
        IsDevActive = true;
        IsWorkspaceActive = false;
        IsHowToUseActive = false;
        IsSimulationActive = false;
        IsCoordinateActive = false;
    }

    private void ShowSimulation()
    {
        CurrentViewModel = Simulation;
        IsSimulationActive = true;
        IsWorkspaceActive = false;
        IsHowToUseActive = false;
        IsDevActive = false;
        IsCoordinateActive = false;
    }

    private async Task ShowSimulationAsync()
    {
        ShowSimulation();
        await Simulation.EnsureWorkspaceAsync();
    }

    public Task ShowSimulationAsync(SimulationWorkspaceViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Simulation.UpdateSourceRevision(FreeformHelper.SimulationWorkspaceSourceRevision);
        Simulation.CurrentWorkspace = viewModel;
        ShowSimulation();
        return Task.CompletedTask;
    }

    private void ShowCoordinatePlanner()
    {
        CurrentViewModel = CoordinatePlanner;
        IsCoordinateActive = true;
        IsWorkspaceActive = false;
        IsHowToUseActive = false;
        IsDevActive = false;
        IsSimulationActive = false;
    }

    private async Task ShowCoordinateAsync()
    {
        await CoordinatePlanner.EnsureWorkspaceAsync();
        ShowCoordinatePlanner();
    }

    public Task ShowCoordinateAsync(CoordinatePlannerWorkspaceViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (CoordinatePlanner.CurrentWorkspace is not null)
        {
            CoordinatePlanner.CurrentWorkspace.WorkspacePreferencesChanged -= OnCoordinateWorkspacePreferencesChanged;
        }

        viewModel.WorkspacePreferencesChanged += OnCoordinateWorkspacePreferencesChanged;
        CoordinatePlanner.UpdateSourceRevision(FreeformHelper.WorkspaceDerivedSourceRevision);
        CoordinatePlanner.CurrentWorkspace = viewModel;
        OnCoordinateWorkspacePreferencesChanged(viewModel.BuildWorkspacePreferences());
        ShowCoordinatePlanner();
        return Task.CompletedTask;
    }

}
