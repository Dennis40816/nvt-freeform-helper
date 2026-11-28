// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadSpinnerProcessHostSendOrderingTests
{
    private static TimeSpan SafetyNetTimeout => TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task Show_WhileTheShowSendIsBlocked_ReturnsAsync()
    {
        var send = new BlockedShowSend();
        using var host = new CadLoadSpinnerProcessHost(send.Send);
        var owner = new Window();
        try
        {
            await send.AssertReturnsWhileBlockedAsync(() => host.Show(owner));
            Assert.False(host.LatestStateSyncTask.IsCompleted);
            Assert.Empty(send.Commands);
        }
        finally
        {
            send.Release();
        }

        await AwaitStateSyncAsync(host);
        Assert.Equal(CadLoadSpinnerIpcProtocol.ShowCommand, Assert.Single(send.Commands));
        Assert.Equal("Visible", host.GetVisibilityStateSnapshot().Effective);
    }

    [AvaloniaFact]
    public async Task Hide_WhileTheShowSendIsBlocked_ReturnsAndSendsShowThenHideAsync()
    {
        var send = new BlockedShowSend();
        using var host = new CadLoadSpinnerProcessHost(send.Send);
        var owner = new Window();
        try
        {
            await send.AssertReturnsWhileBlockedAsync(() => host.Show(owner));
            await send.AssertReturnsWhileBlockedAsync(host.Hide);
            Assert.Equal("Hidden", host.GetVisibilityStateSnapshot().Requested);
            Assert.Empty(send.Commands);
        }
        finally
        {
            send.Release();
        }

        await AwaitStateSyncAsync(host);
        Assert.Equal(new[] { CadLoadSpinnerIpcProtocol.ShowCommand, CadLoadSpinnerIpcProtocol.HideCommand }, send.Commands.ToArray());
        Assert.Equal("Hidden", host.GetVisibilityStateSnapshot().Effective);
    }

    [AvaloniaFact]
    public async Task Show_ThenImmediateHideWithARunningProcess_SendsBothInOrderAsync()
    {
        var commands = new ConcurrentQueue<string>();
        var warmup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new CadLoadSpinnerProcessHost(request =>
        {
            commands.Enqueue(request.Command);
            return true;
        }, () => Task.CompletedTask, () => false, warmup.Task);

        try
        {
            host.Show(new Window());
            host.Hide();
        }
        finally
        {
            warmup.TrySetResult();
        }

        await AwaitStateSyncAsync(host);
        Assert.Equal(new[] { CadLoadSpinnerIpcProtocol.ShowCommand, CadLoadSpinnerIpcProtocol.HideCommand }, commands.ToArray());
        var state = host.GetVisibilityStateSnapshot();
        Assert.Equal("Hidden", state.Requested);
        Assert.Equal("Hidden", state.Effective);
    }

    [AvaloniaFact]
    public async Task Dispose_WhileTheShowSendIsBlocked_ReturnsAndSendsShutdownAfterShowAsync()
    {
        var send = new BlockedShowSend();
        using var host = new CadLoadSpinnerProcessHost(send.Send);
        var owner = new Window();
        try
        {
            await send.AssertReturnsWhileBlockedAsync(() => host.Show(owner));
            await send.AssertReturnsWhileBlockedAsync(host.Dispose);
            Assert.Empty(send.Commands);
        }
        finally
        {
            send.Release();
        }

        await AwaitStateSyncAsync(host);
        Assert.Equal(new[] { CadLoadSpinnerIpcProtocol.ShowCommand, CadLoadSpinnerIpcProtocol.ShutdownCommand }, send.Commands.ToArray());
        var state = host.GetVisibilityStateSnapshot();
        Assert.Equal("Hidden", state.Requested);
        Assert.Equal("Hidden", state.Effective);
    }

    private static async Task AwaitStateSyncAsync(CadLoadSpinnerProcessHost host)
    {
        await host.LatestStateSyncTask.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);
    }

    private sealed class BlockedShowSend
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<string> Commands { get; } = new();

        public bool Send(CadLoadSpinnerRequest request)
        {
            if (request.Command == CadLoadSpinnerIpcProtocol.ShowCommand)
            {
                _entered.TrySetResult();
                TaskBlock.UntilComplete(_release.Task);
            }
            Commands.Enqueue(request.Command);
            return true;
        }

        public void Release() => _release.TrySetResult();

        public async Task AssertReturnsWhileBlockedAsync(Action action)
        {
            var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            // The watchdog releases a regressed UI call so its timeout is reported instead of hanging the runner.
            var probe = Task.Run(async () =>
            {
                try
                {
                    await _entered.Task.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);
                    await returned.Task.WaitAsync(SafetyNetTimeout, TestContext.Current.CancellationToken);
                    Assert.False(_release.Task.IsCompleted);
                }
                catch
                {
                    Release();
                    throw;
                }
            });

            try
            {
                action();
                returned.TrySetResult();
                await probe;
            }
            catch
            {
                Release();
                throw;
            }
        }
    }
}
