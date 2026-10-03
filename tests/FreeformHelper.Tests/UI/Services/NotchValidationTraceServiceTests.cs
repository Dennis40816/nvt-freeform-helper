using System.Globalization;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchValidationTraceServiceTests
{
    [Fact]
    public void BuildTrace_TypedAndLegacyRows_KeepEquivalentTraceSemantics()
    {
        const int nullDiff = 65535;
        _ = new NotchValidationTraceService();

        var typedReport = BuildReport(BuildTypedTable(), regularPadId: 100, icIndex: 0, diffIndex: 10, nullDiff);
        var legacyReport = BuildReport(BuildLegacyTable(), regularPadId: 100, icIndex: 0, diffIndex: 10, nullDiff);
        var regularPads = BuildRegularPads();

        var typedTrace = NotchValidationTraceService.BuildTrace(typedReport, regularPads, nullDiff);
        var legacyTrace = NotchValidationTraceService.BuildTrace(legacyReport, regularPads, nullDiff);

        Assert.Equal(BuildSignature(legacyTrace), BuildSignature(typedTrace));
        Assert.Equal("typed", typedTrace.DirectRows[0].V22?.Source);
        Assert.Equal("legacy-values", legacyTrace.DirectRows[0].V22?.Source);
    }

    private static NotchValidationReport BuildReport(
        NotchTable table,
        int regularPadId,
        int icIndex,
        int diffIndex,
        int nullDiff)
    {
        var bucket = NotchValidationUseCase.BuildBucket(table, nullDiff);
        var regularPad = CreateRegularPad(regularPadId, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, icIndex, diffIndex);
        return NotchValidationUseCase.BuildRegularReportFromBucket(regularPadId, regularPad, bucket);
    }

    private static RegularPad[] BuildRegularPads()
    {
        return new[]
        {
            CreateRegularPad(100, 0, 0, 0, 0, 10, 10, icIndex: 0, diffIndex: 10),
            CreateRegularPad(101, 0, 1, 10, 0, 20, 10, icIndex: 0, diffIndex: 12),
            CreateRegularPad(102, 0, 2, 20, 0, 30, 10, icIndex: 0, diffIndex: 13),
        };
    }

    private static NotchTable BuildTypedTable()
    {
        const int nullDiff = 65535;
        return new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 100,
                cadPadId: 4200,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 10,
                    CombinePercent: 120,
                    TargetDiffIndex1: 12,
                    TargetRatioPercent1: 40,
                    TargetDiffIndex2: 13,
                    TargetRatioPercent2: -20,
                    Flags: 0),
                comment: "typed-main"),
            new(
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 101,
                cadPadId: 4201,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 12,
                    CombinePercent: 100,
                    TargetDiffIndex1: 10,
                    TargetRatioPercent1: 15,
                    TargetDiffIndex2: nullDiff,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "typed-incoming"),
        });
    }

    private static NotchTable BuildLegacyTable()
    {
        const int nullDiff = 65535;
        return new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 100,
                cadPadId: 4200,
                values: new[] { 10, 120, 12, 40, 13, -20, 0, 777 },
                comment: "legacy-main"),
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 101,
                cadPadId: 4201,
                values: new[] { 12, 100, 10, 15, nullDiff, 0, 0, 776 },
                comment: "legacy-incoming"),
        });
    }

    private static string BuildSignature(NotchValidationTraceSnapshot trace)
    {
        return string.Join(
            Environment.NewLine,
            trace.AllRows.Select(row =>
            {
                var ratio = row.RatioPercent?.ToString("0.####", CultureInfo.InvariantCulture) ?? "-";
                var area = row.EffectiveArea?.ToString("0.####", CultureInfo.InvariantCulture) ?? "-";
                var v22 = row.V22 is null
                    ? "-"
                    : $"{row.V22.AnchorDiffIndex}:{row.V22.CombinePercent}:{row.V22.TargetDiffIndex1}:{row.V22.TargetRatioPercent1}:{row.V22.TargetDiffIndex2}:{row.V22.TargetRatioPercent2}:{row.V22.Flags}";
                return string.Join(
                    "|",
                    row.KindText,
                    row.RowNumber,
                    row.IcIndex,
                    row.SourceDiffIndex,
                    row.TargetDiffIndex,
                    row.SourceRegularPadId,
                    row.TargetRegularPadId,
                    row.CadPadId,
                    ratio,
                    area,
                    row.NoteText,
                    v22);
            }));
    }

    private static RegularPad CreateRegularPad(
        int regularPadId,
        int row,
        int col,
        double minX,
        double minY,
        double maxX,
        double maxY,
        int icIndex,
        int diffIndex)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
        var pad = new RegularPad(row, col, regularPadId, polygon)
        {
            IcIndex = icIndex,
            DiffIndex = diffIndex
        };
        return pad;
    }
}
