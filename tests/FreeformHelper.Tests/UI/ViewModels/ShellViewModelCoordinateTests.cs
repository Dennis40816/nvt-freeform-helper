using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShellViewModelCoordinateTests
{
    private static readonly string[] LayerL1 = ["L1"];
    private static readonly string[] AaAndL1Layers = ["AA.drawing", "L1"];

    [Theory]
    [InlineData("coordinate")]
    [InlineData("simulation")]
    [InlineData("simulation-prewarm")]
    public async Task WorkspaceBuild_WhenShellDisposedWhileInFlight_CompletesWithoutExceptionOrErrorLog(string buildKind)
    {
        var previousConfiguration = LogManager.Configuration;
        using var errors = new MemoryTarget { Layout = "${message} ${exception:format=ToString}" };
        var configuration = new LoggingConfiguration();
        configuration.AddRule(LogLevel.Error, LogLevel.Fatal, errors);
        LogManager.Configuration = configuration;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var shell = new ShellViewModel { WorkspaceBuildPauseForTests = release.Task };
            var buildTask = buildKind switch
            {
                "coordinate" => shell.CoordinatePlanner.PrewarmWorkspaceAsync(),
                "simulation" => shell.Simulation.EnsureWorkspaceAsync(),
                "simulation-prewarm" => shell.Simulation.PrewarmWorkspaceAsync(),
                _ => throw new ArgumentOutOfRangeException(nameof(buildKind)),
            };
            Assert.False(buildTask.IsCompleted);

            shell.Dispose();
            shell.Dispose();
            release.SetResult();
            var exception = await Record.ExceptionAsync(() => buildTask);

            Assert.Null(exception);
            Assert.Empty(errors.Logs);
        }
        finally
        {
            release.TrySetResult();
            LogManager.Configuration = previousConfiguration;
        }
    }

    [Fact]
    public async Task ShowCoordinateAsync_BindsWorkspaceIntoCoordinatePage()
    {
        using var shell = new ShellViewModel();
        var workspaceViewModel = CreateWorkspace(shell.FreeformHelper.WorkspaceDerivedSourceRevision);

        await shell.ShowCoordinateAsync(workspaceViewModel);

        Assert.True(shell.IsCoordinateActive);
        Assert.False(shell.IsWorkspaceActive);
        Assert.Same(shell.CoordinatePlanner, shell.CurrentViewModel);
        Assert.Same(workspaceViewModel, shell.CoordinatePlanner.CurrentWorkspace);
        Assert.False(shell.CoordinatePlanner.IsWorkspaceStale);
    }

    [Fact]
    public async Task ShowCoordinateCommand_HealthyWorkspace_BuildsOnlyOnFirstSelection()
    {
        using var shell = new ShellViewModel();
        var workspace = CreateWorkspace(shell.FreeformHelper.WorkspaceDerivedSourceRevision);
        var buildCount = 0;
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = () =>
        {
            buildCount++;
            shell.CoordinatePlanner.CurrentWorkspace = workspace;
            return Task.CompletedTask;
        };
        Assert.Null(shell.CoordinatePlanner.CurrentWorkspace);
        Assert.Equal(0, buildCount);

        await shell.ShowCoordinateCommand.ExecuteAsync(null);
        await shell.ShowCoordinateCommand.ExecuteAsync(null);
        shell.ShowWorkspaceCommand.Execute(null);
        await shell.ShowCoordinateCommand.ExecuteAsync(null);

        Assert.Equal(1, buildCount);
        Assert.Same(workspace, shell.CoordinatePlanner.CurrentWorkspace);
    }

    [Fact]
    public async Task ShowCoordinateCommand_DelayedBuild_KeepsPreviousPageUntilCompletion()
    {
        using var shell = new ShellViewModel();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = async () =>
        {
            entered.SetResult();
            await release.Task;
        };

        var showTask = shell.ShowCoordinateCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.True(shell.IsWorkspaceActive);
            Assert.False(shell.IsCoordinateActive);
            Assert.Same(shell.FreeformHelper, shell.CurrentViewModel);
        }
        finally
        {
            release.TrySetResult();
            await showTask;
        }
    }

    [Fact]
    public async Task ShowCoordinateCommand_RepeatedSelectionWithoutWorkspace_RetriesBuild()
    {
        using var shell = new ShellViewModel();
        var buildCount = 0;
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = () =>
        {
            buildCount++;
            return Task.CompletedTask;
        };
        await shell.ShowCoordinateCommand.ExecuteAsync(null);

        await shell.ShowCoordinateCommand.ExecuteAsync(null);

        Assert.Equal(2, buildCount);
        Assert.True(shell.CoordinatePlanner.HasNoWorkspace);
    }

    [Fact]
    public async Task ShowCoordinateCommand_RepeatedSelectionWithStaleWorkspace_RebuildsWorkspace()
    {
        using var shell = new ShellViewModel();
        var revision = shell.FreeformHelper.WorkspaceDerivedSourceRevision;
        await shell.ShowCoordinateAsync(CreateWorkspace(revision));
        var refreshedWorkspace = CreateWorkspace(revision + 1);
        shell.CoordinatePlanner.UpdateSourceRevision(revision + 1);
        var buildCount = 0;
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = () =>
        {
            buildCount++;
            shell.CoordinatePlanner.CurrentWorkspace = refreshedWorkspace;
            return Task.CompletedTask;
        };

        await shell.ShowCoordinateCommand.ExecuteAsync(null);

        Assert.Equal(1, buildCount);
        Assert.Same(refreshedWorkspace, shell.CoordinatePlanner.CurrentWorkspace);
        Assert.False(shell.CoordinatePlanner.IsWorkspaceStale);
    }

    [Fact]
    public async Task ShowCoordinateAsync_PropagatesCoordinatePreferencesToFreeformSnapshot()
    {
        using var shell = new ShellViewModel();
        var sourceRevision = shell.FreeformHelper.WorkspaceDerivedSourceRevision;
        var workspaceViewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                BuildCadPads(),
                AaAndL1Layers,
                sourceRevision,
                DefaultMachineWidth: 2d,
                DefaultMachineHeight: 1d,
                DefaultPixelWidth: 2,
                DefaultPixelHeight: 1,
                PreferredActiveAreaOutlineLayerName: "AA.drawing"));

        await shell.ShowCoordinateAsync(workspaceViewModel);

        workspaceViewModel.PixelWidth = 123;
        workspaceViewModel.PixelHeight = 456;
        var preferredBoundsOption = workspaceViewModel.AaBoundsOptions.First(static option =>
            option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerBoundsMode.LayerBounds &&
            string.Equals(option.LayerName, "AA.drawing", StringComparison.OrdinalIgnoreCase));
        workspaceViewModel.SelectedAaBoundsOption = preferredBoundsOption;

        Assert.Equal(123m, shell.FreeformHelper.CoordinatePixelWidth);
        Assert.Equal(456m, shell.FreeformHelper.CoordinatePixelHeight);
        Assert.Equal("AA.drawing", shell.FreeformHelper.CoordinatePreferredAaOutlineLayerName);
    }

    private static CoordinatePlannerWorkspaceViewModel CreateWorkspace(int sourceRevision) => new(
        new CoordinatePlannerWorkspaceUseCase(),
        new CoordinatePlannerWorkspaceSession(
            BuildGrid(),
            BuildCadPads(),
            LayerL1,
            sourceRevision,
            DefaultMachineWidth: 2d,
            DefaultMachineHeight: 1d,
            DefaultPixelWidth: 2,
            DefaultPixelHeight: 1));

    private static RegularGrid BuildGrid()
    {
        return TestGeometryFactory.CreateRegularGrid(1, 2);
    }

    private static CadPad[] BuildCadPads()
    {
        return [TestGeometryFactory.CreateCadPad(1, "L1", 0.1d, 0.1d, 0.9d, 0.9d)];
    }
}
