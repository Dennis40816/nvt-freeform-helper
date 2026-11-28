// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportCadPadIdentityTests
{
    private const int NullDiff = ushort.MaxValue;

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, false, 0)]
    [InlineData(NotchAlgorithmVersion.V21, false, 1)]
    [InlineData(NotchAlgorithmVersion.V21, false, 2)]
    [InlineData(NotchAlgorithmVersion.V22, false, 0)]
    [InlineData(NotchAlgorithmVersion.V22, false, 1)]
    [InlineData(NotchAlgorithmVersion.V22, false, 2)]
    [InlineData(NotchAlgorithmVersion.V22, true, 0)]
    [InlineData(NotchAlgorithmVersion.V22, true, 1)]
    [InlineData(NotchAlgorithmVersion.V22, true, 2)]
    public void ExportNamesCadHoldingBadSourceOrTargetDiff(NotchAlgorithmVersion version, bool legacy, int slot)
    {
        var reportingRow = Row(version, legacy, 42, slot == 0 ? 37 : 0,
            slot == 1 ? 37 : NullDiff, slot == 2 ? 37 : NullDiff);
        var ownerRow = Row(version, legacy, 73, 37);
        var rows = slot == 0 ? new[] { reportingRow } : [reportingRow, ownerRow];
        var expectedCad = slot == 0 ? "CAD 42" : "CAD 73";

        var error = Assert.Throws<InvalidOperationException>(() => Export(rows));

        Assert.Contains("IC index 0: 37.", error.Message, StringComparison.Ordinal);
        Assert.Contains(expectedCad, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(slot == 0 ? "CAD 73" : "CAD 42", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "Anchor")]
    [InlineData(1, "First target")]
    [InlineData(2, "Second target")]
    public void LegacyV21NamesCadHoldingBadDiff(int slot, string field)
    {
        var row = Row(NotchAlgorithmVersion.V21, false, 42, slot == 0 ? 37 : 0,
            slot == 1 ? 37 : NullDiff, slot == 2 ? 37 : NullDiff);
        var owner = Row(NotchAlgorithmVersion.V21, false, 73, 37);

        var error = Assert.Throws<InvalidOperationException>(() => Export(
            slot == 0 ? [row] : [row, owner], NotchComputationMode.LegacyRegularAnchor));

        Assert.Contains(field + " diff index", error.Message, StringComparison.Ordinal);
        Assert.Contains(slot == 0 ? "CAD 42" : "CAD 73", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportListsDistinctOwnersOfReportedValueInTheSameIc()
    {
        var rows = new[]
        {
            Row(NotchAlgorithmVersion.V22, false, 42, 0, 37),
            Row(NotchAlgorithmVersion.V22, false, 73, 37),
            Row(NotchAlgorithmVersion.V22, false, 74, 37),
            Row(NotchAlgorithmVersion.V22, false, 73, 37),
            Row(NotchAlgorithmVersion.V22, false, 99, 37, ic: 1),
            Row(NotchAlgorithmVersion.V22, false, 75, 38),
        };

        var error = Assert.Throws<InvalidOperationException>(() => Export(rows));

        Assert.EndsWith("CAD 73, CAD 74.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportBoundsOwnerListAndCountsRemainingPads()
    {
        var rows = Enumerable.Range(70, 5).Select(id => Row(NotchAlgorithmVersion.V22, false, id, 37))
            .Prepend(Row(NotchAlgorithmVersion.V22, false, 42, 0, 37)).ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => Export(rows));

        Assert.EndsWith("CAD 70, CAD 71, CAD 72 (+2 more).", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportDoesNotGuessWhenNoSourceCadHoldsTheValue()
    {
        var row = Row(NotchAlgorithmVersion.V22, false, 42, 0, 37);
        var otherIc = Row(NotchAlgorithmVersion.V22, false, 73, 37, ic: 1);

        var error = Assert.Throws<InvalidOperationException>(() => Export([row, otherIc]));

        Assert.EndsWith("No CAD pad holds this output FW diff.", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("CAD 42", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("CAD 73", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportDoesNotGuessForUnlinkedLegacyShortRow()
    {
        var row = new NotchTableRow(NotchAlgorithmVersion.V22, 0, 37, 0, null, [0, 80, 100], "short legacy");

        var error = Assert.Throws<InvalidOperationException>(() => Export([row]));

        Assert.Contains("IC index 0: 37.", error.Message, StringComparison.Ordinal);
        Assert.EndsWith("No CAD pad holds this output FW diff.", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void RegenerationAfterClearingNamedCadOutputOverrideAllowsExport(NotchAlgorithmVersion version)
    {
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(73, "PAD", 0, 0, 3, 0.5)]);
        var grid = TestGeometryFactory.CreateRegularGrid(1, 3, diffIndexSelector: (_, col) => col);
        var settings = new ProjectSettings();
        settings.Notch.EnabledVersions = [version];
        settings.Notch.EnableToFull = false;
        _ = PadMatcher.Match(cad, grid);
        Array.ForEach(grid.Pads.ToArray(), pad => pad.Freeform = FreeformType.XWay);
        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [73] = 37 });

        var error = Assert.Throws<InvalidOperationException>(() =>
            NotchTableExporter.ExportAsCInitializer(table, cad, grid, settings));
        var clearedTable = generator.Generate(cad, grid, settings,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [73] = 0 });
        var text = NotchTableExporter.ExportAsCInitializer(clearedTable, cad, grid, settings);

        Assert.Contains("CAD 73", error.Message, StringComparison.Ordinal);
        Assert.Contains("IC index 0: 37.", error.Message, StringComparison.Ordinal);
        Assert.Contains("castNHC_TABLE_IC1", text, StringComparison.Ordinal);
    }

    private static NotchTableRow Row(NotchAlgorithmVersion version, bool legacy, int cad, int source,
        int first = NullDiff, int second = NullDiff, int ic = 0)
    {
        // Table identity deliberately differs from the actual firmware source; owners may be Release no-ops.
        int[] values = version == NotchAlgorithmVersion.V21
            ? [source, 100, 100, first, 1, 32, second, 1, 32]
            : [source, 100, first, first == NullDiff ? 0 : 25, second, second == NullDiff ? 0 : 25, 0];
        return new NotchTableRow(version, ic, 1, 0, cad,
            legacy ? [.. values, 0, 0] : values, "synthetic identity");
    }

    private static string Export(IReadOnlyList<NotchTableRow> rows,
        NotchComputationMode mode = NotchComputationMode.CadAllocation)
    {
        var settings = new ProjectSettings { Notch = new NotchSettings { ComputationMode = mode } };
        return NotchTableExporter.ExportAsCInitializer(new NotchTable(rows), new CadPadSet([]),
            TestGeometryFactory.CreateRegularGrid(4, 4), settings);
    }
}
