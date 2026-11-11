// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

// TestAppSettingsIsolation redirects the default store before any view model is constructed.
[Collection("HeadlessUiSerial")]
public sealed class PidDeferredInspectorCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Queue_SupersededRequestCannotPublishOrClearReplacementPendingAsync(bool cad)
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new PadInspectorWorkQueue();
        vm.BackgroundWork.Runner = work;
        var cadPad = Assert.Single(vm.CadPads);
        var regular = Assert.Single(vm.RegularPads);
        var pending = RecordPending(vm);
        vm.ApplyCanvasSelection(cad ? [cadPad.Id] : [], cad ? [] : [regular.Index]);
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        var workName = WorkName(cad);
        var superseded = work.LastStartedAsync(workName);
        work.HoldNewWork = true;

        try
        {
            Queue(vm, cad, cad ? cadPad.Id : regular.RegularPadId);
            await superseded.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Same(current, vm.PadInspectorUi.Snapshot);
            Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
            Assert.Equal<bool>([true], pending);
            work.StartQueued();
            await work.LastStartedAsync(workName).WaitAsync(TimeSpan.FromSeconds(10));

            var applied = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
            Assert.NotSame(current, applied);
            Assert.Equal(cad ? cadPad.Id : regular.RegularPadId,
                cad ? applied.Cad?.CadPadId : applied.Regular?.RegularPadId);
            Assert.DoesNotContain(applied.RuleTrace, static entry => entry.Rule == "Computing");
            Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
            Assert.Equal<bool>([true, false], pending);
        }
        finally
        {
            // Start held work before cancellation disposes the token source read by its delegate.
            work.StartQueued();
            vm.ApplyCanvasSelection([], []);
            await work.LastStartedAsync(workName).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Queue_StaleSelectionRevisionKeepsTheAcceptedSnapshotAsync(bool cad)
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var cadPad = Assert.Single(vm.CadPads);
        var regular = Assert.Single(vm.RegularPads);
        vm.ApplyCanvasSelection(cad ? [cadPad.Id] : [], cad ? [] : [regular.Index]);
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        var superseded = work.LastStartedAsync(WorkName(cad));

        QueueAtRevision(vm, cad, cad ? cadPad.Id : regular.RegularPadId, selectionRevision: 0);
        await Task.WhenAll(superseded, work.LastStartedAsync(WorkName(cad))).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Equal("Queued", Assert.Single(current.RuleTrace).Outcome);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task EmptyCanvasSelection_CompletesBothQueuedRefreshesAndClearsPendingOnceAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var pending = RecordPending(vm);
        vm.QueueDeferredCadInspectorSnapshotRefresh(11);
        vm.QueueDeferredRegularInspectorSnapshotRefresh(101);
        Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);

        vm.ApplyCanvasSelection([], []);
        await Task.WhenAll(work.LastStartedAsync(WorkName(true)), work.LastStartedAsync(WorkName(false)))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(vm.PadInspectorUi.Snapshot);
        Assert.Equal<bool>([true, false], pending);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CadRefresh_NoGridKeepsTheLightweightSnapshotAndCompletesPendingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.CadPads.Add(TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 5, 10));
        var pending = RecordPending(vm);
        vm.ApplyCanvasSelection([11], []);
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);

        await work.LastStartedAsync(WorkName(true)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Null(current.Cad?.Notch);
        Assert.Equal("Queued", Assert.Single(current.RuleTrace).Outcome);
        Assert.Equal<bool>([true, false], pending);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CadRefresh_NoMatchingCadKeepsTheSnapshotAndCompletesPendingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var cad = Assert.Single(vm.CadPads);
        vm.PadInspectorUi.Snapshot = vm.BuildCadPadInspectorSnapshot(cad.Id);
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        var pending = RecordPending(vm);

        vm.QueueDeferredCadInspectorSnapshotRefresh(int.MinValue);
        await work.LastStartedAsync(WorkName(true)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Equal<bool>([true, false], pending);
    }

    private static string WorkName(bool cad) => cad
        ? FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName
        : FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName;

    private static void Queue(FreeformHelperViewModel vm, bool cad, int padId)
    {
        if (cad)
        {
            vm.QueueDeferredCadInspectorSnapshotRefresh(padId);
        }
        else
        {
            vm.QueueDeferredRegularInspectorSnapshotRefresh(padId);
        }
    }

    private static List<bool> RecordPending(FreeformHelperViewModel vm)
    {
        var values = new List<bool>();
        vm.PadInspectorUi.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PadInspectorUiState.IsDeferredRefreshPending))
            {
                values.Add(vm.PadInspectorUi.IsDeferredRefreshPending);
            }
        };
        return values;
    }

    private static void QueueAtRevision(FreeformHelperViewModel vm, bool cad, int padId, long selectionRevision)
    {
        if (cad)
        {
            vm.QueueDeferredCadInspectorSnapshotRefresh(padId, selectionRevision);
        }
        else
        {
            vm.QueueDeferredRegularInspectorSnapshotRefresh(padId, selectionRevision);
        }
    }
}
