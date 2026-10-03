using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchApplySimulationReviewUseCaseTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] SimulationDiffIndices = { 10, 11 };
    private static readonly int[] V21LegacyValues = { 10, 127, 94, 11, 1, 56, NullDiffValue, 0, 0 };

    [Fact]
    public void BuildSnapshot_WhenV22RowsImported_UsesSharedSimulationPath()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, NullDiffValue, 0, 0),
                comment: "sample")
        });
        var useCase = new NotchApplySimulationReviewUseCase();

        var sourcePaths = new List<string> { csvPath };
        var dataset = useCase.ImportSources(sourcePaths, grid);
        var snapshot = NotchApplySimulationReviewUseCase.BuildSnapshot(
            dataset,
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            table,
            NotchAlgorithmVersion.V22,
            NullDiffValue,
            NotchApplySimulationAggregationMode.SingleFrame,
            selectedFrameIndex: 0);

        Assert.True(snapshot.Result.IsSupported);
        Assert.Equal("V2.2 diff-centric apply", snapshot.Result.ContractText);
        Assert.Single(snapshot.Dataset.Files);
        Assert.Equal(1, snapshot.Dataset.CompatibleFrameCount);
        Assert.Single(snapshot.Result.Actions);
        Assert.True(snapshot.Result.Histograms.Before.HasBins);
        Assert.True(snapshot.Result.Heatmap.HasHotspots);
        Assert.Collection(
            snapshot.Result.Cells.OrderBy(static cell => cell.DiffIndex),
            cell => Assert.Equal((10, 100d, 50d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((11, 50d, 94d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)));
    }

    [Fact]
    public void BuildSnapshot_WhenV21RowsImported_UsesSharedProjectedCanonicalPath()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                values: V21LegacyValues,
                comment: "legacy")
        });
        var useCase = new NotchApplySimulationReviewUseCase();

        var sourcePaths = new List<string> { csvPath };
        var dataset = useCase.ImportSources(sourcePaths, grid);
        var snapshot = NotchApplySimulationReviewUseCase.BuildSnapshot(
            dataset,
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            table,
            NotchAlgorithmVersion.V21,
            NullDiffValue,
            NotchApplySimulationAggregationMode.SingleFrame,
            selectedFrameIndex: 0);

        Assert.True(snapshot.Result.IsSupported);
        Assert.Equal("V2.1 projected apply (v2.2 canonical)", snapshot.Result.ContractText);
        Assert.Single(snapshot.Result.Actions);
        Assert.Collection(
            snapshot.Result.Cells.OrderBy(static cell => cell.DiffIndex),
            cell => Assert.Equal((10, 100d, 50d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((11, 50d, 93d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)));
    }

}
