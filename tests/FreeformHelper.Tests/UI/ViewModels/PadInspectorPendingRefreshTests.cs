// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInspectorPendingRefreshTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Refresh_SupersededWorkFinishes_KeepsReplacementPendingAndAppliesOnlyReplacementAsync(bool cad)
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(2,
            ("left", 0, 0, 5, 10), ("right", 10, 0, 15, 10));
        var vm = workspace.ViewModel;
        var work = new PadInspectorWorkQueue();
        vm.BackgroundWork.Runner = work;
        var workName = cad
            ? FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName
            : FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName;
        var firstCad = Assert.Single(vm.CadPads, static pad => pad.Layer == "left");
        var secondCad = Assert.Single(vm.CadPads, static pad => pad.Layer == "right");
        var firstRegular = Assert.Single(vm.RegularPads, static pad => pad.Col == 0);
        var secondRegular = Assert.Single(vm.RegularPads, static pad => pad.Col == 1);
        var snapshots = new List<PadInspectorSnapshot?>();
        vm.PadInspectorUi.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PadInspectorUiState.Snapshot))
            {
                snapshots.Add(vm.PadInspectorUi.Snapshot);
            }
        };

        try
        {
            vm.ApplyCanvasSelection(cad ? [firstCad.Id] : [], cad ? [] : [firstRegular.Index]);
            var superseded = work.LastStartedAsync(workName);
            Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
            work.HoldNewWork = true;
            vm.ApplyCanvasSelection(cad ? [secondCad.Id] : [], cad ? [] : [secondRegular.Index]);
            var replacement = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
            Assert.Equal("Queued", Assert.Single(replacement.RuleTrace).Outcome);

            await superseded.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
            Assert.Same(replacement, vm.PadInspectorUi.Snapshot);
            Assert.Equal(2, snapshots.Count);
            Assert.Equal(1, work.QueuedCount);
            work.StartQueued();
            await work.LastStartedAsync(workName).WaitAsync(TimeSpan.FromSeconds(10));

            var applied = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
            Assert.NotSame(replacement, applied);
            Assert.Equal(cad ? secondCad.Id : secondRegular.RegularPadId,
                cad ? applied.Cad?.CadPadId : applied.Regular?.RegularPadId);
            Assert.DoesNotContain(applied.RuleTrace, static entry => entry.Rule == "Computing");
            Assert.Equal(3, snapshots.Count);
            Assert.Same(applied, snapshots[2]);
            Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
        }
        finally
        {
            // Start a held delegate before cancelling: it reads its request's cancellation token on entry.
            work.StartQueued();
            vm.ApplyCanvasSelection([], []);
            await work.LastStartedAsync(workName).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [AvaloniaFact]
    public async Task Refresh_SelectedCadRemovedFromProjection_KeepsSelectionAndRejectsDeferredSnapshotAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var cad = Assert.Single(vm.CadPads);
        vm.ApplyCanvasSelection([cad.Id], []);
        var current = Assert.IsType<PadInspectorSnapshot>(vm.PadInspectorUi.Snapshot);
        Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
        Assert.Equal("Queued", Assert.Single(current.RuleTrace).Outcome);

        // Hide commands clear the selection; removing the public projection isolates source disappearance.
        Assert.True(vm.CadPads.Remove(cad));
        Assert.Equal(new[] { cad.Id }, vm.SelectedCadPadIds);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(current, vm.PadInspectorUi.Snapshot);
        Assert.Empty(vm.CadPads);
        Assert.Equal(new[] { cad.Id }, vm.SelectedCadPadIds);
        Assert.Equal("Queued", Assert.Single(current.RuleTrace).Outcome);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
        vm.ApplyCanvasSelection([], []);
    }
}
