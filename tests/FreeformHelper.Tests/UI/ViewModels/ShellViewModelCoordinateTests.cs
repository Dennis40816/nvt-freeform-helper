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
        var sourceRevision = shell.FreeformHelper.WorkspaceDerivedSourceRevision;
        var workspaceViewModel = new CoordinatePlannerWorkspaceViewModel(
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

        await shell.ShowCoordinateAsync(workspaceViewModel);

        Assert.True(shell.IsCoordinateActive);
        Assert.False(shell.IsWorkspaceActive);
        Assert.Same(shell.CoordinatePlanner, shell.CurrentViewModel);
        Assert.Same(workspaceViewModel, shell.CoordinatePlanner.CurrentWorkspace);
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

    private static RegularGrid BuildGrid()
    {
        return TestGeometryFactory.CreateRegularGrid(1, 2);
    }

    private static CadPad[] BuildCadPads()
    {
        return [TestGeometryFactory.CreateCadPad(1, "L1", 0.1d, 0.1d, 0.9d, 0.9d)];
    }
}
