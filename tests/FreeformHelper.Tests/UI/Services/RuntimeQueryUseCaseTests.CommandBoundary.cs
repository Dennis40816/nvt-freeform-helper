using System.Runtime.CompilerServices;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationSucceeds_ReturnsNullAndRunsTheOperationOnce()
    {
        var runs = 0;

        var failure = await RuntimeQueryUseCase.RunCommandOperationAsync(
            () =>
            {
                runs++;
                return Task.CompletedTask;
            },
            "TEST_FAILED",
            static ex => ex.Message);

        Assert.Null(failure);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationFailsLater_ReturnsTheFailureWithCodeAndMessage()
    {
        var failure = await RuntimeQueryUseCase.RunCommandOperationAsync(
            () => Task.FromException(new IOException("disk gone")),
            "TEST_FAILED",
            static ex => $"Failed: {ex.Message}");

        Assert.NotNull(failure);
        Assert.False(failure!.Ok);
        Assert.Equal("TEST_FAILED", failure.Error!.Code);
        Assert.Equal("Failed: disk gone", failure.Error.Message);
    }

    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationThrowsBeforeReturningTask_ReturnsTheFailure()
    {
        var failure = await RuntimeQueryUseCase.RunCommandOperationAsync(
            static () => throw new InvalidOperationException("not ready"),
            "TEST_FAILED",
            static ex => ex.Message);

        Assert.NotNull(failure);
        Assert.Equal("TEST_FAILED", failure!.Error!.Code);
        Assert.Equal("not ready", failure.Error.Message);
    }

    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationRunsOutOfMemory_PropagatesTheException()
    {
        var describeCalls = 0;
        var memory = CreateOutOfMemory();

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => RuntimeQueryUseCase.RunCommandOperationAsync(
            () => Task.FromException(memory),
            "TEST_FAILED",
            ex =>
            {
                describeCalls++;
                return ex.Message;
            }));

        Assert.Same(memory, thrown);
        Assert.Equal(0, describeCalls);
    }

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

    [Fact]
    public async Task ExecuteAsync_QuerySimulation_WhenWorkspaceBuildRunsOutOfMemory_PropagatesTheException()
    {
        using var shell = new ShellViewModel();
        var memory = CreateOutOfMemory();
        shell.Simulation.RequestBuildWorkspaceAsync = () => Task.FromException(memory);
        var useCase = new RuntimeQueryUseCase(shell);

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "simulation",
            Args: null)));

        Assert.Same(memory, thrown);
        Assert.False(shell.Simulation.IsBuildingWorkspace);
    }

    // The runtime reserves this type (CA2201), so the instance is created without running a constructor.
    private static OutOfMemoryException CreateOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));
}
