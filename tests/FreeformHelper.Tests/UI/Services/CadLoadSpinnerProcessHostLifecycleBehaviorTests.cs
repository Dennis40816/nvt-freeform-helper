// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadSpinnerProcessHostLifecycleBehaviorTests
{
    private static TimeSpan SafetyNetTimeout => TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task Hide_WhileTheProcessStarts_HidesTheSpinnerAfterThePendingShowAsync()
    {
        var process = new PendingSpinner();
        using var host = process.CreateHost();

        host.Show(new Window());
        var showSync = host.LatestStateSyncTask;
        try
        {
            await process.Started.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);
            host.Hide();
        }
        finally
        {
            process.CompleteWarmup();
        }

        await showSync.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);
        await host.LatestStateSyncTask.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { CadLoadSpinnerIpcProtocol.ShowCommand, CadLoadSpinnerIpcProtocol.HideCommand },
            process.Commands.ToArray());
        var state = host.GetVisibilityStateSnapshot();
        Assert.Equal("Hidden", state.Requested);
        Assert.Equal("Hidden", state.Effective);
    }

    [AvaloniaFact]
    public async Task Dispose_WhileTheProcessStarts_ShutsDownWithoutSendingThePendingShowAsync()
    {
        var process = new PendingSpinner();
        using var host = process.CreateHost();

        host.Show(new Window());
        var showSync = host.LatestStateSyncTask;
        try
        {
            await process.Started.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);
            host.Dispose();
        }
        finally
        {
            process.CompleteWarmup();
        }

        await showSync.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(CadLoadSpinnerIpcProtocol.ShutdownCommand, Assert.Single(process.Commands));
        var state = host.GetVisibilityStateSnapshot();
        Assert.Equal("Hidden", state.Requested);
        Assert.Equal("Hidden", state.Effective);
    }

    [AvaloniaFact]
    public void Dispose_WhenShutdownIsRejected_RemainsDisposedAndDoesNotSendAgain()
    {
        var commands = new List<string>();
        using var host = new CadLoadSpinnerProcessHost(request =>
        {
            commands.Add(request.Command);
            return false;
        });
        var owner = new Window();

        host.Dispose();
        host.Hide();
        host.Dispose();

        Assert.Equal(CadLoadSpinnerIpcProtocol.ShutdownCommand, Assert.Single(commands));
        Assert.Throws<ObjectDisposedException>(() => host.Show(owner));
        Assert.Throws<ObjectDisposedException>(host.Warmup);
        var state = host.GetVisibilityStateSnapshot();
        Assert.Equal("Hidden", state.Requested);
        Assert.Equal("Hidden", state.Effective);
    }

    private sealed class PendingSpinner
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _running;

        public Task Started => _started.Task;
        public ConcurrentQueue<string> Commands { get; } = new();

        public CadLoadSpinnerProcessHost CreateHost()
        {
            return new CadLoadSpinnerProcessHost(Send, StartAsync, () => Volatile.Read(ref _running) == 0);
        }

        public void CompleteWarmup()
        {
            _ready.TrySetResult();
        }

        private Task StartAsync()
        {
            Volatile.Write(ref _running, 1);
            _started.TrySetResult();
            return _ready.Task.WaitAsync(CancellationToken.None);
        }

        private bool Send(CadLoadSpinnerRequest request)
        {
            Commands.Enqueue(request.Command);
            return true;
        }
    }
}
