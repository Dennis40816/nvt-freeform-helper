using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationHostViewModelTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] SimulationDiffIndices = { 10, 11 };

    [Fact]
    public async Task PrewarmWorkspaceAsync_WhenWorkspaceIsStaleAndPristine_InvokesRequest()
    {
        var host = new SimulationHostViewModel();
        host.CurrentWorkspace = BuildWorkspace(sourceRevision: 1);
        host.UpdateSourceRevision(2);

        var invokeCount = 0;
        host.RequestPrewarmWorkspaceAsync = () =>
        {
            invokeCount++;
            return Task.CompletedTask;
        };

        await host.PrewarmWorkspaceAsync();

        Assert.Equal(1, invokeCount);
    }

    [Fact]
    public async Task PrewarmWorkspaceAsync_WhenWorkspaceIsMissing_InvokesRequest()
    {
        var host = new SimulationHostViewModel();

        var invokeCount = 0;
        host.RequestPrewarmWorkspaceAsync = () =>
        {
            invokeCount++;
            return Task.CompletedTask;
        };

        await host.PrewarmWorkspaceAsync();

        Assert.Equal(1, invokeCount);
    }

    [Fact]
    public async Task PrewarmWorkspaceAsync_WhenWorkspaceHasManualOverrides_DoesNotInvokeRequest()
    {
        var host = new SimulationHostViewModel();
        var workspace = BuildWorkspace(sourceRevision: 1);
        workspace.ApplyRegularOverride(regularPadId: 0, value: 12);
        host.CurrentWorkspace = workspace;
        host.UpdateSourceRevision(2);

        var invokeCount = 0;
        host.RequestPrewarmWorkspaceAsync = () =>
        {
            invokeCount++;
            return Task.CompletedTask;
        };

        await host.PrewarmWorkspaceAsync();

        Assert.Equal(0, invokeCount);
    }

    [Fact]
    public void HasWorkspaceFailureOverlay_WhenWorkspaceAndFailureExist_ReturnsTrue()
    {
        var host = new SimulationHostViewModel
        {
            CurrentWorkspace = BuildWorkspace(sourceRevision: 1),
        };

        host.SetBuildFailure("Simulation blocked.");

        Assert.True(host.HasWorkspaceFailureOverlay);
    }

    [Fact]
    public async Task EnsureWorkspaceAsync_ReportsBuildingStateWhileRequestIsRunning()
    {
        var host = new SimulationHostViewModel();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        host.RequestBuildWorkspaceAsync = async () =>
        {
            entered.SetResult();
            await release.Task;
        };

        var buildTask = host.EnsureWorkspaceAsync();
        await entered.Task;

        Assert.True(host.IsBuildingWorkspace);
        Assert.True(host.HasBuildProgressOverlay);
        Assert.False(host.CanOpenSimulation);
        Assert.Equal("Preparing simulation workspace...", host.BuildProgressText);

        host.ReportBuildProgress("Simulation workspace: Build canonical candidates 32/4838, rows=12");
        Assert.Equal("Simulation workspace: Build canonical candidates 32/4838, rows=12", host.BuildProgressText);

        release.SetResult();
        await buildTask;

        Assert.False(host.IsBuildingWorkspace);
        Assert.False(host.HasBuildProgressOverlay);
        Assert.True(host.CanOpenSimulation);
        Assert.Equal(string.Empty, host.BuildProgressText);
    }

    private static SimulationWorkspaceViewModel BuildWorkspace(int sourceRevision)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(
                grid,
                BuildTable(),
                NullDiffValue,
                SourceRevision: sourceRevision,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()));
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
