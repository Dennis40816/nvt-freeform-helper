// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchProgressGateCharacterizationTests
{
    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportFinalUpdatesKeepStageRangeSummaryAndNormalizedDisplayAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var dispatch = new GenerationProgressContext();
        await GenerateAsync(vm, export: true);

        dispatch.Deliver(new NotchGenerationProgress(8, 4, 3, null, null, "Project rows", 2, 3));

        Assert.Equal(1.0 / 3.0, vm.NotchExportProgress, precision: 10);
        Assert.Equal("C v2.2 export [2/5]: Project rows 4/4, built rows=3", vm.NotchExportProgressText);
        Assert.Equal(vm.NotchExportProgressText, vm.NotchExportSummary);
        Assert.Equal(vm.NotchExportProgressText, vm.StatusText);

        dispatch.Deliver(new NotchGenerationProgress(1, -4, 5, null, null, " "));

        Assert.Equal(0.4, vm.NotchExportProgress);
        Assert.Equal("C v2.2 export [2/5]: Scan CAD 0/0, built rows=5", vm.NotchExportProgressText);
        Assert.Equal(vm.NotchExportProgressText, vm.NotchExportSummary);
        Assert.Equal(vm.NotchExportProgressText, vm.StatusText);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task Step5FinalUpdatesKeepFullRangeAndTextCallbackWithoutWritingSummaryAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var dispatch = new GenerationProgressContext();
        var messages = new List<string>();
        await GenerateAsync(vm, export: false, messages.Add);
        var summary = vm.NotchExportSummary;
        var status = vm.StatusText;

        dispatch.Deliver(new NotchGenerationProgress(8, 4, 3, null, null, "Project rows", 2, 3));

        Assert.Equal(2.0 / 3.0, vm.NotchExportProgress, precision: 10);
        Assert.Equal("Simulation workspace: Project rows 4/4, rows=3", vm.NotchExportProgressText);
        Assert.Equal(vm.NotchExportProgressText, messages[^1]);

        dispatch.Deliver(new NotchGenerationProgress(1, 0, 5, null, null));

        Assert.Equal(1.0, vm.NotchExportProgress);
        Assert.Equal("Simulation workspace: Scan CAD 0/0, rows=5", vm.NotchExportProgressText);
        Assert.Equal(vm.NotchExportProgressText, messages[^1]);
        Assert.Equal(summary, vm.NotchExportSummary);
        Assert.Equal(status, vm.StatusText);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueuedFinalUpdateAfterWorkspaceRevisionChangeDoesNotPublishAsync(bool export)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var dispatch = new GenerationProgressContext();
        var messages = new List<string>();
        await GenerateAsync(vm, export, messages.Add);
        var progress = vm.NotchExportProgress;
        var text = vm.NotchExportProgressText;
        var summary = vm.NotchExportSummary;
        var status = vm.StatusText;
        var messageCount = messages.Count;
        var sourceRevision = vm.SimulationWorkspaceSourceRevision;
        vm.NotifySimulationWorkspaceSourceChanged();
        Assert.NotEqual(sourceRevision, vm.SimulationWorkspaceSourceRevision);

        dispatch.Deliver(new NotchGenerationProgress(4, 4, 99, null, null, "Stale rows"));

        Assert.Equal(progress, vm.NotchExportProgress);
        Assert.Equal(text, vm.NotchExportProgressText);
        Assert.Equal(summary, vm.NotchExportSummary);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(messageCount, messages.Count);
    }

    private static async Task GenerateAsync(
        FreeformHelperViewModel vm,
        bool export,
        Action<string>? progressTextReporter = null)
    {
        if (export)
        {
            vm.PickSaveNotchPathAsync = static (_, _) => Task.FromResult<string?>(null);
            await vm.ExportNotchCommand.ExecuteAsync(null);
            Assert.Contains("canceled at stage 4", vm.NotchExportProgressText, StringComparison.Ordinal);
            return;
        }

        var session = await vm.CreateSimulationWorkspaceSessionAsync(false, progressTextReporter);
        Assert.NotNull(session);
    }

    // Hold Progress<T> posts at the public synchronization-context boundary while real generation runs.
    // All other posts retain the headless context and capture this wrapper in their continuations.
    private sealed class GenerationProgressContext : SynchronizationContext, IDisposable
    {
        private readonly SynchronizationContext _inner;
        private readonly Dispatcher _dispatcher = Assert.IsAssignableFrom<Dispatcher>(Dispatcher.FromThread(Thread.CurrentThread));
        private readonly ConcurrentQueue<SendOrPostCallback> _callbacks = new();

        public GenerationProgressContext()
        {
            _inner = Assert.IsAssignableFrom<SynchronizationContext>(Current);
            SetSynchronizationContext(this);
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            if (state is NotchGenerationProgress)
            {
                _callbacks.Enqueue(d);
                return;
            }

            _dispatcher.Post(() => RunContinuation(d, state));
        }

        private void RunContinuation(SendOrPostCallback callback, object? state)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                callback(state);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        public void Deliver(NotchGenerationProgress progress)
        {
            Assert.True(_callbacks.TryPeek(out var callback), "Real generation did not post any progress.");
            Assert.NotNull(callback);
            callback(progress);
        }

        public void Dispose() => SetSynchronizationContext(_inner);
    }
}
