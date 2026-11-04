using FreeformHelper.Application.Export;
using FreeformHelper.Domain.Notch;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchTableExporterCsvOrderTests
{
    private const int RowNumberColumn = 0;
    private const int VersionColumn = 1;
    private const int IcIndexColumn = 3;
    private const int IcNumberColumn = 4;
    private const int DiffColumn = 5;
    private const int RegularPadColumn = 6;
    private const int CadPadColumn = 7;

    private static readonly int[] _payload = [1, 2, 3];

    private static string[][] ExportDataRows(params NotchTableRow[] rows)
    {
        var csv = NotchTableExporter.ExportAsCsv(new NotchTable(rows));
        return csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(static line => line.TrimEnd('\r').Split(','))
            .ToArray();
    }

    private static NotchTableRow Row(
        NotchAlgorithmVersion version,
        int icIndex,
        int diffIndex,
        int regularPadIndex,
        int? cadPadId)
    {
        return new NotchTableRow(version, icIndex, diffIndex, regularPadIndex, cadPadId, _payload, "row");
    }

    private static string[] Column(string[][] dataRows, int column)
    {
        return dataRows.Select(row => row[column]).ToArray();
    }

    [Fact]
    public void ExportAsCsv_SortsByIcIndexBeforeTheFirmwareDiffIndex()
    {
        var dataRows = ExportDataRows(
            Row(NotchAlgorithmVersion.V22, icIndex: 2, diffIndex: 1, regularPadIndex: 1, cadPadId: 101),
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 9, regularPadIndex: 9, cadPadId: 109),
            Row(NotchAlgorithmVersion.V22, icIndex: 1, diffIndex: 5, regularPadIndex: 5, cadPadId: 105));

        Assert.Equal(["0", "1", "2"], Column(dataRows, IcIndexColumn));
        Assert.Equal(["9", "5", "1"], Column(dataRows, DiffColumn));
    }

    [Fact]
    public void ExportAsCsv_WritesIcNumberAsIcIndexPlusOneAndRenumbersTheRows()
    {
        var dataRows = ExportDataRows(
            Row(NotchAlgorithmVersion.V22, icIndex: 3, diffIndex: 1, regularPadIndex: 1, cadPadId: 101),
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 2, regularPadIndex: 2, cadPadId: 102));

        Assert.Equal(["1", "2"], Column(dataRows, RowNumberColumn));
        Assert.Equal(["0", "3"], Column(dataRows, IcIndexColumn));
        Assert.Equal(["1", "4"], Column(dataRows, IcNumberColumn));
    }

    [Fact]
    public void ExportAsCsv_WithTheSameIcIndex_SortsByDiffIndex()
    {
        var dataRows = ExportDataRows(
            Row(NotchAlgorithmVersion.V22, icIndex: 1, diffIndex: 20, regularPadIndex: 1, cadPadId: 101),
            Row(NotchAlgorithmVersion.V22, icIndex: 1, diffIndex: 3, regularPadIndex: 2, cadPadId: 102));

        Assert.Equal(["3", "20"], Column(dataRows, DiffColumn));
    }

    [Fact]
    public void ExportAsCsv_WithTheSameIcAndDiffIndex_PutsVersion21BeforeVersion22()
    {
        var dataRows = ExportDataRows(
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 4, regularPadIndex: 1, cadPadId: 101),
            Row(NotchAlgorithmVersion.V21, icIndex: 0, diffIndex: 4, regularPadIndex: 2, cadPadId: 102));

        Assert.Equal(["2.1", "2.2"], Column(dataRows, VersionColumn));
    }

    [Fact]
    public void ExportAsCsv_WithTheSameIcDiffAndVersion_SortsByRegularPadIndex()
    {
        var dataRows = ExportDataRows(
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 4, regularPadIndex: 7, cadPadId: 101),
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 4, regularPadIndex: 3, cadPadId: 102));

        Assert.Equal(["3", "7"], Column(dataRows, RegularPadColumn));
    }

    [Fact]
    public void ExportAsCsv_WithTheSameKeyAndRegularPad_SortsByCadPadIdAndPutsAMissingIdLast()
    {
        var dataRows = ExportDataRows(
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 4, regularPadIndex: 3, cadPadId: null),
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 4, regularPadIndex: 3, cadPadId: 900),
            Row(NotchAlgorithmVersion.V22, icIndex: 0, diffIndex: 4, regularPadIndex: 3, cadPadId: 12));

        Assert.Equal(["12", "900", string.Empty], Column(dataRows, CadPadColumn));
    }

    [Fact]
    public void ExportAsCsv_DoublesQuotesInTheComment()
    {
        var row = new NotchTableRow(NotchAlgorithmVersion.V22, 0, 1, 1, 101, _payload, "pad \"A\" near edge");

        var csv = NotchTableExporter.ExportAsCsv(new NotchTable([row]));

        Assert.EndsWith(",\"pad \"\"A\"\" near edge\"", csv.TrimEnd('\r', '\n'), StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCsv_WithoutRows_WritesOnlyTheHeaderLine()
    {
        var csv = NotchTableExporter.ExportAsCsv(new NotchTable([]));

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.StartsWith("row_number,version,version_code,ic_index,ic_number,", lines[0], StringComparison.Ordinal);
    }
}
