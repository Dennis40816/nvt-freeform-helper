// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportRangeCheckTests
{
    private const int NullDiff = 65534;
    private static readonly int[] _gridDiffIndices = [0];

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 0, -1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 0, 65536)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 1, -1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 1, 65536)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 2, -1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 2, 65536)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 0, -1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 0, 65536)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 1, -1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 1, 65536)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 2, -1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 2, 65536)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 0, -1)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 0, 65536)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 1, -1)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 1, 65536)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 2, -1)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 2, 65536)]
    public void ExportRejectsDiffIndexOutsideUint16(
        NotchAlgorithmVersion version, NotchComputationMode mode, int slot, int invalidDiff)
    {
        var row = CreateDiffRow(version, slot, invalidDiff);

        var error = Assert.Throws<InvalidOperationException>(() => ExportRow(row, mode));

        Assert.Contains("[0,65535]", error.Message, StringComparison.Ordinal);
        Assert.Contains("IC index 0", error.Message, StringComparison.Ordinal);
        Assert.Contains("diff index 17", error.Message, StringComparison.Ordinal);
        Assert.Contains("CAD pad id 42", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 0)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor, 2)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 0)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 1)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation, 2)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 0)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 1)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation, 2)]
    public void ExportAcceptsDiffIndexAtUint16Limit(
        NotchAlgorithmVersion version, NotchComputationMode mode, int slot)
    {
        var row = CreateDiffRow(version, slot, ushort.MaxValue);

        var text = ExportRow(row, mode);

        Assert.Contains("65535,", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAcceptsNullTargets()
    {
        var row = CreateRow(NotchAlgorithmVersion.V22, firstTarget: NullDiff, secondTarget: NullDiff);

        var text = ExportRow(row, NotchComputationMode.CadAllocation);

        Assert.Contains("NHC_DIFF_NONE,   25, NHC_DIFF_NONE,   25", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchComputationMode.CadAllocation)]
    public void ExportRejectsV21WithEightValues(NotchComputationMode mode)
    {
        var validRow = CreateRow(NotchAlgorithmVersion.V21);
        var row = new NotchTableRow(NotchAlgorithmVersion.V21, 0, 17, 0, 42, validRow.Values[..8], "short");

        var error = Assert.Throws<InvalidOperationException>(() => ExportRow(row, mode));

        Assert.Contains("at least 9 values", error.Message, StringComparison.Ordinal);
        Assert.Contains("IC index 0, diff index 17, CAD pad id 42", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchComputationMode.CadAllocation)]
    public void ExportAcceptsV21WithNineValues(NotchComputationMode mode)
    {
        var row = CreateRow(NotchAlgorithmVersion.V21);

        var text = ExportRow(row, mode);

        Assert.Contains("castNHC_TABLE_IC1", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation)]
    public void ExportRejects256IcsFromRows(NotchAlgorithmVersion version, NotchComputationMode mode)
    {
        var row = CreateRow(version, icIndex: 255);

        var error = Assert.Throws<InvalidOperationException>(() => ExportRow(row, mode));

        Assert.Contains("at most 255", error.Message, StringComparison.Ordinal);
        Assert.Contains("IC index 255, diff index 17, CAD pad id 42", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation)]
    public void ExportRejects256IcsFromGrid(NotchAlgorithmVersion version, NotchComputationMode mode)
    {
        var row = CreateRow(version);

        var error = Assert.Throws<InvalidOperationException>(() => ExportRow(row, mode, gridIcIndex: 255));

        Assert.Contains("at most 255", error.Message, StringComparison.Ordinal);
        Assert.Contains("IC index 255, diff index 0", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation)]
    public void ExportAccepts255Ics(NotchAlgorithmVersion version, NotchComputationMode mode)
    {
        var row = CreateRow(version, icIndex: 254);

        var text = ExportRow(row, mode, gridIcIndex: 254);

        Assert.Contains("#define USER_NHC_IC_NUM          (255u)", text, StringComparison.Ordinal);
        Assert.Contains("castNHC_TABLE_IC255", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation)]
    public void ExportRejects65536NodesInOneIc(NotchAlgorithmVersion version, NotchComputationMode mode)
    {
        var rows = CreateNodeRows(version, ushort.MaxValue + 1);

        var error = Assert.Throws<InvalidOperationException>(() => ExportRows(rows, mode));

        Assert.Contains("at most 65535", error.Message, StringComparison.Ordinal);
        Assert.Contains("IC index 0", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchAlgorithmVersion.V21, NotchComputationMode.CadAllocation)]
    [InlineData(NotchAlgorithmVersion.V22, NotchComputationMode.CadAllocation)]
    public void ExportAccepts65535NodesInOneIc(NotchAlgorithmVersion version, NotchComputationMode mode)
    {
        var rows = CreateNodeRows(version, ushort.MaxValue);

        var text = ExportRows(rows, mode);

        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (65535u)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportCountsReleaseNodesAfterNoOpFiltering()
    {
        var row = new NotchTableRow(0, 17, 0, 42,
            new NotchV22Node(0, 100, NullDiff, 0, NullDiff, 0, 0), "no-op");
        var rows = Enumerable.Repeat(row, ushort.MaxValue + 1).ToList();

        var text = ExportRows(rows, NotchComputationMode.CadAllocation);

        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (0u)", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void ExportRejectsFlagsOutsideByte(int flags)
    {
        var row = CreateFlagsRow(flags, useRawCompatibility: false);

        var error = Assert.Throws<InvalidOperationException>(() => ExportRow(row, NotchComputationMode.CadAllocation));

        Assert.Contains("[0,255]", error.Message, StringComparison.Ordinal);
        Assert.Contains("IC index 0, diff index 17, CAD pad id 42", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportKeepsTheByteMaskForNineValueLegacyRows()
    {
        // Position 6 of the nine-value legacy layout is the null value, not a flags field.
        var row = new NotchTableRow(NotchAlgorithmVersion.V22, 0, 17, 0, 42,
            [0, 100, 110, 1, 1, 32, 54321, 1, 32], "legacy layout");

        var text = ExportRow(row, NotchComputationMode.LegacyRegularAnchor);

        Assert.Contains("castNHC_TABLE_IC1", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExportAcceptsFlagsAtByteLimit(bool useRawCompatibility)
    {
        var row = CreateFlagsRow(byte.MaxValue, useRawCompatibility);

        var text = ExportRow(row, NotchComputationMode.CadAllocation);

        Assert.Contains("NHC_FLAG_CONTINUATION | NHC_FLAG_ANCHOR_OVERRIDE | 0xFCu", text, StringComparison.Ordinal);
    }

    private static NotchTableRow CreateDiffRow(NotchAlgorithmVersion version, int slot, int diffIndex) =>
        CreateRow(version, anchor: slot == 0 ? diffIndex : 0,
            firstTarget: slot == 1 ? diffIndex : 1, secondTarget: slot == 2 ? diffIndex : 2);

    private static NotchTableRow CreateRow(
        NotchAlgorithmVersion version,
        int anchor = 0,
        int firstTarget = 1,
        int secondTarget = 2,
        int icIndex = 0,
        int flags = 0)
    {
        int[] values = version == NotchAlgorithmVersion.V21
            ? [anchor, 100, 110, firstTarget, 1, 32, secondTarget, 1, 32]
            : [anchor, 110, firstTarget, 25, secondTarget, 25, flags];
        return new NotchTableRow(version, icIndex, 17, 0, 42, values, "range check");
    }

    private static NotchTableRow CreateFlagsRow(int flags, bool useRawCompatibility)
    {
        var row = CreateRow(NotchAlgorithmVersion.V22, flags: flags);
        return useRawCompatibility
            ? new NotchTableRow(row.Version, row.IcIndex, row.DiffIndex, row.RegularPadIndex, row.CadPadId,
                row.Values.Append(0).ToArray(), row.Comment)
            : row;
    }

    private static List<NotchTableRow> CreateNodeRows(NotchAlgorithmVersion version, int count) =>
        Enumerable.Range(0, count)
            .Select(index => CreateRow(version, anchor: index, firstTarget: NullDiff, secondTarget: NullDiff))
            .ToList();

    private static string ExportRow(NotchTableRow row, NotchComputationMode mode, int gridIcIndex = 0) =>
        ExportRows(new[] { row }, mode, gridIcIndex);

    private static string ExportRows(IReadOnlyList<NotchTableRow> rows, NotchComputationMode mode, int gridIcIndex = 0)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_gridDiffIndices, _ => gridIcIndex);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "PAD", 0, 0, 1, 1)]);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings { NullValue = NullDiff, ComputationMode = mode },
        };
        return NotchTableExporter.ExportAsCInitializer(new NotchTable(rows), cad, grid, settings);
    }
}
