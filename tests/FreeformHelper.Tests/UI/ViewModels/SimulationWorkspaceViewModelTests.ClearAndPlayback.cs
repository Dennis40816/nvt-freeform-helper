using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class SimulationWorkspaceViewModelTests
{
    [Fact]
    public async Task PlaybackAndClearCsvCommands_TogglePlaybackAndResetImportedFramesAsync()
    {
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            timestamp(mm:ss:nnn):"09:14:265",Frame index:"1",Break point index:"0",DiffData:
            200,80
            """);
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession())
        {
            PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(new[] { csvPath }),
        };
        try
        {
            Assert.False(viewModel.ClearCsvCommand.CanExecute(null));
            Assert.False(viewModel.TogglePlaybackCommand.CanExecute(null));
            await viewModel.ImportCsvAsync();
            Assert.True(viewModel.ClearCsvCommand.CanExecute(null));
            Assert.True(viewModel.TogglePlaybackCommand.CanExecute(null));
            Assert.False(viewModel.IsPlaybackRunning);

            viewModel.TogglePlaybackCommand.Execute(null);
            Assert.True(viewModel.IsPlaybackRunning);
            viewModel.TogglePlaybackCommand.Execute(null);
            Assert.False(viewModel.IsPlaybackRunning);

            viewModel.NextFrameCommand.Execute(null);
            Assert.Equal(1, viewModel.SelectedFrameSliderIndex);
            viewModel.TogglePlaybackCommand.Execute(null);
            Assert.True(viewModel.IsPlaybackRunning);

            viewModel.ClearCsvCommand.Execute(null);

            Assert.False(viewModel.IsPlaybackRunning);
            Assert.False(viewModel.HasCsvFiles);
            Assert.False(viewModel.HasCsvData);
            Assert.Empty(viewModel.CsvFiles);
            Assert.Empty(viewModel.FrameOptions);
            Assert.Equal(0, viewModel.SelectedFrameSliderIndex);
            Assert.Equal(0, viewModel.SelectedFrameSliderMaximum);
            Assert.Equal("Manual frame", viewModel.FrameSummaryText);
            Assert.False(viewModel.CanStepFrames);
            Assert.False(viewModel.CanPlayFrames);
            Assert.False(viewModel.ClearCsvCommand.CanExecute(null));
            Assert.False(viewModel.TogglePlaybackCommand.CanExecute(null));
            viewModel.SelectRegularPad(0);
            Assert.Equal("0", viewModel.SelectedBeforeText);
        }
        finally
        {
            viewModel.ClearCsvCommand.Execute(null);
            File.Delete(csvPath);
        }
    }

    [Fact]
    public void ClearSelectedOverrideCommand_RestoresOnlySelectedPadAndUpdatesCanExecute()
    {
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession())
        {
            GlobalValue = 10d,
        };
        viewModel.SelectRegularPad(0);
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        viewModel.SelectedManualValue = 123d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);
        viewModel.SelectRegularPad(1);
        viewModel.SelectedManualValue = 234d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);
        Assert.True(viewModel.ClearSelectedOverrideCommand.CanExecute(null));

        viewModel.ClearSelectedOverrideCommand.Execute(null);

        Assert.Equal(1, viewModel.SelectedRegularPadId);
        Assert.Equal(10d, viewModel.SelectedManualValue);
        Assert.Equal("10", viewModel.SelectedBeforeText);
        Assert.False(viewModel.HasSelectedOverride);
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        Assert.True(viewModel.HasManualOverrides);

        viewModel.SelectRegularPad(0);
        Assert.Equal("123", viewModel.SelectedBeforeText);
        Assert.True(viewModel.HasSelectedOverride);
        Assert.True(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        viewModel.SelectRegularPad(1);
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
    }

    [Fact]
    public void ClearAllManualOverridesCommand_RestoresBothPadsToGlobalBaseline()
    {
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession())
        {
            GlobalValue = 10d,
        };
        Assert.False(viewModel.ClearAllManualOverridesCommand.CanExecute(null));
        viewModel.SelectRegularPad(0);
        viewModel.SelectedManualValue = 123d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);
        viewModel.SelectRegularPad(1);
        viewModel.SelectedManualValue = 234d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);
        Assert.True(viewModel.ClearAllManualOverridesCommand.CanExecute(null));

        viewModel.ClearAllManualOverridesCommand.Execute(null);

        Assert.False(viewModel.HasManualOverrides);
        Assert.False(viewModel.HasSelectedOverride);
        Assert.False(viewModel.ClearAllManualOverridesCommand.CanExecute(null));
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        Assert.Equal(10d, viewModel.SelectedManualValue);
        Assert.Equal("10", viewModel.SelectedBeforeText);
        viewModel.SelectRegularPad(0);
        Assert.False(viewModel.HasSelectedOverride);
        Assert.Equal("10", viewModel.SelectedBeforeText);
    }

    [Fact]
    public void UseCurrentCopperAsPathEndpointCommands_CaptureIndependentEndpoints()
    {
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession());
        Assert.False(viewModel.UseCurrentCopperAsPathStartCommand.CanExecute(null));
        Assert.False(viewModel.UseCurrentCopperAsPathEndCommand.CanExecute(null));
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);
        Assert.True(viewModel.UseCurrentCopperAsPathStartCommand.CanExecute(null));
        Assert.True(viewModel.UseCurrentCopperAsPathEndCommand.CanExecute(null));

        viewModel.MoveCopperToWorldPoint(new Point2(0.25d, 0.75d));
        viewModel.UseCurrentCopperAsPathStartCommand.Execute(null);
        viewModel.MoveCopperToWorldPoint(new Point2(1.75d, 0.25d));
        viewModel.UseCurrentCopperAsPathEndCommand.Execute(null);

        Assert.Equal(0.25m, viewModel.CopperPathStartX);
        Assert.Equal(0.75m, viewModel.CopperPathStartY);
        Assert.Equal(1.75m, viewModel.CopperPathEndX);
        Assert.Equal(0.25m, viewModel.CopperPathEndY);
    }

    [Fact]
    public void SelectCopperPathReplayRowCommand_SelectsRowAndMovesCopperToItsCenter()
    {
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession())
        {
            CopperDiameter = 0.5d,
            CopperPeakValue = 400d,
        };
        viewModel.ReplayCopperPath(new Point2(0.5d, 0.5d), new Point2(1.5d, 0.5d), stepCount: 2);
        var row = viewModel.CopperPathReplayRows[1];
        Assert.True(viewModel.IsManualSource);

        viewModel.SelectCopperPathReplayRowCommand.Execute(row);

        Assert.Same(row, viewModel.SelectedCopperPathReplayRow);
        Assert.True(viewModel.IsCopperSource);
        Assert.Equal(row.CenterX, viewModel.CopperCenterX);
        Assert.Equal(row.CenterY, viewModel.CopperCenterY);
        viewModel.SelectRegularPad(1);
        Assert.Equal("400", viewModel.SelectedBeforeText);
        viewModel.SelectRegularPad(0);
        Assert.Equal("0", viewModel.SelectedBeforeText);
    }

    [Fact]
    public void FitCanvasCommand_RaisesOneFitRequest()
    {
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession());
        var fitRequests = 0;
        object? requestSender = null;
        viewModel.FitCanvasRequested += (sender, _) =>
        {
            fitRequests++;
            requestSender = sender;
        };

        viewModel.FitCanvasCommand.Execute(null);

        Assert.Equal(1, fitRequests);
        Assert.Same(viewModel, requestSender);
    }
}
