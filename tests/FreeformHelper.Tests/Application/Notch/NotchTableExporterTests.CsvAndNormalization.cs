using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableExporterTests
{
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
        Assert.Equal(_expectedV21PayloadPrefix, v21Columns[9..13]);
        Assert.All(v21Columns[13..18], static value => Assert.Equal(string.Empty, value));
        Assert.Equal("\"v21\"", v21Columns[18]);
        Assert.Equal("2", v22Columns[8]);
        Assert.Equal(_expectedV22PayloadPrefix, v22Columns[9..11]);
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
    public void ExportAsCInitializer_DoesNotEnumerateTheIgnoredActiveRegularSet(
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

        // The exporter ignores the set, so the mutation hook never ran and the null value is still the frozen one.
        Assert.Equal(frozenNullDiff, settings.Notch.NullValue);
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

    [Fact, UseEnUsCulture]
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
            .Select(static line => line[(line.IndexOf("// ", StringComparison.Ordinal) + 3)..line.IndexOf(';', StringComparison.Ordinal)])
            .ToArray();

        Assert.DoesNotContain("drop-no-op", text, StringComparison.Ordinal);
        Assert.Equal(Enumerable.Range(1, 16).Select(static index => $"keep-{index:D2}"), emittedComments);
    }
}
