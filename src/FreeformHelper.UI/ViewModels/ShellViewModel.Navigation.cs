namespace FreeformHelper.UI.ViewModels;

public sealed partial class ShellViewModel
{
    private void ShowWorkspace() => _ = SelectPageAsync(ShellPage.Workspace);

    private void ShowHowToUse() => _ = SelectPageAsync(ShellPage.HowToUse);

    private void ShowDev() => _ = SelectPageAsync(ShellPage.Dev);

    private Task ShowSimulationAsync() => SelectPageAsync(ShellPage.Simulation);

    public Task ShowSimulationAsync(SimulationWorkspaceViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return SelectPageAsync(ShellPage.Simulation, simulationWorkspace: viewModel);
    }

    private Task ShowCoordinateAsync() => SelectPageAsync(ShellPage.Coordinate);

    public Task ShowCoordinateAsync(CoordinatePlannerWorkspaceViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return SelectPageAsync(ShellPage.Coordinate, coordinateWorkspace: viewModel);
    }

    private Task SelectPageAsync(
        ShellPage page,
        SimulationWorkspaceViewModel? simulationWorkspace = null,
        CoordinatePlannerWorkspaceViewModel? coordinateWorkspace = null)
    {
        // Coordinate keeps the previous page visible until its workspace attempt completes.
        if (page == ShellPage.Coordinate && coordinateWorkspace is null)
        {
            return EnsureCoordinateAndSelectAsync();
        }

        if (simulationWorkspace is not null)
        {
            Simulation.UpdateSourceRevision(FreeformHelper.SimulationWorkspaceSourceRevision);
            Simulation.CurrentWorkspace = simulationWorkspace;
        }

        if (coordinateWorkspace is not null)
        {
            if (CoordinatePlanner.CurrentWorkspace is not null)
            {
                CoordinatePlanner.CurrentWorkspace.WorkspacePreferencesChanged -= OnCoordinateWorkspacePreferencesChanged;
            }

            coordinateWorkspace.WorkspacePreferencesChanged += OnCoordinateWorkspacePreferencesChanged;
            CoordinatePlanner.UpdateSourceRevision(FreeformHelper.WorkspaceDerivedSourceRevision);
            CoordinatePlanner.CurrentWorkspace = coordinateWorkspace;
            OnCoordinateWorkspacePreferencesChanged(coordinateWorkspace.BuildWorkspacePreferences());
        }

        ApplySelection();
        // Simulation displays its host before building, even when no workspace can be built.
        return page == ShellPage.Simulation && simulationWorkspace is null
            ? Simulation.EnsureWorkspaceAsync()
            : Task.CompletedTask;

        async Task EnsureCoordinateAndSelectAsync()
        {
            await CoordinatePlanner.EnsureWorkspaceAsync();
            ApplySelection();
        }

        void ApplySelection()
        {
            if (_selectedPage == page)
            {
                return;
            }

            // Preserve lazy page construction before publishing the selected page.
            _ = GetPageViewModel(page);
            var previousPage = _selectedPage;
            var activeProperty = GetActivePropertyName(page);
            var previousActiveProperty = GetActivePropertyName(previousPage);
            OnPropertyChanging(nameof(CurrentViewModel));
            OnPropertyChanging(activeProperty);
            OnPropertyChanging(previousActiveProperty);
            _selectedPage = page;
            OnPropertyChanged(nameof(CurrentViewModel));
            OnPropertyChanged(activeProperty);
            OnPropertyChanged(previousActiveProperty);
        }
    }

    private object GetPageViewModel(ShellPage page) => page switch
    {
        ShellPage.Workspace => FreeformHelper,
        ShellPage.HowToUse => HowToUse,
        ShellPage.Dev => Dev,
        ShellPage.Simulation => Simulation,
        ShellPage.Coordinate => CoordinatePlanner,
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };

    private static string GetActivePropertyName(ShellPage page) => page switch
    {
        ShellPage.Workspace => nameof(IsWorkspaceActive),
        ShellPage.HowToUse => nameof(IsHowToUseActive),
        ShellPage.Dev => nameof(IsDevActive),
        ShellPage.Simulation => nameof(IsSimulationActive),
        ShellPage.Coordinate => nameof(IsCoordinateActive),
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };
}
