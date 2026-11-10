// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationPlaybackCopperOwnerCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task PlaybackTimerRetryPublishesRunningBeforeTicksAndStopsAfterTheLastFrameAsync()
    {
        var vm = await Task.Run(() => CreateLoadedWorkspaceAsync(twoFrames: true));
        vm.PlaybackFramesPerSecond = 60;
        vm.IsPlaybackLoopEnabled = false;
        var events = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsPlaybackRunning))
            {
                events.Add(vm.IsPlaybackRunning ? "running" : "stopped");
            }
            if (e.PropertyName == nameof(vm.SelectedFrameSliderIndex)) events.Add("frame");
        };
        vm.TogglePlaybackCommand.CanExecuteChanged += (_, _) => events.Add($"command:{vm.IsPlaybackRunning}");
        try
        {
            vm.TogglePlaybackCommand.Execute(null);
            Assert.Equal<string>(["command:True", "running", "command:True"], events);
            Assert.Equal(0, vm.SelectedFrameSliderIndex);

            await HeadlessUiPump.WaitUntilAsync(
                () => !vm.IsPlaybackRunning, "Playback did not stop after the last frame.");

            Assert.Equal<string>([
                "command:True", "running", "command:True", "command:True", "frame",
                "command:False", "stopped", "command:False",
            ], events);
            Assert.Equal(1, vm.SelectedFrameSliderIndex);
        }
        finally
        {
            vm.ClearCsvCommand.Execute(null);
        }
    }

    [Fact]
    public async Task PlaybackOffDispatcherPreservesRunningAndCommandNotificationOrderAsync()
    {
        await Task.Run(async () =>
        {
            var vm = await CreateLoadedWorkspaceAsync(twoFrames: true);
            var events = new List<string>();
            vm.PropertyChanging += (sender, e) =>
            {
                Assert.Same(vm, sender);
                if (e.PropertyName == nameof(vm.IsPlaybackRunning)) events.Add($"changing:{vm.IsPlaybackRunning}");
            };
            vm.PropertyChanged += (sender, e) =>
            {
                Assert.Same(vm, sender);
                if (e.PropertyName == nameof(vm.IsPlaybackRunning)) events.Add($"changed:{vm.IsPlaybackRunning}");
            };
            vm.TogglePlaybackCommand.CanExecuteChanged += (_, _) => events.Add($"command:{vm.IsPlaybackRunning}");

            vm.TogglePlaybackCommand.Execute(null);
            vm.TogglePlaybackCommand.Execute(null);

            Assert.Equal<string>([
                "changing:False", "command:True", "changed:True", "command:True",
                "changing:True", "command:False", "changed:False", "command:False",
            ], events);
            Assert.Equal(0, vm.SelectedFrameSliderIndex);
            Assert.False(vm.IsPlaybackRunning);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FrameCommandsKeepLoopBoundariesAndSingleFrameSliderBehaviorAsync()
    {
        var vm = await CreateLoadedWorkspaceAsync(twoFrames: true);
        vm.PreviousFrameCommand.Execute(null);
        Assert.Equal(1, vm.SelectedFrameSliderIndex);
        vm.NextFrameCommand.Execute(null);
        Assert.Equal(0, vm.SelectedFrameSliderIndex);
        vm.IsPlaybackLoopEnabled = false;
        vm.PreviousFrameCommand.Execute(null);
        Assert.Equal(0, vm.SelectedFrameSliderIndex);
        vm.NextFrameCommand.Execute(null);
        vm.NextFrameCommand.Execute(null);
        Assert.Equal(1, vm.SelectedFrameSliderIndex);
        var accepted = vm.GetSimulationScenarioSnapshot();
        vm.SelectedFrameSliderIndex = 99;
        Assert.Equal(1, vm.SelectedFrameSliderIndex);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());

        var single = await CreateLoadedWorkspaceAsync(twoFrames: false);
        Assert.True(single.CanStepFrames);
        Assert.False(single.CanPlayFrames);
        accepted = single.GetSimulationScenarioSnapshot();
        single.SelectedFrameSliderIndex = 99;
        Assert.Equal(99, single.SelectedFrameSliderIndex);
        Assert.Same(accepted, single.GetSimulationScenarioSnapshot());
        single.NextFrameCommand.Execute(null);
        Assert.Equal(0, single.SelectedFrameSliderIndex);
        Assert.Same(accepted, single.GetSimulationScenarioSnapshot());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(120, 60)]
    public void PlaybackSpeedNormalizesInsideTheOriginalNotification(int requested, int expected)
    {
        var vm = CreateWorkspace();
        var observed = new List<int>();
        vm.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.PlaybackFramesPerSecond)) observed.Add(vm.PlaybackFramesPerSecond);
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.PlaybackFramesPerSecond)) observed.Add(vm.PlaybackFramesPerSecond);
        };

        vm.PlaybackFramesPerSecond = requested;

        Assert.Equal(new[] { 6, requested, expected, expected }, observed);
    }

    [Fact]
    public void ContactModelChangesPeakBeforePublishingItsOwnSummaries()
    {
        var vm = CreateWorkspace();
        var events = new List<string>();
        vm.PropertyChanging += (_, e) => events.Add($"changing:{e.PropertyName}");
        vm.PropertyChanged += (_, e) => events.Add($"changed:{e.PropertyName}");

        vm.SelectedCopperContactModelOption = vm.CopperContactModelOptions[1];

        Assert.Equal(360d, vm.CopperPeakValue);
        Assert.Equal<string>([
            "changing:SelectedCopperContactModelOption", "changing:CopperPeakValue",
            "changed:CopperContactModelSummaryText", "changed:CopperPositionText", "changed:CopperSummaryText",
            "changed:CopperProjectionModelText", "changed:CopperContactModelSummaryText",
            "changed:InputPrimarySummaryText", "changed:InputSecondarySummaryText", "changed:SelectedFrameDisplayText",
            "changed:CopperPeakValue", "changed:CopperContactModelSummaryText", "changed:CopperSummaryText",
            "changed:InputPrimarySummaryText", "changed:SelectedFrameDisplayText", "changed:SelectedCopperContactModelOption",
        ], events);
    }

    [Fact]
    public void CopperPointerPublishesIntermediateCentersBeforeOneSnapshotReplacement()
    {
        var vm = CreateWorkspace();
        vm.SelectedInputSourceOption = vm.SourceOptions[2];
        var accepted = vm.GetSimulationScenarioSnapshot();
        var oldY = vm.CopperCenterY;
        var centers = new List<(double X, double Y)>();
        var publications = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.CopperPositionText) && publications == 0)
            {
                Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
                centers.Add((vm.CopperCenterX, vm.CopperCenterY));
            }
            if (e.PropertyName == nameof(vm.SimulationOverlayItems)) publications++;
        };

        vm.MoveCopperToWorldPoint(new Point2(3, 4));

        Assert.Equal(new[] { (3d, oldY), (3d, 4d), (3d, 4d) }, centers);
        Assert.Equal(1, publications);
        Assert.NotSame(accepted, vm.GetSimulationScenarioSnapshot());
    }

    [Fact]
    public void ReplayReplacesRowsButRetainsAnExistingSelectedStepObject()
    {
        var vm = CreateWorkspace();
        var rows = vm.CopperPathReplayRows;
        vm.ReplayCopperPath(new Point2(0.5, 0.5), new Point2(1.5, 0.5), 3);
        var selected = rows[2];
        vm.SelectedCopperPathReplayRow = selected;

        vm.ReplayCopperPath(new Point2(0, 0), new Point2(2, 0), 3);

        Assert.Same(rows, vm.CopperPathReplayRows);
        Assert.Same(selected, vm.SelectedCopperPathReplayRow);
        Assert.NotSame(selected, rows[2]);
        vm.ReplayCopperPath(new Point2(0, 0), new Point2(2, 0), 1);
        Assert.Null(vm.SelectedCopperPathReplayRow);
        vm.SelectedInputSourceOption = vm.SourceOptions[0];
        vm.ClearCsvCommand.Execute(null);
        Assert.True(vm.HasCopperPathReplay);
        Assert.Single(rows);
    }

    [Fact]
    public async Task ReplayExportsPreserveUnavailableEmptySuccessAndCancellationOutcomesAsync()
    {
        var vm = CreateWorkspace();
        await vm.CopyCopperPathReplayCommand.ExecuteAsync(null);
        Assert.Contains("unavailable", vm.StatusText, StringComparison.Ordinal);
        await vm.ExportCopperPathReplayCsvCommand.ExecuteAsync(null);
        Assert.Contains("unavailable", vm.StatusText, StringComparison.Ordinal);
        vm.RequestSetClipboardTextAsync = _ => Task.FromResult(true);
        vm.RequestSaveTextFileAsync = _ => Task.FromResult(true);
        await vm.CopyCopperPathReplayCommand.ExecuteAsync(null);
        Assert.Equal("Run copper path replay before copying rows.", vm.StatusText);
        await vm.ExportCopperPathReplayJsonCommand.ExecuteAsync(null);
        Assert.Equal("Run copper path replay before exporting JSON.", vm.StatusText);
        vm.ReplayCopperPath(new Point2(0.5, 0.5), new Point2(1.5, 0.5), 2);
        await vm.CopyCopperPathReplayCommand.ExecuteAsync(null);
        Assert.Equal("Copied 2 copper path replay row(s).", vm.StatusText);
        vm.RequestSetClipboardTextAsync = _ => Task.FromResult(false);
        await vm.CopyCopperPathReplayCommand.ExecuteAsync(null);
        Assert.Equal("Copper path replay clipboard copy failed.", vm.StatusText);
        SimulationWorkspaceViewModel.SimulationTextExportRequest? request = null;
        vm.RequestSaveTextFileAsync = value =>
        {
            request = value;
            return Task.FromResult(true);
        };
        await vm.ExportCopperPathReplayCsvCommand.ExecuteAsync(null);
        Assert.NotNull(request);
        Assert.Equal("copper_path_replay.csv", request.SuggestedFileName);
        Assert.NotEmpty(request.Content);
        Assert.Equal("Exported CSV copper path replay row(s): 2.", vm.StatusText);
        vm.RequestSaveTextFileAsync = _ => Task.FromResult(false);
        await vm.ExportCopperPathReplayJsonCommand.ExecuteAsync(null);
        Assert.Equal("Copper path replay JSON export cancelled.", vm.StatusText);
    }

    private static async Task<SimulationWorkspaceViewModel> CreateLoadedWorkspaceAsync(bool twoFrames)
    {
        var vm = CreateWorkspace();
        var text = "Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData:\n100,50\n";
        if (twoFrames) text += "timestamp(0:0:1),DiffData:\n200,80\n";
        var path = TestFiles.WriteTempCsv(text);
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([path]);
        await vm.ImportCsvAsync();
        return vm;
    }

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var session = new FreeformHelper.UI.Services.SimulationWorkspaceSession(
            grid, new NotchTable([]), 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadPadsForCopperProjection:
            [
                TestGeometryFactory.CreateCadPad(100, "SENSOR", 0, 0, 1, 1),
                TestGeometryFactory.CreateCadPad(101, "SENSOR", 1, 0, 2, 1),
            ],
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [100] = 10, [101] = 11 },
            CadIcIndexByCadId: new Dictionary<int, int> { [100] = 0, [101] = 0 });
        return new SimulationWorkspaceViewModel(
            new FreeformHelper.UI.Services.SimulationWorkspaceUseCase(
                new FreeformHelper.UI.Services.NotchApplySimulationReviewUseCase()), session);
    }
}
