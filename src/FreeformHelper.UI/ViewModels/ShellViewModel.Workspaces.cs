namespace FreeformHelper.UI.ViewModels;

public sealed partial class ShellViewModel
{
    private async Task BuildSimulationWorkspaceAsync()
    {
        var viewModel = await BuildOrBindSimulationWorkspaceAsync(silent: false, overwriteExistingWorkspace: true);
        if (viewModel is null)
        {
            return;
        }

        await ShowSimulationAsync(viewModel);
    }

    private async Task PrewarmSimulationWorkspaceAsync()
    {
        await _simulationWorkspaceBuildGate.WaitAsync();
        try
        {
            var hasWorkspace = Simulation.CurrentWorkspace is not null;
            var overwriteExistingWorkspace = hasWorkspace &&
                                             Simulation.IsWorkspaceStale &&
                                             Simulation.CanAutoRefreshCurrentWorkspace;
            if (hasWorkspace)
            {
                await BuildOrBindSimulationWorkspaceCoreAsync(silent: true, overwriteExistingWorkspace);
                return;
            }

            var session = await FreeformHelper.CreateSimulationWorkspaceSessionAsync(silent: true);
            if (session is not null)
            {
                Simulation.SetBuildFailure(string.Empty);
            }
        }
        finally
        {
            _simulationWorkspaceBuildGate.Release();
        }
    }

    private async Task BuildCoordinatePlannerWorkspaceAsync()
    {
        var viewModel = await BuildOrBindCoordinatePlannerWorkspaceAsync(silent: false, overwriteExistingWorkspace: true);
        if (viewModel is null)
        {
            return;
        }

        await ShowCoordinateAsync(viewModel);
    }

    private async Task PrewarmCoordinatePlannerWorkspaceAsync()
    {
        await BuildOrBindCoordinatePlannerWorkspaceAsync(silent: true, overwriteExistingWorkspace: false);
    }

    private async Task<SimulationWorkspaceViewModel?> BuildOrBindSimulationWorkspaceAsync(bool silent, bool overwriteExistingWorkspace)
    {
        await _simulationWorkspaceBuildGate.WaitAsync();
        try
        {
            return await BuildOrBindSimulationWorkspaceCoreAsync(silent, overwriteExistingWorkspace);
        }
        finally
        {
            _simulationWorkspaceBuildGate.Release();
        }
    }

    private async Task<SimulationWorkspaceViewModel?> BuildOrBindSimulationWorkspaceCoreAsync(bool silent, bool overwriteExistingWorkspace)
    {
        if (!overwriteExistingWorkspace && Simulation.CurrentWorkspace is not null)
        {
            return Simulation.CurrentWorkspace;
        }

        var session = await FreeformHelper.CreateSimulationWorkspaceSessionAsync(
            silent,
            silent ? null : Simulation.ReportBuildProgress);
        if (session is null)
        {
            Simulation.SetBuildFailure(FreeformHelper.LastSimulationWorkspaceAttemptMessage);
            return null;
        }

        if (!silent)
        {
            Simulation.ReportBuildProgress("Opening simulation workspace...");
        }

        var viewModel = new SimulationWorkspaceViewModel(_simulationWorkspaceUseCase, session);
        Simulation.UpdateSourceRevision(FreeformHelper.SimulationWorkspaceSourceRevision);
        Simulation.SetBuildFailure(string.Empty);
        Simulation.CurrentWorkspace = viewModel;
        return viewModel;
    }

