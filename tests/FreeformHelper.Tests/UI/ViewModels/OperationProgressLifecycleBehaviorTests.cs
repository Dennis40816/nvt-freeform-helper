// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using Xunit;
using static FreeformHelper.Tests.LoadingModeTestScope;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class OperationProgressLifecycleBehaviorTests
{
    private static readonly double[] _resetThenComplete = [0.0, 1.0];
    [AvaloniaTheory]
    [InlineData("Overlap")]
    [InlineData("Match")]
    [InlineData("Mapping")]
    public async Task ProgressCommand_Success_RaisesOldNamesInBusyProgressIdleOrder(string kind)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var operation = new ProgressOperation(await files.CreateLoadedAsync(), kind);
        using var changes = operation.Observe();

        await operation.Command.ExecuteAsync(null);

        changes.AssertSuccessful(operation);
    }

    [AvaloniaTheory]
    [InlineData("Overlap")]
    [InlineData("Match")]
    [InlineData("Mapping")]
    public async Task ProgressCommand_SecondSuccess_ResetsProgressBeforeReportingAgain(string kind)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var operation = new ProgressOperation(await files.CreateLoadedAsync(), kind);
        await operation.Command.ExecuteAsync(null);
        using var changes = operation.Observe();

        await operation.Command.ExecuteAsync(null);

        changes.AssertSuccessful(operation, repeated: true);
    }

    [AvaloniaTheory]
    [InlineData("Overlap", "DXF check: no DXF loaded.")]
    [InlineData("Match", "Step 1: import DXF and build grid first.")]
    [InlineData("Mapping", "Diagnostics: no CAD output pads.")]
    public async Task ProgressCommand_NoDxf_DoesNotEnterBusyOrChangeProgress(string kind, string status)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        await vm.RebuildGridCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.RegularPads);
        Assert.Empty(vm.CadPads);
        var operation = new ProgressOperation(vm, kind);
        using var changes = operation.Observe();
        var before = operation.Progress;

        await operation.Command.ExecuteAsync(null);

        Assert.Empty(changes.Changes);
        Assert.False(operation.Busy);
        Assert.Equal(before, operation.Progress);
        Assert.Equal(status, vm.StatusText);
    }

    [AvaloniaTheory]
    [InlineData("Overlap", "DXF check: no CAD output pads (all layers off).")]
    [InlineData("Match", "Step 1: import DXF and build grid first.")]
    [InlineData("Mapping", "Diagnostics: no CAD output pads.")]
    public async Task ProgressCommand_NoCadOutput_LeavesProgressUnchangedWithoutBusy(string kind, string status)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        var operation = new ProgressOperation(vm, kind);
        await operation.Command.ExecuteAsync(null);
        vm.SelectedGridAlignmentOption = Assert.Single(vm.GridAlignmentOptions,
            option => option.Value == GridAlignmentMode.FromPanelAa);
        Assert.Single(vm.LayerToggles).IsSelected = false;
        await vm.RebuildGridCommand.ExecuteAsync(null);
        Assert.Empty(vm.CadPads);
        var before = operation.Progress;
        using var changes = operation.Observe();

        await operation.Command.ExecuteAsync(null);

        Assert.Empty(changes.Changes);
        Assert.False(operation.Busy);
        Assert.Equal(before, operation.Progress);
        Assert.Equal(status, vm.StatusText);
    }

    [AvaloniaTheory]
    [InlineData("Match", "Step 1: import DXF and build grid first.")]
    [InlineData("Mapping", "Step 4: import DXF and build grid first.")]
    public async Task ProgressCommand_NoGrid_DoesNotEnterBusyOrChangeProgress(string kind, string status)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        vm.SelectedRegularSourceModeOption = Assert.Single(vm.RegularSourceModeOptions,
            option => option.Value == RegularSourceMode.FromDxfLayer);
        vm.SelectedRegularSourceLayerOption = Assert.Single(vm.RegularSourceLayerOptions,
            option => option.Name is null);
        await vm.RebuildGridCommand.ExecuteAsync(null);
        Assert.Empty(vm.RegularPads);
        var operation = new ProgressOperation(vm, kind);
        using var changes = operation.Observe();
        var before = operation.Progress;

        await operation.Command.ExecuteAsync(null);

        Assert.Empty(changes.Changes);
        Assert.False(operation.Busy);
        Assert.Equal(before, operation.Progress);
        Assert.Equal(status, vm.StatusText);
    }

    [AvaloniaFact]
    public async Task CheckDxfQualityCommand_NoGrid_StillChecksLoadedCad()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        vm.SelectedRegularSourceModeOption = Assert.Single(vm.RegularSourceModeOptions,
            option => option.Value == RegularSourceMode.FromDxfLayer);
        vm.SelectedRegularSourceLayerOption = Assert.Single(vm.RegularSourceLayerOptions,
            option => option.Name is null);
        await vm.RebuildGridCommand.ExecuteAsync(null);
        Assert.Empty(vm.RegularPads);
        var operation = new ProgressOperation(vm, "Overlap");
        using var changes = operation.Observe();

        await vm.CheckDxfQualityCommand.ExecuteAsync(null);

        changes.AssertSuccessful(operation);
        Assert.Equal("DXF check: no overlaps detected.", vm.StatusText);
    }

    [AvaloniaTheory]
    [InlineData("Overlap", "DXF overlap check failed")]
    [InlineData("Match", "Match failed")]
    [InlineData("Mapping", "Mapping analyze failed")]
    public async Task ProgressCommand_ResetNotificationThrows_FinallyCompletesProgressAndClearsBusy(string kind, string error)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        var operation = new ProgressOperation(vm, kind);
        await operation.Command.ExecuteAsync(null);
        using var changes = operation.Observe();
        using var failure = OnNextChangeThrowingExpectedException(vm, operation.ProgressName,
            new InvalidOperationException("Synthetic progress failure."));

        await operation.Command.ExecuteAsync(null);

        Assert.True(failure.WasInvoked);
        Assert.Equal(new[] { operation.BusyName, operation.ProgressName, operation.ProgressName, operation.BusyName },
            changes.Changes.Select(change => change.Name));
        Assert.Equal(_resetThenComplete, changes.Changes.Where(change => change.Name == operation.ProgressName)
            .Select(change => change.Progress));
        Assert.False(operation.Busy);
        Assert.Equal(1.0, operation.Progress);
        Assert.Contains(error, vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Synthetic progress failure.", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData("Overlap")]
    [InlineData("Match")]
    public async Task ClearStep1ResultCommand_CompletedProgressOperation_PreservesBusyAndProgress(string kind)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        var operation = new ProgressOperation(vm, kind);
        await operation.Command.ExecuteAsync(null);
        using var changes = operation.Observe();

        vm.ClearStep1ResultCommand.Execute(null);

        Assert.Empty(changes.Changes);
        Assert.False(operation.Busy);
        Assert.Equal(1.0, operation.Progress);
        Assert.Equal("Step 1 result cleared.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ClearStep4ResultCommand_CompletedMapping_ResetsOnlyProgress()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);
        using var changes = new ProgressOperation(vm, "Mapping").Observe();

        vm.ClearStep4ResultCommand.Execute(null);

        Assert.Equal("IndexMappingProgress", Assert.Single(changes.Changes).Name);
        Assert.False(vm.IsAnalyzingIndexMapping);
        Assert.Equal(0.0, vm.IndexMappingProgress);
        Assert.Equal("Step 4 result cleared.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ClearStep4ResultCommand_AlreadyCleared_DoesNotRaiseBusyOrProgress()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        vm.ClearStep4ResultCommand.Execute(null);
        using var changes = new ProgressOperation(vm, "Mapping").Observe();

        vm.ClearStep4ResultCommand.Execute(null);

        Assert.Empty(changes.Changes);
        Assert.False(vm.IsAnalyzingIndexMapping);
        Assert.Equal(0.0, vm.IndexMappingProgress);
    }

    [AvaloniaTheory]
    [InlineData("Overlap")]
    [InlineData("Match")]
    public async Task ProgressCommand_ReentrantExecuteAsync_EntersDespiteDisabledCanExecute(string kind)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var operation = new ProgressOperation(await files.CreateLoadedAsync(), kind);
        using var changes = operation.Observe();
        using var overlap = new ReentrantProgressRun(operation);

        await operation.Command.ExecuteAsync(null);
        await overlap.SecondRun;

        Assert.True(overlap.SecondRequestedWhileRunning);
        Assert.False(overlap.CanExecuteAtSecondRequest);
        Assert.True(overlap.FirstIdleWhileSecondPending);
        Assert.Equal(2, changes.Changes.Count(change => change.Name == operation.BusyName && change.Busy));
        Assert.False(operation.Busy);
        Assert.Equal(1.0, operation.Progress);
    }

    [AvaloniaFact]
    public async Task AnalyzeIndexMappingCommand_OverlappingReports_FirstCompletionClearsBusyWhileSecondWaits()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        using var firstReport = new SavePathGate();
        using var secondReport = new SavePathGate();
        vm.OpenIndexMappingReportAsync = _ => firstReport.PickAsync("report", "");
        var first = await firstReport.StartAndWaitForEntryAsync(() => vm.AnalyzeIndexMappingCommand.ExecuteAsync(null));
        Assert.True(vm.IsAnalyzingIndexMapping);
        Assert.False(vm.AnalyzeIndexMappingCommand.CanExecute(null));
        vm.OpenIndexMappingReportAsync = _ => secondReport.PickAsync("report", "");

        var second = await secondReport.StartAndWaitForEntryAsync(() => vm.AnalyzeIndexMappingCommand.ExecuteAsync(null));
        firstReport.Release();
        await first;

        Assert.False(second.IsCompleted);
        Assert.False(vm.IsAnalyzingIndexMapping); // Looks like a defect, pinned as current with the second report open.
        Assert.Equal(1.0, vm.IndexMappingProgress);
        secondReport.Release();
        await second;
        Assert.False(vm.IsAnalyzingIndexMapping);
        Assert.Equal(1.0, vm.IndexMappingProgress);
    }

    [AvaloniaFact]
    public async Task ClearStep4ResultCommand_ReportPending_ResetsProgressWithoutClearingBusy()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        using var report = new SavePathGate();
        vm.OpenIndexMappingReportAsync = _ => report.PickAsync("report", "");
        var analysis = await report.StartAndWaitForEntryAsync(() => vm.AnalyzeIndexMappingCommand.ExecuteAsync(null));
        using var changes = new ProgressOperation(vm, "Mapping").Observe();

        vm.ClearStep4ResultCommand.Execute(null);

        Assert.False(analysis.IsCompleted);
        Assert.True(vm.IsAnalyzingIndexMapping);
        Assert.Equal(0.0, vm.IndexMappingProgress);
        Assert.Equal("IndexMappingProgress", Assert.Single(changes.Changes).Name);
        report.Release();
        await analysis;
        Assert.False(vm.IsAnalyzingIndexMapping);
        Assert.Equal(1.0, vm.IndexMappingProgress);
    }
}

