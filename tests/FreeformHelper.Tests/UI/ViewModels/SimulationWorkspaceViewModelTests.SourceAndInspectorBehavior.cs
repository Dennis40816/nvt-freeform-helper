// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class SimulationWorkspaceViewModelTests
{
    [Fact]
    public async Task CsvInputSummary_AfterImport_IdentifiesLoadedFramesAndSelectedProjectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), BuildSession());
        viewModel.SelectRegularPad(0);
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(
            option => option.Value == SimulationInputSourceMode.Csv);

        Assert.Equal("CSV not loaded", viewModel.InputPrimarySummaryText);
        Assert.Equal("Import CSV to project frames onto the current regular grid.", viewModel.InputSecondarySummaryText);
        Assert.Equal("CSV not loaded", viewModel.SelectedSourceText);
        Assert.False(viewModel.ShowSelectedOverrideEditor);
        Assert.Contains("projected onto the regular grid", viewModel.InputContractDetailText, StringComparison.Ordinal);

        var path = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            timestamp(mm:ss:nnn):"09:14:265",Frame index:"1",Break point index:"0",DiffData:
            200,80
            """);
        viewModel.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([path]);

        await viewModel.ImportCsvCommand.ExecuteAsync(null);

        Assert.Equal("CSV 1 file(s)", viewModel.InputPrimarySummaryText);
        Assert.Equal("Compatible frames 2", viewModel.InputSecondarySummaryText);
        Assert.Equal("CSV frame projection", viewModel.SelectedSourceText);
        Assert.Equal(viewModel.FrameOptions[0].Display, viewModel.SelectedFrameDisplayText);

        viewModel.NextFrameCommand.Execute(null);

        Assert.Equal(viewModel.FrameOptions[1].Display, viewModel.SelectedFrameDisplayText);
        Assert.Equal("200", viewModel.SelectedBeforeText);
        Assert.Equal("100", viewModel.SelectedAfterText);
        Assert.False(viewModel.ShowSelectedOverrideEditor);
    }

    [Fact]
    public void CopperInputSummary_AfterContactAndPositionChange_DescribesTheCurrentSignal()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), BuildSession());
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(
            option => option.Value == SimulationInputSourceMode.Copper);
        viewModel.SelectedCopperContactModelOption = viewModel.CopperContactModelOptions.Single(
            option => option.Value == SimulationContactModelMode.FingerPress);
        viewModel.CopperDiameter = 0.5d;

        viewModel.MoveCopperToWorldPoint(new Point2(0.5d, 0.5d));
        viewModel.SelectRegularPad(0);

        Assert.Equal("Signal peak 360", viewModel.InputPrimarySummaryText);
        Assert.Equal("X 0.5 · Y 0.5 · Ø 0.5", viewModel.InputSecondarySummaryText);
        Assert.Equal("Finger press (~360) · X 0.5 · Y 0.5 · Ø 0.5", viewModel.SelectedFrameDisplayText);
        Assert.Equal("Copper frame", viewModel.FrameSummaryText);
        Assert.Contains("Peak 360.", viewModel.CopperContactModelSummaryText, StringComparison.Ordinal);
        Assert.Equal("360", viewModel.SelectedBeforeText);
        Assert.False(viewModel.ShowSelectedOverrideEditor);
    }
}
