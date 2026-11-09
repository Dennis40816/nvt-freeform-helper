// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchPreviewTimingCharacterizationTests
{
    [AvaloniaFact]
    public async Task SingleSelectionRefreshesThePreviewWithoutAdvancingTheClockAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var vm = await CreatePreviewViewModelAsync(files, dxf, clock);
        var cadId = vm.CadPads[0].Id;
        Assert.Empty(vm.NotchCanvasPreviewItems);

        vm.ApplyCanvasSelection([cadId], []);

        Assert.Equal(cadId, Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);
        Assert.True(vm.IsNotchCanvasPreviewVisible);
    }

    [AvaloniaFact]
    public async Task MultiSelectionRefreshesAt120MillisecondsButNot119Async()
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var vm = await CreatePreviewViewModelAsync(files, dxf, clock);
        var cadIds = vm.CadPads.Select(static pad => pad.Id).ToArray();
        vm.ApplyCanvasSelection([cadIds[0]], []);
        Assert.Equal(cadIds[0], Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);

        vm.ApplyCanvasSelection(cadIds, []);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(cadIds[0], Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);
        clock.Advance(TimeSpan.FromMilliseconds(119));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(cadIds[0], Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await HeadlessUiPump.WaitUntilAsync(() => vm.NotchCanvasPreviewItems.Count == 2, "Multi-selection preview did not refresh.");

        Assert.Equal(cadIds, vm.NotchCanvasPreviewItems.Select(static item => item.CadPadId));
    }

    [AvaloniaFact]
    public async Task AnotherMultiSelectionRestartsTheWaitAndRefreshesOnceAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var vm = await CreatePreviewViewModelAsync(files, dxf, clock);
        var cadIds = vm.CadPads.Select(static pad => pad.Id).ToArray();
        vm.ApplyCanvasSelection([cadIds[0]], []);
        var previous = vm.NotchCanvasPreviewItems;
        var refreshes = new List<int[]>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FreeformHelperViewModel.NotchCanvasPreviewItems))
            {
                refreshes.Add(vm.NotchCanvasPreviewItems.Select(static item => item.CadPadId).ToArray());
            }
        };

        vm.ApplyCanvasSelection(cadIds, []);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Same(previous, vm.NotchCanvasPreviewItems);
        // A regular-selection change keeps both CAD pads selected and restarts their deferred preview.
        vm.ApplyCanvasSelection(cadIds, [vm.RegularPads[0].Index]);
        clock.Advance(TimeSpan.FromMilliseconds(20));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Empty(refreshes);
        Assert.Same(previous, vm.NotchCanvasPreviewItems);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Empty(refreshes);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await HeadlessUiPump.WaitUntilAsync(() => refreshes.Count > 0, "Restarted multi-selection preview did not refresh.");

        Assert.Equal(cadIds, Assert.Single(refreshes));
        Assert.Equal(cadIds, vm.NotchCanvasPreviewItems.Select(static item => item.CadPadId));
        clock.Advance(TimeSpan.FromMilliseconds(120));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Single(refreshes);
    }

    [AvaloniaFact]
    public async Task ClearingSelectionCancelsThePendingRefreshAndClearsThePreviewAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var vm = await CreatePreviewViewModelAsync(files, dxf, clock);
        var cadIds = vm.CadPads.Select(static pad => pad.Id).ToArray();
        vm.ApplyCanvasSelection([cadIds[0]], []);
        vm.ApplyCanvasSelection(cadIds, []);
        clock.Advance(TimeSpan.FromMilliseconds(119));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(cadIds[0], Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);

        vm.ApplyCanvasSelection([], []);

        Assert.Empty(vm.NotchCanvasPreviewItems);
        Assert.False(vm.IsNotchCanvasPreviewVisible);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Empty(vm.NotchCanvasPreviewItems);
        clock.Advance(TimeSpan.FromMilliseconds(120));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Empty(vm.NotchCanvasPreviewItems);
    }

    [AvaloniaFact]
    public async Task AutoPlayAdvancesAtTheIntervalAndStopsWhenThePreviewIsHiddenAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var vm = await CreatePreviewViewModelAsync(files, dxf, clock);
        vm.ApplyCanvasSelection([vm.CadPads[0].Id], []);
        Assert.True(vm.IsNotchCanvasPreviewVisible);
        vm.NotchPreviewAutoPlayIntervalMs = 200m;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.NotchPreviewAutoPlayEnabled = true;
        var loop = work.LastStartedAsync(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName);
        try
        {
            // Enabling playback immediately advances Seed to Candidate, before the first interval.
            Assert.Equal(2m, vm.NotchPreviewVisualizationStep);
            clock.Advance(TimeSpan.FromMilliseconds(199));
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(2m, vm.NotchPreviewVisualizationStep);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            await HeadlessUiPump.WaitUntilAsync(() => vm.NotchPreviewVisualizationStep == 3m, "Autoplay did not advance to Final.");
            Assert.Equal(3m, vm.NotchPreviewVisualizationStep);

            vm.ShowNotchCanvasPreview = false;
            await loop.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(vm.IsNotchCanvasPreviewVisible);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(3m, vm.NotchPreviewVisualizationStep);
        }
        finally
        {
            vm.ShowNotchCanvasPreview = false;
            await loop.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    private static async Task<FreeformHelperViewModel> CreatePreviewViewModelAsync(
        OperationProgressLifecycleTestScope files,
        DxfEditingBehaviorTestScope dxf,
        ManualTimeProvider clock)
    {
        var vm = await files.CreateLoadedAsync();
        vm.NotchPreviewAutoPlayEnabled = false;
        vm.NotchPreviewTimingForTests.TimeProvider = clock;
        vm.EnableToFull = true;
        vm.ShowNotchCanvasPreview = true;
        vm.NotchPreviewVisualizationStep = 1m;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        await dxf.OpenAsync(vm, ("signal", 0, 0, 8, 10), ("signal", 12, 0, 20, 10));
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result, vm.StatusText);
        Assert.Equal(2, vm.CadPads.Count);
        vm.ApplyCanvasSelection([], []);
        return vm;
    }
}