internal sealed class ReentrantProgressRun : IDisposable
{
    private readonly ProgressOperation _operation;
    // Accessed only on the headless UI thread by the command and notification callbacks.
    private Request? _request;
    public Task SecondRun => _request?.Run ?? Task.CompletedTask;
    public bool SecondRequestedWhileRunning => _request?.WasRunning ?? false;
    public bool CanExecuteAtSecondRequest => _request?.CanExecute ?? false;
    public bool FirstIdleWhileSecondPending { get; private set; }

    public ReentrantProgressRun(ProgressOperation operation)
    {
        _operation = operation;
        _operation.ViewModel.PropertyChanged += OnChange;
    }

    private void OnChange(object? sender, System.ComponentModel.PropertyChangedEventArgs change)
    {
        var vm = _operation.ViewModel;
        var completed = _operation.Kind == "Overlap" ? "DXF check: no overlaps detected." : "Matching complete.";
        if (change.PropertyName == nameof(vm.StatusText) && vm.StatusText == completed && _request is null)
        {
            var wasRunning = _operation.Command.IsRunning;
            var canExecute = _operation.Command.CanExecute(null);
            _request = new Request(_operation.Command.ExecuteAsync(null), wasRunning, canExecute);
        }
        if (change.PropertyName == _operation.BusyName && !_operation.Busy && !SecondRun.IsCompleted)
        {
            FirstIdleWhileSecondPending = true;
        }
    }

    public void Dispose() => _operation.ViewModel.PropertyChanged -= OnChange;

    private sealed record Request(Task Run, bool WasRunning, bool CanExecute);
}
