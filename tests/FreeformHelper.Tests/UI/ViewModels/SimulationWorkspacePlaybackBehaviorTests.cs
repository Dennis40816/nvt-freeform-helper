// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspacePlaybackBehaviorTests
{
    [AvaloniaFact]
    public async Task FrameCommands_AtBothEnds_RespectLoopSettingAndPausePlaybackAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateLoadedWorkspaceAsync();
        try
        {
            Assert.Equal("Loop on", vm.PlaybackLoopText);
            vm.TogglePlaybackCommand.Execute(null);

            vm.PreviousFrameCommand.Execute(null);

            Assert.False(vm.IsPlaybackRunning);
            Assert.Equal("Play", vm.PlaybackButtonText);
            Assert.Equal(2, vm.SelectedFrameSliderIndex);
            Assert.Equal("300", vm.SelectedBeforeText);
            Assert.Equal("Frame 3/3", vm.FrameSummaryText);

            vm.TogglePlaybackCommand.Execute(null);
            vm.NextFrameCommand.Execute(null);

            Assert.False(vm.IsPlaybackRunning);
            Assert.Equal(0, vm.SelectedFrameSliderIndex);
            Assert.Equal("100", vm.SelectedBeforeText);

            vm.IsPlaybackLoopEnabled = false;
            Assert.Equal("Loop off", vm.PlaybackLoopText);
            vm.TogglePlaybackCommand.Execute(null);
            vm.PreviousFrameCommand.Execute(null);

            Assert.False(vm.IsPlaybackRunning);
            Assert.Equal(0, vm.SelectedFrameSliderIndex);
            Assert.Equal("100", vm.SelectedBeforeText);
            vm.SelectedFrameSliderIndex = 2;
            vm.TogglePlaybackCommand.Execute(null);
            vm.NextFrameCommand.Execute(null);

            Assert.False(vm.IsPlaybackRunning);
            Assert.Equal(2, vm.SelectedFrameSliderIndex);
            Assert.Equal("300", vm.SelectedBeforeText);
        }
        finally
        {
            vm.ClearCsvCommand.Execute(null);
        }
    }

    [AvaloniaFact]
    public async Task Playback_WithoutLoop_ClampsRequestedSpeedAndStopsOnTheLastFrameAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateLoadedWorkspaceAsync();
        try
        {
            vm.PlaybackFramesPerSecond = 0;
            Assert.Equal(1, vm.PlaybackFramesPerSecond);
            vm.PlaybackFramesPerSecond = 120;
            Assert.Equal(60, vm.PlaybackFramesPerSecond);
            vm.IsPlaybackLoopEnabled = false;

            vm.TogglePlaybackCommand.Execute(null);

            Assert.True(vm.IsPlaybackRunning);
            Assert.Equal("Pause", vm.PlaybackButtonText);
            await HeadlessUiPump.WaitUntilAsync(
                () => !vm.IsPlaybackRunning, "Non-looping playback did not stop at the last frame.");

            Assert.Equal(2, vm.SelectedFrameSliderIndex);
            Assert.Equal("Frame 3/3", vm.FrameSummaryText);
            Assert.Equal("300", vm.SelectedBeforeText);
            Assert.Equal("Play", vm.PlaybackButtonText);
            Assert.True(vm.TogglePlaybackCommand.CanExecute(null));
        }
        finally
        {
            vm.ClearCsvCommand.Execute(null);
        }
    }

    [AvaloniaFact]
    public async Task Playback_CreatedOffUiThread_WithLoopWrapsToTheFirstFrameAndKeepsRunningAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await Task.Run(CreateLoadedWorkspaceAsync);
        try
        {
            vm.PlaybackFramesPerSecond = 60;
            vm.SelectedFrameSliderIndex = 2;

            vm.TogglePlaybackCommand.Execute(null);
            await HeadlessUiPump.WaitUntilAsync(
                () => vm.SelectedFrameSliderIndex == 0, "Looping playback did not return to the first frame.");

            Assert.True(vm.IsPlaybackRunning);
            Assert.Equal("Pause", vm.PlaybackButtonText);
            Assert.Equal("Frame 1/3", vm.FrameSummaryText);
            Assert.Equal("100", vm.SelectedBeforeText);
        }
        finally
        {
            vm.ClearCsvCommand.Execute(null);
        }
    }

    private static async Task<SimulationWorkspaceViewModel> CreateLoadedWorkspaceAsync()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        var vm = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
        var path = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            timestamp(mm:ss:nnn):"09:14:265",Frame index:"1",Break point index:"0",DiffData:
            200,80
            timestamp(mm:ss:nnn):"09:14:365",Frame index:"2",Break point index:"0",DiffData:
            300,90
            """);
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([path]);
        await vm.ImportCsvCommand.ExecuteAsync(null);
        vm.SelectRegularPad(0);
        Assert.Equal(3, vm.FrameOptions.Count);
        Assert.Equal("100", vm.SelectedBeforeText);
        return vm;
    }
}
