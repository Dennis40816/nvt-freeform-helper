using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the spinner host does when the spinner process has ended: the next Show starts a new process
/// and then sends the Show request. Before, nothing started it again and every Show failed until the program restarted.
/// </summary>
public sealed class CadLoadSpinnerProcessHostRestartTests
{
    // Long on purpose: it only turns a real hang into a failure.
    private static TimeSpan SafetyNetTimeout => TimeSpan.FromSeconds(30);

    private sealed class FakeProcess
    {
        private int _exited;
        private int _starts;
        private readonly List<string> _commands = [];

        public FakeProcess(bool exited)
        {
            _exited = exited ? 1 : 0;
        }

        public int Starts => Volatile.Read(ref _starts);

        public bool HasExited => Volatile.Read(ref _exited) == 1;

        public IReadOnlyList<string> Commands
        {
            get
            {
                lock (_commands)
                {
                    return [.. _commands];
                }
            }
        }

        public bool Send(CadLoadSpinnerRequest request)
        {
            lock (_commands)
            {
                _commands.Add(request.Command);
            }

            return true;
        }

        public void Restore()
        {
            Volatile.Write(ref _exited, 0);
        }

        public Task StartAsync()
        {
            Interlocked.Increment(ref _starts);
            Volatile.Write(ref _exited, 0);
            return Task.CompletedTask;
        }
    }

    private static async Task AwaitStateSyncAsync(Task stateSync)
    {
        await stateSync.WaitAsync(SafetyNetTimeout);
    }

    [AvaloniaFact]
    public async Task Show_WhenTheProcessHasExited_StartsANewProcessAndSendsShow()
    {
        var process = new FakeProcess(exited: true);
        using var host = new CadLoadSpinnerProcessHost(process.Send, process.StartAsync, () => process.HasExited);

        host.Show(new Window());
        await AwaitStateSyncAsync(host.GetLatestStateSyncTask());

        Assert.Equal(1, process.Starts);
        Assert.Contains(CadLoadSpinnerIpcProtocol.ShowCommand, process.Commands);
        Assert.Equal("Visible", host.GetVisibilityStateSnapshot().Effective);
    }

    [AvaloniaFact]
    public async Task Show_WhileTheWarmupStillRuns_DoesNotStartASecondProcess()
    {
        // The spinner process ended during its own start-up. The start task still runs and cleans up by itself.
        var process = new FakeProcess(exited: true);
        var warmup = new TaskCompletionSource();
        using var host = new CadLoadSpinnerProcessHost(process.Send, process.StartAsync, () => process.HasExited, warmup.Task);

        host.Show(new Window());
        var stateSync = host.GetLatestStateSyncTask();
        Assert.False(stateSync.IsCompleted);

        Assert.Equal(0, process.Starts);
        warmup.SetResult();
        process.Restore();
        await AwaitStateSyncAsync(stateSync);

        Assert.Equal(0, process.Starts);
        Assert.Contains(CadLoadSpinnerIpcProtocol.ShowCommand, process.Commands);
    }

    [AvaloniaFact]
    public void Show_WhenTheProcessIsRunning_SendsShowWithoutStartingAProcess()
    {
        var process = new FakeProcess(exited: false);
        using var host = new CadLoadSpinnerProcessHost(process.Send, process.StartAsync, () => process.HasExited);

        host.Show(new Window());

        Assert.Equal(0, process.Starts);
        Assert.Contains(CadLoadSpinnerIpcProtocol.ShowCommand, process.Commands);
        Assert.Equal("Visible", host.GetVisibilityStateSnapshot().Effective);
    }
}
