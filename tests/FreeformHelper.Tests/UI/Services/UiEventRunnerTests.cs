// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiEventRunnerTests
{
    [AvaloniaFact]
    public async Task Run_AsynchronousFailure_ReportsOnceWithoutDispatcherException()
    {
        var probe = new UiEventProbe();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("clipboard unavailable");
        var dispatcherFailures = 0;
        void OnUnhandled(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            dispatcherFailures++;
            e.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += OnUnhandled;
        try
        {
            probe.Runner.Run("Console.CopyAll", async _ =>
            {
                await release.Task;
                throw failure;
            });
            release.SetResult();
            var report = await probe.Reported.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.Equal("Console.CopyAll", report.Operation);
            Assert.Same(failure, report.Exception);
            Assert.Single(probe.Reports);
            Assert.Empty(probe.FallbackReports);
            Assert.Equal(0, dispatcherFailures);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= OnUnhandled;
        }
    }

    [Fact]
    public async Task RunAsync_PrimaryReporterThrows_FallsBackWithOriginalFailure()
    {
        var failure = new InvalidOperationException("operation failed");
        var primaryCalls = 0;
        (string Operation, Exception Exception)? fallback = null;
        var runner = new UiEventRunner((_, _) =>
        {
            primaryCalls++;
            throw new InvalidOperationException("reporter failed");
        }, (operation, exception) => fallback = (operation, exception));

        await runner.RunAsync("Settings.CascadeDetails", _ => Task.FromException(failure), TestContext.Current.CancellationToken);

        Assert.Equal(1, primaryCalls);
        Assert.Equal("Settings.CascadeDetails", fallback?.Operation);
        Assert.Same(failure, fallback?.Exception);
    }

    [Fact]
    public async Task RunAsync_BothReportersThrow_ObservesTheirFailures()
    {
        var runner = new UiEventRunner(
            (_, _) => throw new InvalidOperationException("primary failed"),
            (_, _) => throw new InvalidOperationException("fallback failed"));

        var exception = await Record.ExceptionAsync(() => runner.RunAsync(
            "NotchExport.Export", _ => Task.FromException(new InvalidOperationException("export failed")), TestContext.Current.CancellationToken));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RunAsync_MatchingCancellation_IsSilent()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var probe = new UiEventProbe();

        await probe.Runner.RunAsync("Window.ClosingSave", token => Task.FromCanceled(token), cancellation.Token);

        Assert.Empty(probe.Reports);
        Assert.Empty(probe.FallbackReports);
    }

    [Fact]
    public async Task RunAsync_ForeignCancellation_IsReported()
    {
        using var cancellation = new CancellationTokenSource();
        using var foreignCancellation = new CancellationTokenSource();
        cancellation.Cancel();
        foreignCancellation.Cancel();
        var probe = new UiEventProbe();

        await probe.Runner.RunAsync("Window.ClosingSave", _ => Task.FromCanceled(foreignCancellation.Token), cancellation.Token);

        Assert.Equal("Window.ClosingSave", Assert.Single(probe.Reports).Operation);
    }

    [AvaloniaFact]
    public void ShellDependencies_ShareCompositionRunner()
    {
        using var shell = new ShellViewModel();

        Assert.Same(shell.FreeformHelper.UiEvents, shell.UiEvents);
        Assert.Same(shell.UiEvents, shell.Dev.UiEvents);
        Assert.Same(shell.UiEvents, shell.FreeformHelper.CreateSettingsWindowViewModel().UiEvents);
    }
}
