// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationCsvImportFailureTests
{
    [AvaloniaFact]
    public async Task ImportCsv_AfterNonNumericCellFailure_ImportsValidFileWithoutRetryingRejectedSourceAsync()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        var vm = new SimulationWorkspaceViewModel(new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
        var badPath = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData:\n7,abc\n");
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([badPath]);

        await vm.ImportCsvCommand.ExecuteAsync(null);

        var goodPath = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,100,50\n");
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([goodPath]);

        await vm.ImportCsvCommand.ExecuteAsync(null);

        var snapshot = Assert.IsType<SimulationScenarioSnapshot>(vm.GetSimulationScenarioSnapshot());
        Assert.Equal(goodPath, Assert.Single(snapshot.Dataset.Files).SourcePath);
        Assert.Single(vm.CsvFiles);
        Assert.Single(vm.FrameOptions);
        Assert.Collection(snapshot.SimulationResult.Cells,
            cell => Assert.Equal(100d, cell.BeforeValue),
            cell => Assert.Equal(50d, cell.BeforeValue));
        Assert.Equal("V2.2 diff-centric apply · frames 1 · total Δ 0", vm.StatusText);
    }
}
