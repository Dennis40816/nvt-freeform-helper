using System.Reflection;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DiffFrameCsvFixtureTests
{
    [ExampleDataFact]
    public void Tm81SingleFingerFixture_ImportsWithExpectedDimensionsAndFrameCount()
    {
        var fixturePath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "simulation-single-finger.csv");

        var file = DiffFrameCsvImporter.Import(fixturePath);

        Assert.Equal(36, file.DeclaredCols);
        Assert.Equal(18, file.DeclaredRows);
        Assert.Equal(24, file.Frames.Count);
        Assert.All(file.Frames, frame =>
        {
            Assert.Equal(18, frame.RowCount);
            Assert.Equal(36, frame.ColCount);
        });
    }

    [ExampleDataFact]
    public async Task Tm81SingleFingerFixture_KeepsUnassignedRegularPadsAtZero()
    {
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "TM 8.1", "TM8.1.json");
        var fixturePath = Path.Combine(repoRoot, "example", "TM 8.1", "simulation-single-finger.csv");

        var vm = new FreeformHelperViewModel();
        var loadMethod = typeof(FreeformHelperViewModel).GetMethod("LoadProjectFromPathAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(loadMethod);

        var loadTask = Assert.IsAssignableFrom<Task>(loadMethod!.Invoke(vm, new object[] { projectPath }));
        await loadTask;

        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        var grid = Assert.IsType<RegularGrid>(gridField!.GetValue(vm));
        var file = DiffFrameCsvImporter.Import(fixturePath);

        foreach (var frame in file.Frames)
        {
            var projection = DiffFrameGridProjectionService.ProjectFrame(
                frame,
                grid,
                NotchApplySimulationReviewUseCase.CsvProjectionRowOrigin);
            Assert.True(projection.IsCompatible, projection.Diagnostic);

            foreach (var cell in projection.Cells)
            {
                var regularPad = grid.Pads[cell.RegularPadId];
                if (regularPad.MatchedCadPadId is null)
                {
                    Assert.Equal(0d, cell.Value);
                }
            }
        }
    }

    [ExampleDataFact]
    public async Task Tm81SingleFingerFixture_KeepsDiffMismatchedStep1PadsAtZero()
    {
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "TM 8.1", "TM8.1.json");
        var fixturePath = Path.Combine(repoRoot, "example", "TM 8.1", "simulation-single-finger.csv");

        var vm = new FreeformHelperViewModel();
        var loadMethod = typeof(FreeformHelperViewModel).GetMethod("LoadProjectFromPathAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(loadMethod);

        var loadTask = Assert.IsAssignableFrom<Task>(loadMethod!.Invoke(vm, new object[] { projectPath }));
        await loadTask;

        var session = await vm.CreateSimulationWorkspaceSessionAsync();
        Assert.NotNull(session);

        var useCase = new NotchApplySimulationReviewUseCase();
        var dataset = useCase.ImportSources(new[] { fixturePath }, session!.Grid);
        var version = session.Table.Rows.Select(static row => row.Version).Distinct().Single();
        var snapshot = NotchApplySimulationReviewUseCase.BuildSnapshot(
            dataset,
            session.Grid,
            session.ActiveRegularPadIds,
            session.Table,
            version,
            session.NullDiffValue,
            NotchApplySimulationAggregationMode.SingleFrame,
            selectedFrameIndex: 0);

        var reg72 = Assert.Single(snapshot.Result.Cells, static cell => cell.RegularPadId == 72);
        Assert.Equal(0d, reg72.BeforeValue);
    }

    [ExampleDataFact]
    public async Task Tm81SingleFingerFixture_AllowsAfterValueForInactiveTargetDiffPads()
    {
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "TM 8.1", "TM8.1.json");
        var fixturePath = Path.Combine(repoRoot, "example", "TM 8.1", "simulation-single-finger.csv");

        var vm = new FreeformHelperViewModel();
        var loadMethod = typeof(FreeformHelperViewModel).GetMethod("LoadProjectFromPathAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(loadMethod);

        var loadTask = Assert.IsAssignableFrom<Task>(loadMethod!.Invoke(vm, new object[] { projectPath }));
        await loadTask;

        var session = await vm.CreateSimulationWorkspaceSessionAsync();
        Assert.NotNull(session);

        var useCase = new NotchApplySimulationReviewUseCase();
        var dataset = useCase.ImportSources(new[] { fixturePath }, session!.Grid);
        var snapshot = NotchApplySimulationReviewUseCase.BuildSnapshot(
            dataset,
            session.Grid,
            session.ActiveRegularPadIds,
            session.Table,
            NotchAlgorithmVersion.V22,
            session.NullDiffValue,
            NotchApplySimulationAggregationMode.SingleFrame,
            selectedFrameIndex: 0);

        var reg473 = Assert.Single(snapshot.Result.Cells, static cell => cell.RegularPadId == 473);
        Assert.Equal(0d, reg473.BeforeValue);
        Assert.True(reg473.AfterValue > 0d, $"Expected REG473 to receive a non-zero after value, but got {reg473.AfterValue}.");
    }

    [ExampleDataFact]
    public void Tm81SingleFingerFixture_FirstFrameStartsFromTopVisualRows()
    {
        var fixturePath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "simulation-single-finger.csv");
        var file = DiffFrameCsvImporter.Import(fixturePath);
        var grid = BuildFixtureShapeGrid(rows: 18, cols: 36);

        var projection = DiffFrameGridProjectionService.ProjectFrame(
            file.Frames[0],
            grid,
            NotchApplySimulationReviewUseCase.CsvProjectionRowOrigin);

        Assert.True(projection.IsCompatible, projection.Diagnostic);
        var firstPositiveCell = projection.Cells.First(static cell => cell.Value > 0d);
        Assert.Equal(grid.Rows - 1 - firstPositiveCell.FrameRow, firstPositiveCell.RegularRow);
        Assert.Equal(firstPositiveCell.FrameCol, firstPositiveCell.RegularCol);
    }

    private static RegularGrid BuildFixtureShapeGrid(int rows, int cols)
    {
        return TestGeometryFactory.CreateRegularGrid(rows, cols);
    }
}
