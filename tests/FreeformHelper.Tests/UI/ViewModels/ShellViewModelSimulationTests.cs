using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ShellViewModelSimulationTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] SimulationDiffIndices = { 10, 11 };

    [Fact]
    public async Task ShowSimulationCommand_ActivatesSimulationPlaceholderPage_WhenWorkspaceCannotBeBuilt()
    {
        using var shell = new ShellViewModel();

        await shell.ShowSimulationCommand.ExecuteAsync(null);

        Assert.True(shell.IsSimulationActive);
        Assert.False(shell.IsWorkspaceActive);
        Assert.Same(shell.Simulation, shell.CurrentViewModel);
        Assert.True(shell.Simulation.HasNoWorkspace);
    }

    [Fact]
    public async Task ShowSimulationCommand_ShowsSimulationHostBeforeDelayedBuildCompletes()
    {
        using var shell = new ShellViewModel();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        shell.Simulation.RequestBuildWorkspaceAsync = async () =>
        {
            entered.SetResult();
            await release.Task;
        };

        var showTask = shell.ShowSimulationCommand.ExecuteAsync(null);
        await entered.Task;

        Assert.True(shell.IsSimulationActive);
        Assert.False(shell.IsWorkspaceActive);
        Assert.Same(shell.Simulation, shell.CurrentViewModel);
        Assert.True(shell.Simulation.IsBuildingWorkspace);

        release.SetResult();
        await showTask;
    }

    [Fact]
    public async Task ShowSimulationAsync_BindsWorkspaceIntoSimulationPage()
    {
        using var shell = new ShellViewModel();
        var sourceRevision = shell.FreeformHelper.SimulationWorkspaceSourceRevision;
        var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
        var workspaceViewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(
                grid,
                BuildTable(),
                NullDiffValue,
                SourceRevision: sourceRevision,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()));

        await shell.ShowSimulationAsync(workspaceViewModel);

        Assert.True(shell.IsSimulationActive);
        Assert.Same(shell.Simulation, shell.CurrentViewModel);
        Assert.Same(workspaceViewModel, shell.Simulation.CurrentWorkspace);
        Assert.False(shell.Simulation.IsWorkspaceStale);
    }

    [Fact]
    public void SimulationSafetyOverview_WhenWorkspaceHasNoCells_UsesNotRunAvailabilityStatus()
    {
        using var shell = new ShellViewModel();
        var grid = TestGeometryFactory.CreateLinearRegularGrid(Array.Empty<int>());
        var workspaceViewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(
                grid,
                new NotchTable(Array.Empty<NotchTableRow>()),
                NullDiffValue,
                SourceRevision: shell.FreeformHelper.SimulationWorkspaceSourceRevision,
                ActiveRegularPadIds: new HashSet<int>()));

        shell.Simulation.CurrentWorkspace = workspaceViewModel;

        var snapshot = Assert.IsType<SimulationScenarioSnapshot>(workspaceViewModel.GetSimulationScenarioSnapshot());
        Assert.True(snapshot.Result.IsSupported);
        Assert.False(snapshot.SafetyAudit.HasCells);
        Assert.Equal("EMS safety: no simulation cells.", workspaceViewModel.SimulationSafetySummaryText);
        Assert.Equal(
            ("Simulation not run", "Simulation not run"),
            (workspaceViewModel.SimulationSafetyStatusText, shell.FreeformHelper.SimulationSafetyOverviewStatusText));
        Assert.False(shell.FreeformHelper.HasSimulationSafetyOverviewAudit);
        Assert.False(shell.FreeformHelper.HasSimulationSafetyOverviewRisk);
    }

    [Fact]
    public async Task SimulationActiveWithoutWorkspace_PrewarmsArtifactsAfterWorkspaceSourceChanges()
    {
        using var shell = new ShellViewModel();

        await shell.ShowSimulationCommand.ExecuteAsync(null);

        Assert.True(shell.IsSimulationActive);
        Assert.True(shell.Simulation.HasNoWorkspace);

        var prewarmCount = 0;
        shell.Simulation.RequestPrewarmWorkspaceAsync = () =>
        {
            prewarmCount++;
            return Task.CompletedTask;
        };

        shell.FreeformHelper.NotifySimulationWorkspaceSourceChangedForTests();

        await WaitForConditionAsync(() => prewarmCount == 1);
        Assert.Equal(1, prewarmCount);
        Assert.True(shell.Simulation.HasNoWorkspace);
        Assert.Null(shell.Simulation.CurrentWorkspace);
    }

    [Fact]
    public async Task WorkspaceSourceChanges_PrewarmsSimulationArtifactsWhenWorkspaceNotCreated()
    {
        using var shell = new ShellViewModel();

        Assert.True(shell.IsWorkspaceActive);
        Assert.True(shell.Simulation.HasNoWorkspace);

        var prewarmCount = 0;
        shell.Simulation.RequestPrewarmWorkspaceAsync = () =>
        {
            prewarmCount++;
            return Task.CompletedTask;
        };

        shell.FreeformHelper.NotifySimulationWorkspaceSourceChangedForTests();

        await WaitForConditionAsync(() => prewarmCount == 1);
        Assert.Equal(1, prewarmCount);
        Assert.True(shell.Simulation.HasNoWorkspace);
        Assert.Null(shell.Simulation.CurrentWorkspace);
        Assert.True(shell.IsWorkspaceActive);
        Assert.False(shell.IsSimulationActive);
    }

    [Fact]
    public async Task WorkspaceSourceChanges_RebuildsStaleSimulationWorkspace_WhenWorkspaceIsPristine()
    {
        using var shell = new ShellViewModel();
        var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
        var initialWorkspace = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(
                grid,
                BuildTable(),
                NullDiffValue,
                SourceRevision: shell.FreeformHelper.SimulationWorkspaceSourceRevision,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()));
        shell.Simulation.CurrentWorkspace = initialWorkspace;

        var prewarmCount = 0;
        shell.Simulation.RequestPrewarmWorkspaceAsync = () =>
        {
            prewarmCount++;
            var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
            shell.Simulation.CurrentWorkspace = new SimulationWorkspaceViewModel(
                new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
                new SimulationWorkspaceSession(
                    grid,
                    BuildTable(),
                    NullDiffValue,
                    SourceRevision: shell.FreeformHelper.SimulationWorkspaceSourceRevision,
                    ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()));
            return Task.CompletedTask;
        };

        shell.FreeformHelper.NotifySimulationWorkspaceSourceChangedForTests();

        await WaitForConditionAsync(() => prewarmCount == 1);
        Assert.Equal(1, prewarmCount);
        Assert.NotSame(initialWorkspace, shell.Simulation.CurrentWorkspace);
    }

    [Fact]
    public async Task WorkspaceSourceChanges_BurstOnlyPrewarmsOnce()
    {
        using var shell = new ShellViewModel();
        var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
        shell.Simulation.CurrentWorkspace = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(
                grid,
                BuildTable(),
                NullDiffValue,
                SourceRevision: shell.FreeformHelper.SimulationWorkspaceSourceRevision,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()));

        var prewarmCount = 0;
        shell.Simulation.RequestPrewarmWorkspaceAsync = async () =>
        {
            Interlocked.Increment(ref prewarmCount);
            await Task.Delay(80);
        };

        shell.FreeformHelper.NotifySimulationWorkspaceSourceChangedForTests();
        shell.FreeformHelper.NotifySimulationWorkspaceSourceChangedForTests();
        shell.FreeformHelper.NotifySimulationWorkspaceSourceChangedForTests();

        await WaitForConditionAsync(() => Volatile.Read(ref prewarmCount) > 0);
        await Task.Delay(120, TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref prewarmCount));
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, int timeoutMs = 1200)
    {
        var started = Environment.TickCount64;
        while (!predicate())
        {
            if (Environment.TickCount64 - started > timeoutMs)
            {
                throw new TimeoutException("Condition was not satisfied within timeout.");
            }

            await Task.Delay(20);
        }
    }

    private static NotchTable BuildTable()
    {
        return new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, NullDiffValue, 0, 0),
                comment: "sample")
        });
    }
}
