// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchAutoPlayCharacterizationTests
{
    private static readonly decimal[] _expectedStageSequence = [2m, 3m, 1m, 2m];

    [AvaloniaFact]
    public async Task HideThenShow_StopsTheOldLoopAndStartsOneLoopWithOneImmediateStageAdvanceAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = CreatePreviewViewModel();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var stages = new List<decimal>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FreeformHelperViewModel.NotchPreviewVisualizationStep))
            {
                stages.Add(vm.NotchPreviewVisualizationStep);
            }
        };
        try
        {
            vm.NotchPreviewAutoPlayEnabled = true;
            Assert.Equal(2m, vm.NotchPreviewVisualizationStep);
            Assert.Equal(1, work.StartedCount(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName));
            await HeadlessUiPump.WaitUntilAsync(() => vm.NotchPreviewVisualizationStep == 3m, "Autoplay did not advance to Final.");

            vm.ShowNotchCanvasPreview = false;
            await work.LastStartedAsync(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(vm.IsNotchCanvasPreviewVisible);
            Assert.Equal(3m, vm.NotchPreviewVisualizationStep);

            vm.ShowNotchCanvasPreview = true;
            Assert.Equal(1m, vm.NotchPreviewVisualizationStep);
            Assert.Equal(2, work.StartedCount(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName));
            // Replacing the visible data must not start a second loop or advance again.
            vm.NotchCanvasPreviewItems = new ObservableCollection<NotchCanvasPreviewItem>(vm.NotchCanvasPreviewItems);
            Assert.Equal(1m, vm.NotchPreviewVisualizationStep);
            Assert.Equal(2, work.StartedCount(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName));
            await HeadlessUiPump.WaitUntilAsync(
                () => vm.NotchPreviewVisualizationStep == 2m, "Restarted autoplay did not advance to Candidate.");
            // Order matters: Candidate then Final, then the reshow resets to Seed and advances to Candidate again.
            Assert.Equal(_expectedStageSequence, stages.Take(_expectedStageSequence.Length));
        }
        finally
        {
            vm.ShowNotchCanvasPreview = false;
            await work.LastStartedAsync(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    private static FreeformHelperViewModel CreatePreviewViewModel()
    {
        var seed = TestGeometryFactory.CreateRect(0, 3, 3, 7);
        var final = TestGeometryFactory.CreateRect(0, 0, 10, 10);
        return new FreeformHelperViewModel
        {
            NotchPreviewAutoPlayEnabled = false,
            NotchPreviewAutoPlayIntervalMs = 200m,
            EnableToFull = true,
            ShowNotchCanvasPreview = true,
            NotchPreviewVisualizationStep = 1m,
            NotchCanvasPreviewItems = new ObservableCollection<NotchCanvasPreviewItem>
            {
                new(101, 0.12, 100.0 / 12, true, [seed], [final], [final], [final]),
            },
        };
    }
}
