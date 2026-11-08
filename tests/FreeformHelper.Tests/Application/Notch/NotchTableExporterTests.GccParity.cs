using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableExporterTests
{
    [Fact]
    public void ExportAsCInitializer_GccCompilesFwStyleExports_WhenGccIsAvailable()
    {
        if (!IsGccAvailable())
        {
            return;
        }

        var v22OnlyTable = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                v22Node: new NotchV22Node(10, 120, 20, 30, 65535, 0, 0),
                comment: "v22-only"),
        });
        var v21OnlyTable = new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                values: _v21OnlyPayload,
                comment: "v21-only"),
        });

        AssertCCompilesWithGcc(NotchTableExporter.ExportAsCInitializer(
            v22OnlyTable,
            CreateCadSet(),
            CreateGrid(),
            CreateProjectSettings(),
            NotchExportProfile.Debug));
        AssertCCompilesWithGcc(NotchTableExporter.ExportAsCInitializer(
            v21OnlyTable,
            CreateCadSet(),
            CreateGrid(),
            CreateProjectSettings(),
            NotchExportProfile.Debug));
    }

    [Fact]
    public void ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22_WhenGccIsAvailable()
    {
        if (!IsGccAvailable())
        {
            return;
        }

        var grid = CreateRuntimeParityGrid();
        var table = CreateRuntimeParityV22Table();
        var expected = SimulateWithCSharp(grid, table, NotchAlgorithmVersion.V22);
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(2),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        var actual = RunCProgramWithGcc(CreateV22RuntimeParityHarnessSource(generatedSource));

        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], actual[index], precision: 6);
        }
    }

    [Fact]
    public void ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22ContinuationRows_WhenGccIsAvailable()
    {
        int[] diffIndices = [10, 11, 12, 13];
        double[] beforeValues = [100d, 0d, 0d, 0d];
        double[] expected = [40d, 60d, 20d, 10d];
        var grid = TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                v22Node: new NotchV22Node(10, 120, 11, 60, 12, 20, 0),
                comment: "MAIN"),
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                v22Node: new NotchV22Node(10, 100, 13, 10, 65535, 0, 1),
                comment: "CONT"),
        });
        var simulated = SimulateWithCSharp(
            grid,
            table,
            NotchAlgorithmVersion.V22,
            beforeValues: beforeValues);
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);
        var nodeLines = new[]
        {
            ExtractSingleTableRowLine(generatedSource, "// CONT;"),
            ExtractSingleTableRowLine(generatedSource, "// MAIN;"),
        };

        Assert.Equal(expected, simulated);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (2u)", generatedSource, StringComparison.Ordinal);
        Assert.Equal(
            [
                "    {    10, 100,            13,   10, NHC_DIFF_NONE,    0, NHC_FLAG_CONTINUATION }, // CONT; NODE KEEP=90% MOVE=10%",
                "    {    10, 120,            11,   60,            12,   20, NHC_FLAG_NONE }, // MAIN; NODE KEEP=40% MOVE=80%",
            ],
            nodeLines);
        if (!IsGccAvailable())
        {
            return;
        }

        var firmware = RunCProgramWithGcc(CreateSingleIcRuntimeParityHarnessSource(
            generatedSource,
            diffIndices,
            [100, 0, 0, 0]));

        Assert.Equal(expected, firmware);
    }

    [Fact]
    public void ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22Int16Truncation_WhenGccIsAvailable()
    {
        int[] diffIndices = [10, 11];
        double[] beforeValues = [1d, 0d];
        double[] expected = [1d, 0d];
        var grid = TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 150, 11, 50, 65535, 0, 0),
                comment: "INT16 truncation")
        });
        var projection = CreateRuntimeParityProjection(grid, beforeValues);
        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [projection],
            table,
            NotchAlgorithmVersion.V22,
            65535));
        var simulated = simulation.Cells.Select(static cell => cell.AfterValue).ToArray();
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Equal(expected, simulated);
        Assert.Empty(simulation.Diagnostics);
        var action = Assert.Single(simulation.Actions);
        Assert.Equal((150, 100, 1d, 1d),
            (action.CombinePercent, action.SourceRetainedPercent, action.SourceBeforeValue, action.SourceAfterValue));
        var leg = Assert.Single(action.Legs);
        Assert.Equal((11, 50, 0d),
            (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue));
        if (!IsGccAvailable())
        {
            return;
        }

        var firmware = RunCProgramWithGcc(CreateSingleIcRuntimeParityHarnessSource(
            generatedSource,
            diffIndices,
            [1, 0]));

        Assert.Equal(expected, firmware);
    }

    [Fact]
    public void ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21_WhenGccIsAvailable()
    {
        if (!IsGccAvailable())
        {
            return;
        }

        var grid = CreateRuntimeParityGrid();
        var table = CreateRuntimeParityV21Table();
        var expected = SimulateWithCSharp(grid, table, NotchAlgorithmVersion.V21);
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(2),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        var actual = RunCProgramWithGcc(CreateV21RuntimeParityHarnessSource(generatedSource));

        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21LegacyRegularAnchor_WhenGccIsAvailable()
    {
        if (!IsGccAvailable())
        {
            return;
        }

        var grid = CreateRuntimeParityGrid();
        var table = CreateRuntimeParityLegacyV21Table();
        var expected = SimulateWithCSharp(
            grid,
            table,
            NotchAlgorithmVersion.V21,
            NotchComputationMode.LegacyRegularAnchor);
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(2),
            grid,
            CreateProjectSettings(NotchComputationMode.LegacyRegularAnchor),
            NotchExportProfile.Debug);

        var actual = RunCProgramWithGcc(CreateV21RuntimeParityHarnessSource(generatedSource));

        Assert.Equal(new double[] { 75, 50, 13, 80, -11, 30 }, expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExportAsCInitializer_GccAndCSharpSimulationMatchV21Q7BoundaryContract_WhenGccIsAvailable()
    {
        if (!IsGccAvailable())
        {
            return;
        }

        var grid = CreateV21Q7BoundaryGrid();
        var table = CreateV21Q7BoundaryTable();
        var projection = CreateV21Q7BoundaryProjection(grid);
        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            NotchAlgorithmVersion.V21,
            65535));
        var simulatedValues = simulation.Cells
            .OrderBy(static cell => cell.RegularRow)
            .ThenBy(static cell => cell.RegularCol)
            .Select(static cell => cell.AfterValue)
            .ToArray();
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(1),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        var firmwareValues = RunCProgramWithGcc(CreateV21Q7BoundaryRuntimeHarnessSource(generatedSource));

        Assert.True(simulation.IsSupported);
        Assert.Empty(simulation.Diagnostics);
        Assert.Equal(_expectedV21Q7BoundaryRuntimeValues, firmwareValues);
        Assert.Equal(_expectedV21Q7BoundaryRuntimeValues, simulatedValues);
        Assert.Equal(_expectedV21Q7BoundarySignedPercents, simulation.Actions.SelectMany(static action => action.Legs).Select(static leg => leg.RatioPercent));
        Assert.Equal(_expectedV21Q7BoundaryLegDeltas, simulation.Actions.SelectMany(static action => action.Legs).Select(static leg => leg.DeltaValue));
        Assert.Equal(_expectedV21Q7BoundaryRuntimeValues.Where((_, index) => index % 2 == 0), simulation.Actions.Select(static action => action.SourceAfterValue));
    }

    [Fact]
    public void ExportAsCInitializer_V21ThreeTerms_SplitsNodesAndPreservesInt16Wrap()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_v21MultiTermWrapDiffs);
        var table = new NotchTable(_v21MultiTermWrapSourceDiffs
            .Select(sourceDiff => new NotchTableRow(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: sourceDiff,
                regularPadIndex: sourceDiff,
                cadPadId: 1000 + sourceDiff,
                values: [sourceDiff, 100, 200, 20, 1, 128, 65535, 0, 0],
                comment: $"src{sourceDiff}"))
            .ToArray());
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(1),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);
        var destinationNodeLines = generatedSource
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.TrimEnd('\r'))
            .Where(static line => line.Contains("{    20, 100, 100,", StringComparison.Ordinal))
            .ToArray();
        var simulatedValues = SimulateWithCSharp(
            grid,
            table,
            NotchAlgorithmVersion.V21,
            beforeValues: [32767d, 32767d, 32767d, 0d]);

        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (2u)", generatedSource, StringComparison.Ordinal);
        Assert.Contains("    INT16 s16Sum;", generatedSource, StringComparison.Ordinal);
        Assert.Equal(
            [
                "    {    20, 100, 100,            10, NHC_TYPE_ADD,  128,            11, " +
                    "NHC_TYPE_ADD,  128 }, // LEG src10 srcD=10 | LEG src11 srcD=11",
                "    {    20, 100, 100,            12, NHC_TYPE_ADD,  128, NHC_DIFF_NONE, NHC_TYPE_NONE,    0 }, // LEG src12 srcD=12",
            ],
            destinationNodeLines);
        Assert.Equal([32767d, 32767d, 32767d, 32765d], simulatedValues);

        if (IsGccAvailable())
        {
            var firmwareValues = RunCProgramWithGcc(CreateV21MultiTermWrapHarnessSource(generatedSource));
            Assert.Equal(simulatedValues, firmwareValues);
        }
    }
}
