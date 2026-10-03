using System.Diagnostics;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchTableExporterTests
{
    private static readonly double[] UnitEdges = [0d, 10d];
    private static readonly string[] ExpectedV21PayloadPrefix = ["1", "2", "3", "4"];
    private static readonly string[] ExpectedV22PayloadPrefix = ["7", "8"];
    private static readonly int[] V21OnlyPayload = [10, 120, 120, 20, 1, 38, 65535, 0, 0];
    private static readonly double[] RuntimeParityBeforeValues = [100d, 50d, 25d, 80d, 40d, 30d];
    private static readonly double[] ExpectedFwBaseMaskRuntimeValues = [400d, 0d, 400d, 123d, 0d, 0d];
    private static readonly int[] V21Q7BoundaryMagnitudes = [0, 1, 127, 128, 129, 255];
    private static readonly int[] V21Q7BoundaryTypes = [1, 2];
    private static readonly int[] V21MultiTermWrapDiffs = [10, 11, 12, 20];
    private static readonly int[] V21MultiTermWrapSourceDiffs = [12, 10, 11];
    private static readonly int[] ExpectedV21Q7BoundarySignedPercents =
        [1, -1, 99, -99, 100, -100, 101, -101, 199, -199];
    private static readonly double[] ExpectedV21Q7BoundaryLegDeltas =
        [1d, -1d, 127d, -127d, 128d, -128d, 129d, -129d, 255d, -255d];
    private static readonly double[] ExpectedV21Q7BoundaryRuntimeValues =
    [
        128d, 0d,
        128d, 0d,
        127d, 1d,
        129d, -1d,
        1d, 127d,
        255d, -127d,
        0d, 128d,
        256d, -128d,
        0d, 129d,
        257d, -129d,
        0d, 255d,
        383d, -255d,
    ];

    [Fact]
    public void ExportAsCsv_UsesReviewContract_AndOrdersByFwDiffIndex()
    {
        var d9Values = new[] { 9, 1, 2 };
        var d3Values = new[] { 3, 1, 2 };
        var rows = new[]
        {
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 9, 9, 1009, d9Values, "d9"),
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 3, 3, 1003, d3Values, "d3"),
        };
        var table = new NotchTable(rows);
        _ = new NotchTableExporter();
        var csv = NotchTableExporter.ExportAsCsv(table);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            "row_number,version,version_code,ic_index,ic_number,fw_diff_idx,regular_pad_id,cad_pad_id,payload_width,payload_01,payload_02,payload_03,payload_04,payload_05,payload_06,payload_07,payload_08,payload_09,comment",
            lines[0].TrimEnd('\r'));

        var d3Columns = lines[1].TrimEnd('\r').Split(',');
        var d9Columns = lines[2].TrimEnd('\r').Split(',');
        Assert.Equal("1", d3Columns[0]);
        Assert.Equal("2.2", d3Columns[1]);
        Assert.Equal("30", d3Columns[2]);
        Assert.Equal("0", d3Columns[3]);
        Assert.Equal("1", d3Columns[4]);
        Assert.Equal("3", d3Columns[5]);
        Assert.Equal("3", d3Columns[6]);
        Assert.Equal("1003", d3Columns[7]);
        Assert.Equal("3", d3Columns[8]);
        Assert.Equal("3", d3Columns[9]);
        Assert.Equal("\"d3\"", d3Columns[^1]);
        Assert.Equal("2", d9Columns[0]);
        Assert.Equal("9", d9Columns[5]);
        Assert.Equal("\"d9\"", d9Columns[^1]);
    }

    [Fact]
    public void ExportAsCsv_UsesFixedPayloadColumnsForMixedValueLengthRows()
    {
        var v21Values = new[] { 1, 2, 3, 4 };
        var v22Values = new[] { 7, 8 };
        var rows = new[]
        {
            new NotchTableRow(NotchAlgorithmVersion.V21, 0, 1, 1, 1001, v21Values, "v21"),
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 2, 2, 1002, v22Values, "v22"),
        };
        var table = new NotchTable(rows);
        _ = new NotchTableExporter();
        var csv = NotchTableExporter.ExportAsCsv(table);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var v21Columns = lines[1].TrimEnd('\r').Split(',');
        var v22Columns = lines[2].TrimEnd('\r').Split(',');
        Assert.Equal(19, v21Columns.Length);
        Assert.Equal(19, v22Columns.Length);
        Assert.Equal("4", v21Columns[8]);
        Assert.Equal(ExpectedV21PayloadPrefix, v21Columns[9..13]);
        Assert.All(v21Columns[13..18], static value => Assert.Equal(string.Empty, value));
        Assert.Equal("\"v21\"", v21Columns[18]);
        Assert.Equal("2", v22Columns[8]);
        Assert.Equal(ExpectedV22PayloadPrefix, v22Columns[9..11]);
        Assert.All(v22Columns[11..18], static value => Assert.Equal(string.Empty, value));
        Assert.Equal("\"v22\"", v22Columns[18]);
    }

    [Fact]
    public void ExportAndSimulation_TypedAndUntypedV22Rows_NormalizeIdentically()
    {
        const int nullDiff = 65535;
        var typedRow = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 321,
            cadPadId: 4321,
            v22Node: new NotchV22Node(
                AnchorDiffIndex: 21,
                CombinePercent: 300,
                TargetDiffIndex1: 45,
                TargetRatioPercent1: 130,
                TargetDiffIndex2: nullDiff,
                TargetRatioPercent2: -130,
                Flags: 2),
            comment: "same-comment");
        var untypedRow = new NotchTableRow(
            NotchAlgorithmVersion.V22,
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 321,
            cadPadId: 4321,
            values: [21, 300, 45, 130, nullDiff, -130, 2, 999],
            comment: "same-comment");
        Assert.NotNull(typedRow.V22Node);
        Assert.Null(untypedRow.V22Node);

        var grid = TestGeometryFactory.CreateLinearRegularGrid([21, 45]);
        var typedTable = new NotchTable([typedRow]);
        var untypedTable = new NotchTable([untypedRow]);
        var typedText = NotchTableExporter.ExportAsCInitializer(
            typedTable,
            CreateCadSet(),
            grid,
            CreateProjectSettings());
        var untypedText = NotchTableExporter.ExportAsCInitializer(
            untypedTable,
            CreateCadSet(),
            grid,
            CreateProjectSettings());

        var typedNodeLine = ExtractSingleTableRowLine(typedText, "{    21, 255,");
        var untypedNodeLine = ExtractSingleTableRowLine(untypedText, "{    21, 255,");
        Assert.Equal(untypedNodeLine, typedNodeLine);
        Assert.Contains(
            "{    21, 255,            45,  100, NHC_DIFF_NONE, -100, NHC_FLAG_ANCHOR_OVERRIDE }",
            typedNodeLine,
            StringComparison.Ordinal);
        Assert.Equal(
            [255d, 100d],
            SimulateWithCSharp(grid, typedTable, NotchAlgorithmVersion.V22, beforeValues: [100d, 0d]));
        Assert.Equal(
            [255d, 100d],
            SimulateWithCSharp(grid, untypedTable, NotchAlgorithmVersion.V22, beforeValues: [100d, 0d]));
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void ExportAndSimulation_RejectNullDiffOutsideFirmwareUint16Domain(
        NotchAlgorithmVersion version)
    {
        // Before validation, V22 firmware produced [50,0] while simulation produced [50,50].
        const int invalidNullDiff = ushort.MaxValue + 1;
        const string expectedMessage = "NullValue must be in [0,65535].";
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, ushort.MaxValue]);
        var values = version == NotchAlgorithmVersion.V22
            ? new[] { 10, 100, ushort.MaxValue, 50, invalidNullDiff, 0, 0, 0 }
            : new[] { 10, 100, 100, ushort.MaxValue, 1, 64, invalidNullDiff, 0, 0 };
        var table = new NotchTable([
            new NotchTableRow(version, 0, 10, 0, 90, values, $"{version} UINT16 null collision")
        ]);
        var settings = CreateProjectSettings();
        settings.Notch.NullValue = invalidNullDiff;
        var exportException = Record.Exception(() =>
            NotchTableExporter.ExportAsCInitializer(
                table,
                CreateCadSet(),
                grid,
                settings,
                NotchExportProfile.Debug));
        var simulationException = Record.Exception(() => SimulateWithCSharp(
            grid,
            table,
            version,
            beforeValues: [100d, 0d],
            nullDiffValue: invalidNullDiff));

        Assert.Equal(expectedMessage, Assert.IsType<InvalidOperationException>(exportException).Message);
        Assert.Equal(expectedMessage, Assert.IsType<InvalidOperationException>(simulationException).Message);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 0, 100, 100, 100)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 100, 0, 0, 100)]
    public void ExportAndSimulation_PreserveUint16MaxDiffWhenSentinelIsAdjacent(
        NotchAlgorithmVersion version,
        NotchComputationMode computationMode,
        int beforeSource,
        int beforeUint16Max,
        int expectedSource,
        int expectedUint16Max)
    {
        const int nullDiff = ushort.MaxValue - 1;
        int[] diffIndices = [10, ushort.MaxValue];
        var values = version == NotchAlgorithmVersion.V22
            ? new[] { 10, 100, ushort.MaxValue, 100, nullDiff, 0, 0, 0 }
            : new[] { 10, 100, 100, ushort.MaxValue, 1, 128, nullDiff, 0, 0 };
        var grid = TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
        var table = new NotchTable([
            new NotchTableRow(version, 0, 10, 0, 93, values, "UINT16 max diff")
        ]);
        var settings = CreateProjectSettings(computationMode);
        settings.Notch.NullValue = nullDiff;

        var simulated = SimulateWithCSharp(
            grid,
            table,
            version,
            computationMode,
            beforeValues: [beforeSource, beforeUint16Max],
            nullDiffValue: nullDiff);
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            settings,
            NotchExportProfile.Release);

        double[] expected = [expectedSource, expectedUint16Max];
        Assert.Equal(expected, simulated);
        Assert.Contains("#define NHC_DIFF_NONE            (0xFFFEu)", generatedSource, StringComparison.Ordinal);
        var nodeLine = ExtractSingleTableRowLine(generatedSource, "// UINT16 max diff");
        Assert.Contains("65535", nodeLine, StringComparison.Ordinal);
        Assert.Contains("NHC_DIFF_NONE", nodeLine, StringComparison.Ordinal);
        if (IsGccAvailable())
        {
            var firmware = RunCProgramWithGcc(CreateSingleIcRuntimeParityHarnessSource(
                generatedSource,
                diffIndices,
                [beforeSource, beforeUint16Max]));
            Assert.Equal(expected, firmware);
        }
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation)]
    public void ExportAsCInitializer_SnapshotsNullDiffBeforeReadingCallerCollections(
        NotchAlgorithmVersion version,
        NotchComputationMode computationMode)
    {
        const int frozenNullDiff = ushort.MaxValue - 1;
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, ushort.MaxValue]);
        var values = version == NotchAlgorithmVersion.V22
            ? new[] { 10, 100, ushort.MaxValue, 100, frozenNullDiff, 0, 0, 0 }
            : new[] { 10, 100, 100, ushort.MaxValue, 1, 128, frozenNullDiff, 0, 0 };
        var table = new NotchTable([
            new NotchTableRow(version, 0, 10, 0, 94, values, "frozen UINT16 null")
        ]);
        var settings = CreateProjectSettings(computationMode);
        settings.Notch.NullValue = frozenNullDiff;
        var activeRegularPadIds = new MutatingReadOnlySet(
            grid.Pads.Select(static pad => pad.RegularPadId),
            () => settings.Notch.NullValue = ushort.MaxValue);

        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            settings,
            NotchExportProfile.Debug,
            activeRegularPadIds);

        Assert.Equal(ushort.MaxValue, settings.Notch.NullValue);
        Assert.Contains("#define NHC_DIFF_NONE            (0xFFFEu)", generatedSource, StringComparison.Ordinal);
        Assert.Contains(
            "65535",
            ExtractSingleTableRowLine(generatedSource, "// frozen UINT16 null"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmptyTablePreservesCommentOnlyFastPathForInvalidNullValue()
    {
        var settings = CreateProjectSettings();
        settings.Notch.NullValue = ushort.MaxValue + 1;

        var text = NotchTableExporter.ExportAsCInitializer(
            new NotchTable([]),
            CreateCadSet(),
            CreateGrid(),
            settings,
            NotchExportProfile.Debug);

        Assert.Equal("// Notch table is empty.\n", text);
    }

    [Fact]
    public void ExportAndSimulation_ShortV22Row_PreserveLegacyCompatibility()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var table = new NotchTable([
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 91,
                values: [999, 90, 80],
                comment: string.Empty)
        ]);

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Equal(
            "    {    10,  72, NHC_DIFF_NONE,    0, NHC_DIFF_NONE,    0, NHC_FLAG_NONE }, // LEGACY C=72%; NODE KEEP=72% MOVE=0%",
            ExtractSingleTableRowLine(text, "{    10,  72,"));
        Assert.Equal(
            [72d],
            SimulateWithCSharp(grid, table, NotchAlgorithmVersion.V22, beforeValues: [100d]));
    }

    [Fact]
    public void ExportAndSimulation_ShortV22Row_PreserveExactOverflowException()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var table = new NotchTable([
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 92,
                values: [10, 300, 100],
                comment: string.Empty)
        ]);

        var exportException = Assert.Throws<InvalidOperationException>(() =>
            NotchTableExporter.ExportAsCInitializer(
                table,
                CreateCadSet(),
                grid,
                CreateProjectSettings(),
                NotchExportProfile.Debug));
        var simulationException = Assert.Throws<InvalidOperationException>(() =>
            SimulateWithCSharp(grid, table, NotchAlgorithmVersion.V22, beforeValues: [100d]));

        const string expected = "v2.2 Combine ratio exceeds 255% at IC1 diff10: 300.00%.";
        Assert.Equal(expected, exportException.Message);
        Assert.Equal(expected, simulationException.Message);
    }

    [Fact]
    public void ExportAsCInitializer_V22NoOpFiltering_RemainsReleaseOnly()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var table = new NotchTable([
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 93,
                v22Node: new NotchV22Node(10, 100, 65535, 0, 65535, 0, 0),
                comment: "profile-no-op")
        ]);

        var debugText = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);
        var releaseText = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Release);

        Assert.Contains("#define USER_NHC_NODE_NUM        (1u)", debugText, StringComparison.Ordinal);
        Assert.Contains("profile-no-op", debugText, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM        (0u)", releaseText, StringComparison.Ordinal);
        Assert.DoesNotContain("profile-no-op", releaseText, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_ReleaseNoOpFiltering_PreservesPreFilterFirmwareSortContract()
    {
        var rows = Enumerable.Range(0, 17)
            .Select(index => new NotchTableRow(
                icIndex: 0,
                diffIndex: index,
                regularPadIndex: index,
                cadPadId: index + 1,
                v22Node: new NotchV22Node(
                    10,
                    index == 0 ? 100 : 101,
                    65535,
                    0,
                    65535,
                    0,
                    0),
                comment: index == 0 ? "drop-no-op" : $"keep-{index:D2}"))
            .ToArray();

        var text = NotchTableExporter.ExportAsCInitializer(
            new NotchTable(rows),
            CreateCadSet(),
            CreateGrid(),
            CreateProjectSettings(),
            NotchExportProfile.Release);
        var emittedComments = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(static line => line.Contains("// keep-", StringComparison.Ordinal))
            .Select(static line => line[(line.IndexOf("// ", StringComparison.Ordinal) + 3)..line.IndexOf(';')])
            .ToArray();

        Assert.DoesNotContain("drop-no-op", text, StringComparison.Ordinal);
        Assert.Equal(Enumerable.Range(1, 16).Select(static index => $"keep-{index:D2}"), emittedComments);
    }

    [Fact]
    public void ExportAndSimulation_V22Projection_PreservesSourceDiagnosticsWhileDroppingInvalidFirmwareIc()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 94,
                v22Node: new NotchV22Node(10, 150, 99, 50, 65535, 0, 0),
                comment: "valid-ic"),
            new(
                icIndex: -1,
                diffIndex: 10,
                regularPadIndex: 1,
                cadPadId: 95,
                v22Node: new NotchV22Node(10, 100, 65535, 0, 65535, 0, 0),
                comment: "invalid-ic"),
        });
        var projection = CreateRuntimeParityProjection(grid, [100d]);

        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [projection],
            table,
            NotchAlgorithmVersion.V22,
            65535));
        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Equal(
            [
                "Missing target diff IC1/diff99 in projected grid.",
                "Missing anchor diff IC0/diff10 in projected grid.",
            ],
            simulation.Diagnostics);
        var action = Assert.Single(simulation.Actions);
        var leg = Assert.Single(action.Legs);
        Assert.Equal((99, 50, 50d),
            (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue));
        Assert.Contains("#define USER_NHC_NODE_NUM        (1u)", text, StringComparison.Ordinal);
        Assert.Contains("valid-ic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-ic", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAndSimulation_V22Projection_DoesNotFabricateActionForDroppedInvalidIc()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        Assert.Single(grid.Pads).IcIndex = -1;
        var table = new NotchTable([
            new NotchTableRow(
                icIndex: -1,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 96,
                v22Node: new NotchV22Node(10, 50, 65535, 0, 65535, 0, 0),
                comment: "invalid-ic-source")
        ]);
        var projection = CreateRuntimeParityProjection(grid, [100d]);

        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [projection],
            table,
            NotchAlgorithmVersion.V22,
            65535));
        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Equal((100d, 100d, 0d),
            (Assert.Single(simulation.Cells).BeforeValue,
                simulation.Cells[0].AfterValue,
                simulation.Cells[0].DeltaValue));
        Assert.Empty(simulation.Actions);
        Assert.Empty(simulation.Diagnostics);
        Assert.Contains("#define USER_NHC_NODE_NUM        (0u)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-ic-source", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmitsPerIcDispatchTable_WhenRowsAreSparseAcrossIcs()
    {
        const int nullDiff = 65535;
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                v22Node: new NotchV22Node(10, 120, 20, 30, nullDiff, 0, 0),
                comment: "ic0-v22"),
            new(
                icIndex: 2,
                diffIndex: 30,
                regularPadIndex: 30,
                cadPadId: 300,
                v22Node: new NotchV22Node(30, 110, 31, 15, nullDiff, 0, 0),
                comment: "ic2-v22"),
        });

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(3),
            CreateGridWithIcCount(3),
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Contains("#define USER_NHC_IC_NUM          (3u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (1u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC2 (0u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC3 (1u)", text, StringComparison.Ordinal);
        Assert.Contains("static const ST_PRI_NHC_TABLE_NODE_INFO castNHC_TABLE_IC1[USER_NHC_NODE_NUM_IC1]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("static const ST_PRI_NHC_TABLE_NODE_INFO castNHC_TABLE_IC2", text, StringComparison.Ordinal);
        Assert.Contains("static const ST_PRI_NHC_TABLE_NODE_INFO castNHC_TABLE_IC3[USER_NHC_NODE_NUM_IC3]", text, StringComparison.Ordinal);
        Assert.Contains("{ ((const ST_PRI_NHC_TABLE_NODE_INFO*)0), USER_NHC_NODE_NUM_IC2 }, // IC2", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmitsFwBaseMask_ForActiveRegularSimulation()
    {
        var grid = CreateRuntimeParityGrid();
        var table = CreateRuntimeParityV22Table();
        var activeRegularPadIds = new HashSet<int> { 0, 2, 3 };

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(2),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug,
            activeRegularPadIds);

        Assert.Contains("// FW simulation base mask", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_FW_MASK_NUM_IC1  (2u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_FW_MASK_SPAN_IC1 (3u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_FW_MASK_NUM_IC2  (1u)", text, StringComparison.Ordinal);
        Assert.Contains("static const UINT16 cau16NHC_FW_BASE_MASK_IC1[USER_NHC_FW_MASK_NUM_IC1]", text, StringComparison.Ordinal);
        Assert.Contains("    0, 2", text, StringComparison.Ordinal);
        Assert.Contains("static const UINT16 cau16NHC_FW_BASE_MASK_IC2[USER_NHC_FW_MASK_NUM_IC2]", text, StringComparison.Ordinal);
        Assert.Contains("void FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)", text, StringComparison.Ordinal);
        Assert.Contains("S_2D_DIFFAFTER->as16Buf[u16j] = 0;", text, StringComparison.Ordinal);
        Assert.Contains("S_2D_DIFFAFTER->as16Buf[pstMaskInfo->pu16ActiveDiffs[u16j]] = s16BaseValue;", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_OmitsFwBaseMask_ForReleaseProfile()
    {
        var grid = CreateRuntimeParityGrid();
        var table = CreateRuntimeParityV22Table();

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(2),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Release,
            new HashSet<int> { 0, 2, 3 });

        Assert.DoesNotContain("// FW simulation base mask", text, StringComparison.Ordinal);
        Assert.DoesNotContain("USER_NHC_FW_MASK_NUM", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ST_PRI_NHC_FW_BASE_MASK_INFO", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cau16NHC_FW_BASE_MASK", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FUNC_NHC_SimulationLoadFwBaseMask", text, StringComparison.Ordinal);
        Assert.Contains("void FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmitsFwStyleV22File_AndPreservesKnownFlagBits()
    {
        var settings = CreateProjectSettings();
        settings.Notch.CompensationModel = NotchCompensationModel.ConservativeNoGain;
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 10,
                cadPadId: 100,
                v22Node: new NotchV22Node(10, 120, 20, 30, 30, -30, 3),
                comment: "flagged"),
        });

        var text = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSet(),
            CreateGrid(),
            settings,
            NotchExportProfile.Debug);

        Assert.DoesNotContain("#include <stdint.h>", text, StringComparison.Ordinal);
        Assert.Contains("#include \"notch.h\"", text, StringComparison.Ordinal);
        Assert.Contains("// Requires FW platform typedefs from notch.h / ap_gvariable.h: UINT8, UINT16, INT8, INT16.", text, StringComparison.Ordinal);
        Assert.Contains("UINT16 NHC_IDX;", text, StringComparison.Ordinal);
        Assert.Contains("INT8   NHC_1ST_RATIO;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ST_PRI_NHC_TABLE_META_INFO", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PriNhc_GetMeta", text, StringComparison.Ordinal);
        Assert.Contains("// Generation metadata", text, StringComparison.Ordinal);
        Assert.Contains("//   CompensationModel: ConservativeNoGain", text, StringComparison.Ordinal);
        Assert.Contains("//   SourceCombineRule: C = sum(target regular coverage); ToFull excludes source gain", text, StringComparison.Ordinal);
        Assert.Contains("//   TargetAllocationRule: Target-regular coverage; legs use overlap/regular-area coverage, ToFull is support/cap/allowance.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ST_PRI_NHC_TABLE_EXPORT", text, StringComparison.Ordinal);
        Assert.Contains("static const ST_PRI_NHC_IC_TABLE_INFO castNHC_TABLE_BY_IC[USER_NHC_IC_NUM]", text, StringComparison.Ordinal);
        Assert.Contains("void FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)", text, StringComparison.Ordinal);
        Assert.Contains("void FUNC_NHC_DiffCompensation(void)", text, StringComparison.Ordinal);
        Assert.Contains("NHC_FLAG_CONTINUATION | NHC_FLAG_ANCHOR_OVERRIDE", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_EmitsVersionSpecificFwFiles()
    {
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
                values: V21OnlyPayload,
                comment: "v21-only"),
        });

        var v22OnlyText = NotchTableExporter.ExportAsCInitializer(
            v22OnlyTable,
            CreateCadSet(),
            CreateGrid(),
            CreateProjectSettings(),
            NotchExportProfile.Debug);
        var v21OnlyText = NotchTableExporter.ExportAsCInitializer(
            v21OnlyTable,
            CreateCadSet(),
            CreateGrid(),
            CreateProjectSettings(),
            NotchExportProfile.Debug);

        Assert.Contains("Notch v2.2 compensation export", v22OnlyText, StringComparison.Ordinal);
        Assert.Contains("UINT8  NHC_COMBINE;", v22OnlyText, StringComparison.Ordinal);
        Assert.DoesNotContain("UINT8  NHC_REGU_TO_FULL;", v22OnlyText, StringComparison.Ordinal);

        Assert.Contains("Notch v2.1 compensation export", v21OnlyText, StringComparison.Ordinal);
        Assert.Contains("UINT8  NHC_REGU_TO_FULL;", v21OnlyText, StringComparison.Ordinal);
        Assert.DoesNotContain("UINT8  NHC_COMBINE;", v21OnlyText, StringComparison.Ordinal);
    }

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
                values: V21OnlyPayload,
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
        Assert.Equal(ExpectedV21Q7BoundaryRuntimeValues, firmwareValues);
        Assert.Equal(ExpectedV21Q7BoundaryRuntimeValues, simulatedValues);
        Assert.Equal(ExpectedV21Q7BoundarySignedPercents, simulation.Actions.SelectMany(static action => action.Legs).Select(static leg => leg.RatioPercent));
        Assert.Equal(ExpectedV21Q7BoundaryLegDeltas, simulation.Actions.SelectMany(static action => action.Legs).Select(static leg => leg.DeltaValue));
        Assert.Equal(ExpectedV21Q7BoundaryRuntimeValues.Where((_, index) => index % 2 == 0), simulation.Actions.Select(static action => action.SourceAfterValue));
    }

    [Fact]
    public void ExportAsCInitializer_V21ThreeTerms_SplitsNodesAndPreservesInt16Wrap()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(V21MultiTermWrapDiffs);
        var table = new NotchTable(V21MultiTermWrapSourceDiffs
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

    [Fact]
    public void ExportAsCInitializer_GccRuntimeLoadsFwBaseMask_WhenGccIsAvailable()
    {
        if (!IsGccAvailable())
        {
            return;
        }

        var grid = CreateRuntimeParityGrid();
        var table = CreateRuntimeParityV22Table();
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table,
            CreateCadSetWithIcCount(2),
            grid,
            CreateProjectSettings(),
            NotchExportProfile.Debug,
            new HashSet<int> { 0, 2, 3 });

        var actual = RunCProgramWithGcc(CreateFwBaseMaskRuntimeHarnessSource(generatedSource));

        Assert.Equal(ExpectedFwBaseMaskRuntimeValues, actual);
    }

    private static CadPadSet CreateCadSet()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var pads = new[] { new CadPad(1, "L", "P", polygon) };
        return new CadPadSet(pads);
    }

    private static CadPadSet CreateCadSetWithIcCount(int icCount)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var pads = Enumerable.Range(0, Math.Max(1, icCount))
            .Select(index =>
            {
                return new CadPad(index + 1, $"L{index}", $"P{index}", polygon);
            })
            .ToArray();
        return new CadPadSet(pads);
    }

    private static RegularGrid CreateGrid()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var regular = new RegularPad(0, 0, 0, polygon)
        {
            DiffIndex = 0,
            IcIndex = 0
        };
        var regularPads = new[] { regular };
        return new RegularGrid(1, 1, UnitEdges, UnitEdges, regularPads);
    }

    private static RegularGrid CreateGridWithIcCount(int icCount)
    {
        var cols = Math.Max(1, icCount);
        var xEdges = Enumerable.Range(0, cols + 1).Select(static index => (double)(index * 10)).ToArray();
        var yEdges = new[] { 0d, 10d };
        var pads = new List<RegularPad>(cols);
        for (var col = 0; col < cols; col++)
        {
            var polygon = new Polygon2(new[]
            {
                new Point2(col * 10, 0),
                new Point2((col + 1) * 10, 0),
                new Point2((col + 1) * 10, 10),
                new Point2(col * 10, 10),
            });
            var pad = new RegularPad(0, col, col, polygon)
            {
                DiffIndex = col,
                IcIndex = col,
            };
            pads.Add(pad);
        }

        return new RegularGrid(1, cols, xEdges, yEdges, pads);
    }

    private static ProjectSettings CreateProjectSettings(
        NotchComputationMode computationMode = NotchComputationMode.CadAllocation)
    {
        return new ProjectSettings
        {
            Notch = new NotchSettings
            {
                NullValue = 65535,
                ComputationMode = computationMode,
            }
        };
    }

    private static RegularGrid CreateRuntimeParityGrid()
    {
        const int rows = 2;
        const int cols = 3;
        var xEdges = Enumerable.Range(0, cols + 1).Select(static index => (double)index).ToArray();
        var yEdges = Enumerable.Range(0, rows + 1).Select(static index => (double)index).ToArray();
        var pads = new List<RegularPad>(rows * cols);
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var regularPadId = (row * cols) + col;
                var polygon = new Polygon2(new[]
                {
                    new Point2(col, row),
                    new Point2(col + 1, row),
                    new Point2(col + 1, row + 1),
                    new Point2(col, row + 1),
                });
                pads.Add(new RegularPad(row, col, regularPadId, polygon)
                {
                    IcIndex = row,
                    DiffIndex = col,
                });
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    private static RegularGrid CreateV21Q7BoundaryGrid()
    {
        var cols = V21Q7BoundaryMagnitudes.Length * V21Q7BoundaryTypes.Length * 2;
        var xEdges = Enumerable.Range(0, cols + 1).Select(static index => (double)index).ToArray();
        var yEdges = new[] { 0d, 1d };
        var pads = Enumerable.Range(0, cols)
            .Select(col => new RegularPad(
                row: 0,
                col,
                index: col,
                new Polygon2(new[]
                {
                    new Point2(col, 0),
                    new Point2(col + 1, 0),
                    new Point2(col + 1, 1),
                    new Point2(col, 1),
                }))
            {
                IcIndex = 0,
                DiffIndex = col,
            })
            .ToArray();
        return new RegularGrid(1, cols, xEdges, yEdges, pads);
    }

    private static NotchTable CreateV21Q7BoundaryTable()
    {
        var rows = new List<NotchTableRow>();
        var pairIndex = 0;
        foreach (var magnitudeQ7 in V21Q7BoundaryMagnitudes)
        {
            foreach (var type in V21Q7BoundaryTypes)
            {
                var sourceDiffIndex = pairIndex * 2;
                var targetDiffIndex = sourceDiffIndex + 1;
                rows.Add(new NotchTableRow(
                    NotchAlgorithmVersion.V21,
                    icIndex: 0,
                    diffIndex: sourceDiffIndex,
                    regularPadIndex: sourceDiffIndex,
                    cadPadId: 1000 + pairIndex,
                    values: [sourceDiffIndex, 100, 100, targetDiffIndex, type, magnitudeQ7, 65535, 0, 0],
                    comment: $"Q7={magnitudeQ7} type={type}"));
                pairIndex++;
            }
        }

        return new NotchTable(rows);
    }

    private static DiffFrameGridProjectionResult CreateV21Q7BoundaryProjection(RegularGrid grid)
    {
        var cells = grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select(static pad => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: pad.DiffIndex % 2 == 0 ? 128d : 0d))
            .ToArray();
        return new DiffFrameGridProjectionResult(true, null, cells);
    }

    private static NotchTable CreateRuntimeParityV22Table()
    {
        return new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 0,
                regularPadIndex: 0,
                cadPadId: 100,
                v22Node: new NotchV22Node(0, 94, 1, 44, 65535, 0, 0),
                comment: "ic0 move 44"),
            new(
                icIndex: 0,
                diffIndex: 2,
                regularPadIndex: 2,
                cadPadId: 102,
                v22Node: new NotchV22Node(2, 120, 1, 60, 65535, 0, 0),
                comment: "ic0 amplify to diff1"),
            new(
                icIndex: 1,
                diffIndex: 0,
                regularPadIndex: 3,
                cadPadId: 200,
                v22Node: new NotchV22Node(0, 90, 2, -20, 65535, 0, 0),
                comment: "ic1 negative leg"),
        });
    }

    private static NotchTable CreateRuntimeParityV21Table()
    {
        return new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 0,
                regularPadIndex: 0,
                cadPadId: 100,
                values: [0, 100, 94, 1, 1, 56, 65535, 0, 0],
                comment: "ic0 move 44"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 2,
                regularPadIndex: 2,
                cadPadId: 102,
                values: [2, 100, 120, 1, 1, 77, 65535, 0, 0],
                comment: "ic0 amplify to diff1"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 1,
                diffIndex: 0,
                regularPadIndex: 3,
                cadPadId: 200,
                values: [0, 100, 90, 2, 2, 26, 65535, 0, 0],
                comment: "ic1 negative leg"),
        });
    }

    private static NotchTable CreateRuntimeParityLegacyV21Table()
    {
        return new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 0,
                regularPadIndex: 0,
                cadPadId: 100,
                values: [0, 100, 50, 1, 1, 64, 65535, 0, 0],
                comment: "ic0 destination scale and add"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 2,
                regularPadIndex: 2,
                cadPadId: 102,
                values: [2, 100, 100, 0, 2, 32, 65535, 0, 0],
                comment: "ic0 destination subtract"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 1,
                diffIndex: 1,
                regularPadIndex: 4,
                cadPadId: 201,
                values: [1, 100, 120, 0, 1, -128, 2, 2, 300],
                comment: "ic1 malformed magnitudes normalize at UINT8 boundary"),
        });
    }

    private static double[] SimulateWithCSharp(
        RegularGrid grid,
        NotchTable table,
        NotchAlgorithmVersion version,
        NotchComputationMode computationMode = NotchComputationMode.CadAllocation,
        IReadOnlyList<double>? beforeValues = null,
        int nullDiffValue = 65535)
    {
        var projection = CreateRuntimeParityProjection(grid, beforeValues ?? RuntimeParityBeforeValues);
        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            version,
            nullDiffValue,
            ComputationMode: computationMode));

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        return result.Cells
            .OrderBy(static cell => cell.RegularRow)
            .ThenBy(static cell => cell.RegularCol)
            .Select(static cell => cell.AfterValue)
            .ToArray();
    }

    private static DiffFrameGridProjectionResult CreateRuntimeParityProjection(
        RegularGrid grid,
        IReadOnlyList<double> beforeValues)
    {
        Assert.Equal(grid.Pads.Count, beforeValues.Count);
        var cells = grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select((pad, index) => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: beforeValues[index]))
            .ToList();
        return new DiffFrameGridProjectionResult(true, null, cells);
    }

    private static string ExtractSingleTableRowLine(string text, string rowPrefix)
    {
        var line = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static l => l.TrimEnd('\r'))
            .Single(l => l.Contains(rowPrefix, StringComparison.Ordinal));
        return line;
    }

    private static bool IsGccAvailable()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gcc",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("--version");

        try
        {
            if (!process.Start())
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static double[] RunCProgramWithGcc(string source)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "FreeformHelper.NotchExporterRuntimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, "notch_runtime.c");
        var executablePath = Path.Combine(tempDirectory, "notch_runtime.exe");
        File.WriteAllText(sourcePath, source);
        WriteNotchHeaderStub(tempDirectory);

        try
        {
            RunProcess(
                "gcc",
                [
                    "-std=c11",
                    "-Wall",
                    "-Wextra",
                    "-Werror",
                    sourcePath,
                    "-o",
                    executablePath,
                ],
                "gcc failed to build generated notch runtime harness.");
            var output = RunProcess(executablePath, [], "generated notch runtime harness failed.");
            return output
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(static value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Best-effort temp cleanup only.
            }
        }
    }

    private static string RunProcess(string fileName, IEnumerable<string> arguments, string failureMessage)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), $"{fileName} did not start.");
        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(15000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
        }

        Assert.True(
            exited && process.ExitCode == 0,
            $"{failureMessage}{Environment.NewLine}stdout:{Environment.NewLine}{stdOut}{Environment.NewLine}stderr:{Environment.NewLine}{stdErr}");
        return stdOut;
    }

    private static void AssertCCompilesWithGcc(string source)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "FreeformHelper.NotchExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, "notch_export.c");
        var objectPath = Path.Combine(tempDirectory, "notch_export.o");
        File.WriteAllText(sourcePath, CreateGccHarnessSource(source));
        WriteNotchHeaderStub(tempDirectory);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gcc",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-std=c11");
        process.StartInfo.ArgumentList.Add("-Wall");
        process.StartInfo.ArgumentList.Add("-Wextra");
        process.StartInfo.ArgumentList.Add("-Werror");
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(sourcePath);
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(objectPath);

        try
        {
            Assert.True(process.Start(), "gcc did not start.");
            var stdOut = process.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            Assert.True(
                process.ExitCode == 0,
                $"gcc failed to compile generated notch export.{Environment.NewLine}stdout:{Environment.NewLine}{stdOut}{Environment.NewLine}stderr:{Environment.NewLine}{stdErr}");
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Best-effort temp cleanup only.
            }
        }
    }

    private static void WriteNotchHeaderStub(string tempDirectory)
    {
        var headerPath = Path.Combine(tempDirectory, "notch.h");
        File.WriteAllText(
            headerPath,
            """
#ifndef NOTCH_H_
#define NOTCH_H_
#include <stdint.h>
#define FUNC_ENABLE 1
#define USER_SWITCH_NOTCH_COMPENSATION FUNC_ENABLE
typedef uint8_t UINT8;
typedef uint16_t UINT16;
typedef int8_t INT8;
typedef int16_t INT16;
typedef struct
{
    INT16 as16Buf[65536];
} NHC_DIFF_BUFFER;
extern NHC_DIFF_BUFFER g_stNhcDiffAfter;
#define S_2D_DIFFAFTER (&g_stNhcDiffAfter)
#endif
""");
    }

    private static string CreateGccHarnessSource(string generatedSource) =>
        string.Concat(
            generatedSource,
            "\nNHC_DIFF_BUFFER g_stNhcDiffAfter;\n");

    private static string CreateV22RuntimeParityHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            CreateRuntimeMainSource());

    private static string CreateSingleIcRuntimeParityHarnessSource(
        string generatedSource,
        int[] diffIndices,
        int[] beforeValues)
    {
        Assert.Equal(diffIndices.Length, beforeValues.Length);
        var loadLines = string.Join(
            Environment.NewLine,
            diffIndices.Select((diffIndex, index) =>
                $"    g_stNhcDiffAfter.as16Buf[{diffIndex}] = {beforeValues[index]};"));
        var printLines = string.Join(
            Environment.NewLine,
            diffIndices.Select(static diffIndex =>
                $"    printf(\"%d\\n\", g_stNhcDiffAfter.as16Buf[{diffIndex}]);"));
        return string.Concat(
            CreateGccHarnessSource(generatedSource),
            "\n#include <stdio.h>\n\nint main(void)\n{\n",
            loadLines,
            "\n\n    FUNC_NHC_DiffCompensationByIc(0u);\n",
            printLines,
            "\n\n    return 0;\n}\n");
    }

    private static string CreateV21RuntimeParityHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            CreateRuntimeMainSource());

    private static string CreateV21Q7BoundaryRuntimeHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            """

#include <stdio.h>

int main(void)
{
    for (int index = 0; index < 24; ++index)
    {
        g_stNhcDiffAfter.as16Buf[index] = ((index % 2) == 0) ? 128 : 0;
    }

    FUNC_NHC_DiffCompensationByIc(0u);
    for (int index = 0; index < 24; ++index)
    {
        printf("%d\n", g_stNhcDiffAfter.as16Buf[index]);
    }

    return 0;
}
""");

    private static string CreateV21MultiTermWrapHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            """

#include <stdio.h>

int main(void)
{
    g_stNhcDiffAfter.as16Buf[10] = 32767;
    g_stNhcDiffAfter.as16Buf[11] = 32767;
    g_stNhcDiffAfter.as16Buf[12] = 32767;
    g_stNhcDiffAfter.as16Buf[20] = 0;

    FUNC_NHC_DiffCompensationByIc(0u);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[10]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[11]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[12]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[20]);
    return 0;
}
""");

    private static string CreateFwBaseMaskRuntimeHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            """

#include <stdio.h>

int main(void)
{
    for (int index = 0; index < 6; ++index)
    {
        g_stNhcDiffAfter.as16Buf[index] = 9;
    }

    FUNC_NHC_SimulationLoadFwBaseMaskByIc(0u, 400);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[0]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[1]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[2]);

    for (int index = 0; index < 6; ++index)
    {
        g_stNhcDiffAfter.as16Buf[index] = 9;
    }

    FUNC_NHC_SimulationLoadFwBaseMaskByIc(1u, 123);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[0]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[1]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[2]);

    return 0;
}
""");

    private static string CreateRuntimeMainSource() =>
        """

#include <stdio.h>

static void LoadIc0(void)
{
    g_stNhcDiffAfter.as16Buf[0] = 100;
    g_stNhcDiffAfter.as16Buf[1] = 50;
    g_stNhcDiffAfter.as16Buf[2] = 25;
}

static void LoadIc1(void)
{
    g_stNhcDiffAfter.as16Buf[0] = 80;
    g_stNhcDiffAfter.as16Buf[1] = 40;
    g_stNhcDiffAfter.as16Buf[2] = 30;
}

int main(void)
{
    INT16 values[6];
    LoadIc0();
    FUNC_NHC_DiffCompensationByIc(0u);
    values[0] = g_stNhcDiffAfter.as16Buf[0];
    values[1] = g_stNhcDiffAfter.as16Buf[1];
    values[2] = g_stNhcDiffAfter.as16Buf[2];

    LoadIc1();
    FUNC_NHC_DiffCompensationByIc(1u);
    values[3] = g_stNhcDiffAfter.as16Buf[0];
    values[4] = g_stNhcDiffAfter.as16Buf[1];
    values[5] = g_stNhcDiffAfter.as16Buf[2];

    for (int index = 0; index < 6; ++index)
    {
        printf("%d\n", values[index]);
    }

    return 0;
}
""";

    private sealed class MutatingReadOnlySet(IEnumerable<int> values, Action mutation)
        : SortedSet<int>(values), IReadOnlySet<int>, IEnumerable<int>
    {
        private Action? _mutation = mutation;

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            System.Threading.Interlocked.Exchange(ref _mutation, null)?.Invoke();
            return base.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            ((IEnumerable<int>)this).GetEnumerator();
    }
}
