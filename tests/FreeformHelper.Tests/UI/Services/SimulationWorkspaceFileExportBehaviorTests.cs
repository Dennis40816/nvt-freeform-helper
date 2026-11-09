// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.Json;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceFileExportBehaviorTests
{
    private static async Task<bool> WriteTextAsync(string path, string content)
    {
        await TextExportWriter.WriteAsync(path, content, includeBom: true, TestContext.Current.CancellationToken);
        return true;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task JsonCommandThroughTextWriterSavesCurrentCopperReplayRowsAndSummaryAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateWorkspace();
        string path = TestFiles.NewTempFilePath("copper-path-replay-", ".json");
        viewModel.RequestSaveTextFileAsync = request => WriteTextAsync(path, request.Content);
        viewModel.ReplayCopperPath(new Point2(0.5d, 0.5d), new Point2(1.5d, 0.5d), stepCount: 2);
        var result = viewModel.ReplayCopperPath(new Point2(1.5d, 0.5d), new Point2(0.5d, 0.5d), stepCount: 3);
        var expected = CopperPillarPathReplayArtifactService.BuildSnapshot(result);
        Assert.NotEmpty(viewModel.CopperPathReplayRows);

        await viewModel.ExportCopperPathReplayJsonCommand.ExecuteAsync(null);

        Assert.True(File.Exists(path));
        var saved = JsonSerializer.Deserialize<CopperPillarPathReplayArtifactSnapshot>(TestFiles.ReadContent(path), _jsonOptions);
        Assert.NotNull(saved);
        Assert.Equal(expected.StepCount, saved.StepCount);
        Assert.Equal(expected.TotalEmsViolationCount, saved.TotalEmsViolationCount);
        Assert.Equal(expected.TotalNetFlowResidualCount, saved.TotalNetFlowResidualCount);
        Assert.Equal(expected.TotalTargetCoverageRiskCount, saved.TotalTargetCoverageRiskCount);
        Assert.Equal(expected.WorstStepIndex, saved.WorstStepIndex);
        Assert.Equal(viewModel.CopperPathReplayRows.Count, saved.Rows.Count);
        for (int index = 0; index < saved.Rows.Count; index++)
        {
            var row = viewModel.CopperPathReplayRows[index];
            // Compare the persisted fields; HasGlobalFlowResidual is explicitly excluded from the JSON contract.
            Assert.Equal(row with { HasGlobalFlowResidual = false }, saved.Rows[index]);
        }

        Assert.Contains("Exported JSON", viewModel.StatusText, StringComparison.Ordinal);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var table = new NotchTable(
        [
            new NotchTableRow(icIndex: 0, diffIndex: 10, regularPadIndex: 0, cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), comment: "sample"),
        ]);
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(grid, table, 65535, SourceRevision: 0,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
                CadPadsForCopperProjection:
                [
                    TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d),
                    TestGeometryFactory.CreateCadPad(101, "SENSOR", 1d, 0d, 2d, 1d),
                ],
                CadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [100] = 10, [101] = 11 },
                CadIcIndexByCadId: new Dictionary<int, int> { [100] = 0, [101] = 0 }))
        {
            CopperDiameter = 0.5d,
            CopperPeakValue = 400d,
            CopperBaselineValue = 0d,
        };
    }
}
