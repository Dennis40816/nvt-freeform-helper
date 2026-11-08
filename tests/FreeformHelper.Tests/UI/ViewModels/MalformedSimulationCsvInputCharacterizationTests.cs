// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MalformedSimulationCsvInputCharacterizationTests
{
    private static readonly double[] _loadedValues = [100d, 50d];
    private static readonly double[] _wrongWidthValues = [7d, 8d, 9d];
    private static readonly double[] _lastFrameValues = [10_000d, 10_001d];

    [AvaloniaFact]
    public async Task ImportCsv_EmptyOrHeaderOnly_CharacterizationAddsEmptyFileWithoutWarningAndKeepsLoadedFrameAsync()
    {
        foreach (var content in new[] { string.Empty, "Xch:\"2\",Ych:\"1\"\n" })
        {
            var vm = await CreateLoadedWorkspaceAsync();
            var previous = CaptureCells(vm);
            var path = TestFiles.WriteTempCsv(content);

            await ImportAsync(vm, path);

            Assert.Equal("V2.2 diff-centric apply · frames 1 · total Δ 0", vm.StatusText);
            Assert.Equal(previous, CaptureCells(vm));
            Assert.Equal("Frame 1/1", vm.FrameSummaryText);
            Assert.Equal(2, vm.CsvFiles.Count);
            Assert.Equal("0/0 compatible", vm.CsvFiles[1].SummaryText);
            Assert.Equal("Files 2 · Compatible frames 1/1 · Expected grid 2 x 1", vm.CsvInspectorSummaryText);
            Assert.Empty(GetSnapshot(vm).Dataset.Diagnostics);
        }
    }

    [AvaloniaFact]
    public async Task ImportCsv_WrongColumnCount_CharacterizationListsIncompatibleFrameAndKeepsLoadedValuesAndStatusAsync()
    {
        var vm = await CreateLoadedWorkspaceAsync();
        var previous = CaptureCells(vm);
        var path = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,7,8,9\n");

        await ImportAsync(vm, path);

        Assert.Equal("V2.2 diff-centric apply · frames 1 · total Δ 0", vm.StatusText);
        Assert.Equal(previous, CaptureCells(vm));
        Assert.Equal("Frame 1/1", vm.FrameSummaryText);
        Assert.Equal("0/1 compatible", vm.CsvFiles[1].SummaryText);
        Assert.Equal("Files 2 · Compatible frames 1/2 · Expected grid 2 x 1", vm.CsvInspectorSummaryText);
        var incompatible = GetSnapshot(vm).Dataset.Frames[1];
        Assert.False(incompatible.Projection.IsCompatible);
        Assert.Contains("1x3", incompatible.Projection.Diagnostic!, StringComparison.Ordinal);
        Assert.Equal(_wrongWidthValues, Assert.Single(incompatible.Frame.Rows));
    }

    [AvaloniaFact]
    public async Task ImportCsv_NonNumericCell_ReportsImportFailureWithoutThrowingAndKeepsLoadedResultAsync()
    {
        var vm = await CreateLoadedWorkspaceAsync();
        var previous = CaptureCells(vm);
        var previousSnapshot = GetSnapshot(vm);
        var path = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData:\n7,abc\n");
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([path]);

        await vm.ImportCsvCommand.ExecuteAsync(null);

        Assert.Contains("CSV import failed", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(path), vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("line 2", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(previous, CaptureCells(vm));
        Assert.Same(previousSnapshot, GetSnapshot(vm));
        Assert.Equal("Frame 1/1", vm.FrameSummaryText);
        Assert.Single(vm.CsvFiles);
        Assert.Equal("1/1 compatible", vm.CsvFiles[0].SummaryText);
    }

    [AvaloniaFact]
    public async Task ImportCsv_TenThousandFrames_PreservesLoadedFrameAndImportsEveryFrameThroughTheLastValuesAsync()
    {
        const int frameCount = 10_000;
        var vm = await CreateLoadedWorkspaceAsync();
        var previous = CaptureCells(vm);
        var content = new StringBuilder("Xch:\"2\",Ych:\"1\"\n");
        for (var index = 0; index < frameCount; index++)
        {
            content.Append(CultureInfo.InvariantCulture, $"timestamp(0:0:0),Frame index:\"{index}\",DiffData,{index + 1},{index + 2}\n");
        }

        await ImportAsync(vm, TestFiles.WriteTempCsv(content.ToString()));

        Assert.Equal("V2.2 diff-centric apply · frames 1 · total Δ 0", vm.StatusText);
        Assert.Equal(previous, CaptureCells(vm));
        Assert.Equal(frameCount + 1, vm.FrameOptions.Count);
        Assert.Equal(frameCount, vm.SelectedFrameSliderMaximum);
        Assert.Equal("10000/10000 compatible", vm.CsvFiles[1].SummaryText);
        Assert.Equal("Files 2 · Compatible frames 10001/10001 · Expected grid 2 x 1", vm.CsvInspectorSummaryText);
        var frames = GetSnapshot(vm).Dataset.Frames;
        Assert.Equal(frameCount + 1, frames.Count);
        Assert.Equal(_loadedValues, Assert.Single(frames[0].Frame.Rows));
        Assert.Equal(_lastFrameValues, Assert.Single(frames[frameCount].Frame.Rows));

        vm.SelectedFrameSliderIndex = frameCount;

        Assert.Equal("Frame 10001/10001", vm.FrameSummaryText);
        Assert.Equal(_lastFrameValues, GetSnapshot(vm).SimulationResult.Cells.Select(static cell => cell.BeforeValue));
        Assert.Equal("V2.2 diff-centric apply · frames 1 · total Δ 0", vm.StatusText);
    }

    private static async Task<SimulationWorkspaceViewModel> CreateLoadedWorkspaceAsync()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        var vm = new SimulationWorkspaceViewModel(new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
        await ImportAsync(vm, TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,100,50\n"));
        Assert.Equal(_loadedValues, GetSnapshot(vm).SimulationResult.Cells.Select(static cell => cell.BeforeValue));
        Assert.True(vm.IsCsvSource);
        Assert.True(vm.HasCsvData);
        return vm;
    }

    private static async Task ImportAsync(SimulationWorkspaceViewModel vm, string path)
    {
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([path]);
        await vm.ImportCsvCommand.ExecuteAsync(null);
    }

    private static SimulationScenarioSnapshot GetSnapshot(SimulationWorkspaceViewModel vm) =>
        Assert.IsType<SimulationScenarioSnapshot>(vm.GetSimulationScenarioSnapshot());

    private static (int Id, double Before, double After, double Delta)[] CaptureCells(SimulationWorkspaceViewModel vm) =>
        GetSnapshot(vm).SimulationResult.Cells.Select(static cell =>
            (cell.RegularPadId, cell.BeforeValue, cell.AfterValue, cell.DeltaValue)).ToArray();
}
