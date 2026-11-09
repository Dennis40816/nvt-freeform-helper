// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInspectorDeferredBehaviorTests
{
    [AvaloniaFact]
    public async Task CadRefresh_StaleSelectionRevision_KeepsTheCurrentSelectionSnapshotAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var cad = Assert.Single(vm.CadPads);
        vm.ApplyCanvasSelection([cad.Id], []);
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        var superseded = work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName);
        Assert.Equal("Queued", Assert.Single(current.RuleTrace).Outcome);

        // Revision zero predates the selection applied above.
        vm.QueueDeferredCadInspectorSnapshotRefresh(cad.Id, selectionRevision: 0);
        Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
        await Task.WhenAll(superseded, work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Equal(new[] { cad.Id }, vm.SelectedCadPadIds);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [AvaloniaFact]
    public async Task RegularRefresh_StaleSelectionRevision_KeepsTheCurrentSelectionSnapshotAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var regular = Assert.Single(vm.RegularPads);
        vm.ApplyCanvasSelection([], [regular.Index]);
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        var superseded = work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName);
        Assert.Equal("Queued", Assert.Single(current.RuleTrace).Outcome);

        vm.QueueDeferredRegularInspectorSnapshotRefresh(regular.RegularPadId, selectionRevision: 0);
        Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
        await Task.WhenAll(superseded, work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Equal(new[] { regular.Index }, vm.SelectedRegularPadIndices);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [AvaloniaFact]
    public async Task CadRefresh_DifferentPadRequest_DoesNotReplaceTheSelectedCadAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(2,
            ("selected", 0, 0, 5, 10), ("other", 10, 0, 15, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var selected = Assert.Single(vm.CadPads, static pad => pad.Layer == "selected");
        var other = Assert.Single(vm.CadPads, static pad => pad.Layer == "other");
        vm.ApplyCanvasSelection([selected.Id], []);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        Assert.NotNull(current.Cad?.Notch);

        vm.QueueDeferredCadInspectorSnapshotRefresh(other.Id);
        Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Equal(selected.Id, vm.PadInspectorUi.Snapshot?.Cad?.CadPadId);
        Assert.Equal(new[] { selected.Id }, vm.SelectedCadPadIds);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [AvaloniaFact]
    public async Task RegularRefresh_DifferentPadRequest_DoesNotReplaceTheSelectedRegularAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(2,
            ("left", 0, 0, 5, 10), ("right", 10, 0, 15, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var selected = Assert.Single(vm.RegularPads, static pad => pad.Col == 0);
        var other = Assert.Single(vm.RegularPads, static pad => pad.Col == 1);
        vm.ApplyCanvasSelection([], [selected.Index]);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        Assert.Contains(current.RuleTrace, static entry => entry.Rule == "Identity");

        vm.QueueDeferredRegularInspectorSnapshotRefresh(other.RegularPadId);
        Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Equal(selected.RegularPadId, vm.PadInspectorUi.Snapshot?.Regular?.RegularPadId);
        Assert.Equal(new[] { selected.Index }, vm.SelectedRegularPadIndices);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }
}