    private async Task<CoordinatePlannerWorkspaceViewModel?> BuildOrBindCoordinatePlannerWorkspaceAsync(bool silent, bool overwriteExistingWorkspace)
    {
        await _coordinatePlannerWorkspaceBuildGate.WaitAsync();
        try
        {
            if (!overwriteExistingWorkspace && CoordinatePlanner.CurrentWorkspace is not null)
            {
                return CoordinatePlanner.CurrentWorkspace;
            }

            var session = await FreeformHelper.CreateCoordinatePlannerWorkspaceSessionAsync(silent);
            if (session is null)
            {
                return null;
            }

            var viewModel = new CoordinatePlannerWorkspaceViewModel(_coordinatePlannerWorkspaceUseCase, session);
            if (CoordinatePlanner.CurrentWorkspace is not null)
            {
                CoordinatePlanner.CurrentWorkspace.WorkspacePreferencesChanged -= OnCoordinateWorkspacePreferencesChanged;
            }

            viewModel.WorkspacePreferencesChanged += OnCoordinateWorkspacePreferencesChanged;
            CoordinatePlanner.UpdateSourceRevision(FreeformHelper.WorkspaceDerivedSourceRevision);
            CoordinatePlanner.CurrentWorkspace = viewModel;
            OnCoordinateWorkspacePreferencesChanged(viewModel.BuildWorkspacePreferences());
            return viewModel;
        }
        finally
        {
            _coordinatePlannerWorkspaceBuildGate.Release();
        }
    }

    private void OnFreeformHelperWorkspaceSourceChanged(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _pendingWorkspaceSourceChangeVersion);
        TryRunWorkspaceSourceChangeDrain();
    }

    private void TryRunWorkspaceSourceChangeDrain()
    {
        if (Interlocked.CompareExchange(ref _workspaceSourceChangeDrainRunning, 1, 0) != 0)
        {
            return;
        }

        _ = RunWorkspaceSourceChangeDrainAsync();
    }

    private async Task RunWorkspaceSourceChangeDrainAsync()
    {
        try
        {
            while (true)
            {
                var processedVersion = Volatile.Read(ref _processedWorkspaceSourceChangeVersion);
                var targetVersion = Volatile.Read(ref _pendingWorkspaceSourceChangeVersion);
                if (targetVersion <= processedVersion)
                {
                    break;
                }

                // Keep prewarm strictly in background: do not contend with foreground Step5 export.
                if (FreeformHelper.IsNotchExporting)
                {
                    await Task.Delay(200);
                    continue;
                }

                Simulation.UpdateSourceRevision(FreeformHelper.SimulationWorkspaceSourceRevision);
                await Simulation.PrewarmWorkspaceAsync();
                CoordinatePlanner.UpdateSourceRevision(FreeformHelper.WorkspaceDerivedSourceRevision);
                await CoordinatePlanner.PrewarmWorkspaceAsync();
                Volatile.Write(
                    ref _processedWorkspaceSourceChangeVersion,
                    Volatile.Read(ref _pendingWorkspaceSourceChangeVersion));
            }
        }
        catch (Exception ex)
        {
            Volatile.Write(
                ref _processedWorkspaceSourceChangeVersion,
                Volatile.Read(ref _pendingWorkspaceSourceChangeVersion));
            Logger.Error(ex, "Derived workspace prewarm failed after workspace source change.");
        }
        finally
        {
            Volatile.Write(ref _workspaceSourceChangeDrainRunning, 0);
            if (Volatile.Read(ref _processedWorkspaceSourceChangeVersion) <
                Volatile.Read(ref _pendingWorkspaceSourceChangeVersion))
            {
                TryRunWorkspaceSourceChangeDrain();
            }
        }
    }

    public void Dispose()
    {
        DetachSimulationSafetyOverviewSync();
        FreeformHelper.WorkspaceDerivedSourceChanged -= OnFreeformHelperWorkspaceSourceChanged;
        if (CoordinatePlanner.CurrentWorkspace is not null)
        {
            CoordinatePlanner.CurrentWorkspace.WorkspacePreferencesChanged -= OnCoordinateWorkspacePreferencesChanged;
        }
        _simulationWorkspaceBuildGate.Dispose();
        _coordinatePlannerWorkspaceBuildGate.Dispose();
    }

    private void OnCoordinateWorkspacePreferencesChanged(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerWorkspacePreferences preferences)
    {
        FreeformHelper.ApplyCoordinatePlannerPreferences(
            preferences.PixelWidth,
            preferences.PixelHeight,
            preferences.PreferredAaOutlineLayerName);
    }
}
