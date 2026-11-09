// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.IO.Pipes;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("RuntimeQueryIpcHost")]
public sealed class RuntimeQueryIpcHostRecoveryBehaviorTests
{
    [AvaloniaFact]
    public async Task StopAsync_ClosesPipeAndAllowsRestartWithNewShellAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        await RuntimeQueryIpcHost.StopAsync();
        using var original = new ShellViewModel { IsDevPageAvailable = true };
        using var replacement = new ShellViewModel { IsDevPageAvailable = true };
        original.FreeformHelper.EnableToFull = false;
        replacement.FreeformHelper.EnableToFull = false;
        await original.FreeformHelper.WaitForGridRebuildIdleAsync();
        await replacement.FreeformHelper.WaitForGridRebuildIdleAsync();

        try
        {
            RuntimeQueryIpcHost.Start(original);
            await RuntimeQueryIpcHost.StopAsync();
            var stopped = await WaitForPipeToCloseAsync();
            Assert.False(stopped.Ok);
            Assert.Equal("INSTANCE_NOT_RUNNING", stopped.Error?.Code);

            RuntimeQueryIpcHost.Start(replacement);
            var restarted = await EnableToFullAsync();

            Assert.True(restarted.Ok);
            Assert.True(replacement.FreeformHelper.EnableToFull);
            Assert.False(original.FreeformHelper.EnableToFull);
        }
        finally
        {
            await RuntimeQueryIpcHost.StopAsync();
        }
    }

    [AvaloniaFact]
    public async Task SendRequest_AfterClientDisconnectsWithoutPayload_StillExecutesNextCommandAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        await RuntimeQueryIpcHost.StopAsync();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        shell.FreeformHelper.EnableToFull = false;
        await shell.FreeformHelper.WaitForGridRebuildIdleAsync();

        try
        {
            RuntimeQueryIpcHost.Start(shell);
            await using (var abandoned = new NamedPipeClientStream(
                ".", RuntimeQueryProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await abandoned.ConnectAsync(3000, TestContext.Current.CancellationToken);
            }

            var response = await EnableToFullAsync();

            Assert.True(response.Ok);
            Assert.True(shell.FreeformHelper.EnableToFull);
        }
        finally
        {
            await RuntimeQueryIpcHost.StopAsync();
        }
    }

    private static Task<RuntimeQueryResponseEnvelope> EnableToFullAsync()
    {
        return Task.Run(() => RuntimeQueryCommandLine.SendRequest(
            new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "set-tofull",
                new Dictionary<string, string> { ["enable"] = "true" }), timeoutMs: 3000),
            TestContext.Current.CancellationToken);
    }

    private static async Task<RuntimeQueryResponseEnvelope> WaitForPipeToCloseAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            var response = await Task.Run(() => RuntimeQueryCommandLine.SendRequest(
                new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "status", Args: null), timeoutMs: 250), timeout.Token);
            if (response.Error?.Code == "INSTANCE_NOT_RUNNING")
            {
                return response;
            }

            await Task.Yield();
        }
    }
}
