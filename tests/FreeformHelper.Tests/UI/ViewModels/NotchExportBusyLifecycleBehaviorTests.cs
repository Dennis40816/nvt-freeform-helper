// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportBusyLifecycleBehaviorTests
{
    private static readonly string[] _clearNotifications = ["NotchExportProgress", "NotchExportProgressText", "IsNotchExporting"];
    [AvaloniaFact]
    public async Task ExportNotchCommand_Success_KeepsBusyThroughSaveAndRaisesOldNames()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var dialog = new SavePathGate();
        vm.PickSaveNotchPathAsync = dialog.PickAsync;
        using var changes = ObserveExport(vm);

        var export = await dialog.StartAndWaitForEntryAsync(() => vm.ExportNotchCommand.ExecuteAsync(null));
        Assert.True(vm.IsNotchExporting);
        Assert.False(export.IsCompleted);
        dialog.Release(files.ExportPath);
        await export;

        changes.AssertBusyScope();
        Assert.Contains(changes.Changes, change => change.Name == "NotchExportProgress");
        Assert.Contains(changes.Changes, change => change.Name == "NotchExportProgressText");
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(1.0, vm.NotchExportProgress);
        Assert.StartsWith("C v2.2 exported: rows=", vm.NotchExportProgressText, StringComparison.Ordinal);
        Assert.Equal(vm.NotchExportSummary, vm.NotchExportProgressText);
        Assert.True(File.Exists(files.ExportPath));
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_SaveDialogCanceled_EndsIdleAtStageFour()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var dialog = new SavePathGate();
        vm.PickSaveNotchPathAsync = dialog.PickAsync;
        using var changes = ObserveExport(vm);

        var export = await dialog.StartAndWaitForEntryAsync(() => vm.ExportNotchCommand.ExecuteAsync(null));
        Assert.True(vm.IsNotchExporting);
        Assert.False(export.IsCompleted);
        dialog.Release();
        await export;

        changes.AssertBusyScope();
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(0.8, vm.NotchExportProgress);
        Assert.Equal("C v2.2 export canceled at stage 4 (save path not selected).", vm.NotchExportProgressText);
        Assert.Equal(vm.NotchExportProgressText, vm.StatusText);
        Assert.False(File.Exists(files.ExportPath));
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_SaveDialogUnwired_DoesNotEnterBusyScope()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var changes = ObserveExport(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.DoesNotContain(changes.Changes, change => change.Name == "IsNotchExporting");
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(0.0, vm.NotchExportProgress);
        Assert.Equal("C v2.2 export blocked: save dialog handler not wired.", vm.NotchExportProgressText);
        Assert.Equal("Export notch: dialog handler not wired.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_SaveDialogUnwiredAfterSuccess_PreservesCompletedProgress()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(files.ExportPath);
        await vm.ExportNotchCommand.ExecuteAsync(null);
        Assert.Equal(1.0, vm.NotchExportProgress);
        vm.PickSaveNotchPathAsync = null;
        using var changes = ObserveExport(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.DoesNotContain(changes.Changes, change => change.Name == "IsNotchExporting" || change.Name == "NotchExportProgress");
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(1.0, vm.NotchExportProgress);
        Assert.Equal("C v2.2 export blocked: save dialog handler not wired.", vm.NotchExportProgressText);
        Assert.Equal("Export notch: dialog handler not wired.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_WorkflowGateBlocked_EndsIdleAtStageOneWithoutDialog()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.ClearStep1ResultCommand.Execute(null);
        using var dialog = new SavePathGate();
        vm.PickSaveNotchPathAsync = dialog.PickAsync;
        using var changes = ObserveExport(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        changes.AssertBusyScope();
        Assert.False(dialog.Entered.IsCompleted);
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(0.2, vm.NotchExportProgress);
        Assert.Equal("C v2.2 export blocked at stage 1: Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional.",
            vm.NotchExportProgressText);
        Assert.Equal("Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_NoDxf_PreservesIdleProgressAndText()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        using var changes = ObserveExport(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Empty(changes.Changes);
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(0.0, vm.NotchExportProgress);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
        Assert.Equal("Export notch: import DXF and build grid first.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_SaveDialogThrows_EndsIdleWithStageFourError()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromException<string?>(
            new InvalidOperationException("Synthetic save dialog failure."));
        using var changes = ObserveExport(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        changes.AssertBusyScope();
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(0.8, vm.NotchExportProgress);
        Assert.Equal("C v2.2 export failed: Synthetic save dialog failure.", vm.NotchExportProgressText);
        Assert.Equal(vm.NotchExportProgressText, vm.StatusText);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearStep5ResultCommand_SaveDialogPending_ClearsBusyBeforeExportEnds(bool save)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var dialog = new SavePathGate();
        vm.PickSaveNotchPathAsync = dialog.PickAsync;
        var export = await dialog.StartAndWaitForEntryAsync(() => vm.ExportNotchCommand.ExecuteAsync(null));
        Assert.True(vm.IsNotchExporting);
        using var changes = ObserveExport(vm);

        vm.ClearStep5ResultCommand.Execute(null);

        Assert.False(export.IsCompleted);
        Assert.False(vm.IsNotchExporting); // Looks like a defect, pinned as current while the dialog still waits.
        Assert.Equal(0.0, vm.NotchExportProgress);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
        Assert.Equal(_clearNotifications.Order(StringComparer.Ordinal),
            changes.Changes.Select(change => change.Name).Order(StringComparer.Ordinal));
        dialog.Release(save ? files.ExportPath : null);
        await export;
        Assert.False(vm.IsNotchExporting);
        Assert.Equal(save ? 1.0 : 0.0, vm.NotchExportProgress);
        Assert.Equal(save, File.Exists(files.ExportPath));
        Assert.StartsWith(save ? "C v2.2 exported: rows=" : "C v2.2 export canceled at stage 4 (save path not selected).",
            vm.NotchExportProgressText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_SecondExecuteAsyncWhileDialogPending_KeepsBusyUntilLastExportEnds()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        using var firstDialog = new SavePathGate();
        using var secondDialog = new SavePathGate();
        vm.PickSaveNotchPathAsync = firstDialog.PickAsync;
        var first = await firstDialog.StartAndWaitForEntryAsync(() => vm.ExportNotchCommand.ExecuteAsync(null));
        Assert.False(vm.ExportNotchCommand.CanExecute(null));
        vm.PickSaveNotchPathAsync = secondDialog.PickAsync;
        using var changes = ObserveExport(vm);

        var second = await secondDialog.StartAndWaitForEntryAsync(() => vm.ExportNotchCommand.ExecuteAsync(null));
        firstDialog.Release();
        await first;

        Assert.False(second.IsCompleted);
        Assert.True(vm.IsNotchExporting);
        Assert.DoesNotContain(changes.Changes, change => change.Name == "IsNotchExporting");
        secondDialog.Release();
        await second;
        Assert.False(vm.IsNotchExporting);
        Assert.False(Assert.Single(changes.Changes, change => change.Name == "IsNotchExporting").Busy);
        Assert.Equal(0.8, vm.NotchExportProgress);
        Assert.Equal("C v2.2 export canceled at stage 4 (save path not selected).", vm.NotchExportProgressText);
    }

    private static LifecycleNotifications ObserveExport(FreeformHelperViewModel vm) => new(vm,
        () => vm.IsNotchExporting, () => vm.NotchExportProgress,
        "IsNotchExporting", "NotchExportProgress", "NotchExportProgressText");
}
