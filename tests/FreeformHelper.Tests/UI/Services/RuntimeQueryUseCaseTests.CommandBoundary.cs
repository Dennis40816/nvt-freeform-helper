using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_QuerySimulation_WhenWorkspaceBuildFails_ReportsBuildFailedWithTheMessage()
    {
        using var shell = new ShellViewModel();
        shell.Simulation.RequestBuildWorkspaceAsync = static () => throw new InvalidOperationException("build exploded");

        var response = await new RuntimeQueryUseCase(shell).ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "simulation",
            Args: null));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal("BUILD_FAILED", response.Error!.Code);
        Assert.Equal("Simulation workspace build failed: build exploded", response.Error.Message);
        Assert.False(shell.Simulation.IsBuildingWorkspace);
    }
}
