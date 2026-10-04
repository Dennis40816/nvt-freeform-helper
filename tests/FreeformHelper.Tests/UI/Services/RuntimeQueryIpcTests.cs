using System.IO.Pipes;
using System.Reflection;
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
        using var shell = new ShellViewModel();
        try
        {
            RuntimeQueryIpcHost.Start(shell);

            var stopTask = RuntimeQueryIpcHost.StopAsync();
            var completed = await Task.WhenAny(stopTask, Task.Delay(1500));

            Assert.Same(stopTask, completed);
            await stopTask;
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
        using var shell = new ShellViewModel();
        await using var client = new NamedPipeClientStream(
            ".",
            RuntimeQueryProtocol.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            RuntimeQueryIpcHost.Start(shell);
            await client.ConnectAsync(1500);

            var stopTask = RuntimeQueryIpcHost.StopAsync();
            var completed = await Task.WhenAny(stopTask, Task.Delay(1500));

            Assert.Same(stopTask, completed);
            await stopTask;
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

        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, leaveOpen: true);
            _ = await reader.ReadLineAsync();
            await Task.Delay(500);
        });
        var request = new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "status", Args: null);

        var response = RuntimeQueryCommandLine.SendRequest(request, timeoutMs: 100);

        Assert.False(response.Ok);
        Assert.Equal("IPC_TIMEOUT", response.Error?.Code);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [AvaloniaFact]
    public async Task SendRequest_WhenRuntimeQueryThrows_ReturnsIpcErrorEnvelope()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        await RuntimeQueryIpcHost.StopAsync();
        using var shell = new ShellViewModel();
        var projectFile = new ProjectFile();
        projectFile.Settings.Notch.NullValue = ushort.MaxValue + 1;
        var projectFileField = typeof(FreeformHelperViewModel)
            .GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(projectFileField);
        projectFileField!.SetValue(shell.FreeformHelper, projectFile);

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
