// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("RuntimeQueryIpcHost")]
public sealed class RuntimeQueryIpcHostStartupTests
{
    [AvaloniaFact]
    public async Task Start_WhenCalledTwice_KeepsOriginalShellAndStopsItsPipeAsync()
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
            RuntimeQueryIpcHost.Start(replacement);
            var response = await Task.Run(() => RuntimeQueryCommandLine.SendRequest(
                new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "set-tofull",
                    new Dictionary<string, string> { ["enable"] = "true" }),
                timeoutMs: 3000), TestContext.Current.CancellationToken);

            Assert.True(response.Ok);
            Assert.True(original.FreeformHelper.EnableToFull);
            Assert.False(replacement.FreeformHelper.EnableToFull);

            await RuntimeQueryIpcHost.StopAsync();
            var stoppedResponse = await Task.Run(() => RuntimeQueryCommandLine.SendRequest(
                new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "status", Args: null),
                timeoutMs: 250), TestContext.Current.CancellationToken);

            Assert.False(stoppedResponse.Ok);
            Assert.Equal("INSTANCE_NOT_RUNNING", stoppedResponse.Error?.Code);
        }
        finally
        {
            await RuntimeQueryIpcHost.StopAsync();
        }
    }
}
