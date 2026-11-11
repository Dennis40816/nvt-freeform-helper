using System.IO.Pipes;
using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("RuntimeQueryIpcHost")]
public sealed class RuntimeQueryIpcTests
{
    [Fact]
    public async Task StopAsync_WhenHostNotStarted_Completes()
    {
        await RuntimeQueryIpcHost.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WhenHostStarted_CompletesWithinTimeout()
    {
        await RuntimeQueryIpcHost.StopAsync();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        try
        {
            RuntimeQueryIpcHost.Start(shell);

            var stopTask = RuntimeQueryIpcHost.StopAsync();
            await stopTask.WaitAsync(TimeSpan.FromMilliseconds(1500), TestContext.Current.CancellationToken);
        }
        finally
        {
            await RuntimeQueryIpcHost.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_WhenClientConnectedWithoutRequest_CompletesWithinTimeout()
    {
        await RuntimeQueryIpcHost.StopAsync();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        await using var client = new NamedPipeClientStream(
            ".",
            RuntimeQueryProtocol.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            RuntimeQueryIpcHost.Start(shell);
            await client.ConnectAsync(1500, TestContext.Current.CancellationToken);

            var stopTask = RuntimeQueryIpcHost.StopAsync();
            await stopTask.WaitAsync(TimeSpan.FromMilliseconds(1500), TestContext.Current.CancellationToken);
        }
        finally
        {
            await RuntimeQueryIpcHost.StopAsync();
        }
    }

    [Fact]
    public async Task SendRequest_WhenServerAcceptsButDoesNotRespond_ReturnsTimeout()
    {
        await RuntimeQueryIpcHost.StopAsync();
        await using var server = new NamedPipeServerStream(
            RuntimeQueryProtocol.PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
            using var reader = new StreamReader(server, leaveOpen: true);
            _ = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            await releaseServer.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
        var request = new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "status", Args: null);

        try
        {
            var response = RuntimeQueryCommandLine.SendRequest(request, timeoutMs: 100);

            Assert.False(response.Ok);
            Assert.Equal("IPC_TIMEOUT", response.Error?.Code);
        }
        finally
        {
            releaseServer.TrySetResult();
            await serverTask.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task SendRequest_WhenRuntimeQueryThrows_ReturnsIpcErrorEnvelope()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        await RuntimeQueryIpcHost.StopAsync();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var projectFile = new ProjectFile();
        projectFile.Settings.Notch.NullValue = ushort.MaxValue + 1;
        FreeformHelperViewModelPrivateAccess.ReplaceProjectFile(shell.FreeformHelper, projectFile);

        try
        {
            RuntimeQueryIpcHost.Start(shell);
            var response = await Task.Run(() => RuntimeQueryCommandLine.SendRequest(
                new RuntimeQueryRequest(
                    RuntimeQueryProtocol.Version,
                    "notch-validation",
                    new Dictionary<string, string> { ["regular-id"] = "100" }),
                timeoutMs: 3000));

            Assert.False(response.Ok);
            Assert.Equal("IPC_ERROR", response.Error?.Code);
            Assert.Equal("NullValue must be in [0,65535].", response.Error?.Message);
        }
        finally
        {
            await RuntimeQueryIpcHost.StopAsync();
        }
    }
}

[CollectionDefinition("RuntimeQueryIpcHost", DisableParallelization = true)]
public sealed class RuntimeQueryIpcHostCollectionDefinition;
