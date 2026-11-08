// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadSpinnerProcessHostRetryBehaviorTests
{
    [AvaloniaFact]
    public async Task Show_WhenTheFirstSendReturnsFalse_RetriesAndBecomesVisibleAsync()
    {
        var commands = new ConcurrentQueue<string>();
        var showAttempts = 0;
        using var host = new CadLoadSpinnerProcessHost(request =>
        {
            commands.Enqueue(request.Command);
            return request.Command != CadLoadSpinnerIpcProtocol.ShowCommand ||
                Interlocked.Increment(ref showAttempts) > 1;
        });

        host.Show(new Window());
        await host.LatestStateSyncTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { CadLoadSpinnerIpcProtocol.ShowCommand, CadLoadSpinnerIpcProtocol.ShowCommand },
            commands.ToArray());
        var state = host.GetVisibilityStateSnapshot();
        Assert.Equal("Visible", state.Requested);
        Assert.Equal("Visible", state.Effective);
    }
}
