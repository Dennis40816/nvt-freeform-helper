using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchValidationUseCaseTests
{
    private const int NullDiff = 65535;

    [Fact]
    public void BuildBucket_TypedV22AndLegacyValues_ProduceEquivalentValidationResult()
    {
        const int nullDiff = 65535;
        _ = new NotchValidationUseCase();

        var typedTable = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 100,
                cadPadId: 200,
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
                cadPadId: 201,
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

        var legacyTable = new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 100,
                cadPadId: 200,
                values: new[] { 10, 120, 12, 40, 13, -20, 0 },
                comment: "legacy-main"),
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 101,
                cadPadId: 201,
                values: new[] { 12, 100, 10, 15, nullDiff, 0, 0 },
                comment: "legacy-incoming"),
        });

        var typedBucket = NotchValidationUseCase.BuildBucket(typedTable, nullDiff);
        var legacyBucket = NotchValidationUseCase.BuildBucket(legacyTable, nullDiff);

        var regularPad = CreateRegularPad(regularPadId: 100, icIndex: 0, diffIndex: 10);
        var typedReport = NotchValidationUseCase.BuildRegularReportFromBucket(100, regularPad, typedBucket);
        var legacyReport = NotchValidationUseCase.BuildRegularReportFromBucket(100, regularPad, legacyBucket);

        Assert.Equal(BuildSignature(legacyReport), BuildSignature(typedReport));
    }

    [Fact]
    public void BuildBucket_OrdersFlowsAndSkipsZeroLegsAndRowsWithoutCad()
    {
        var table = new NotchTable(new[]
        {
            CreateV22Row(diffIndex: 10, regularPadIndex: 100, cadPadId: 200, 20, 50, 21, -80),
            CreateV22Row(diffIndex: 11, regularPadIndex: 101, cadPadId: 201, 20, 50, NullDiff, 0),
            CreateV22Row(diffIndex: 12, regularPadIndex: 102, cadPadId: 202, 20, 80, 22, 0),
            new NotchTableRow(NotchAlgorithmVersion.V21, 0, 13, 100, null, new int[3], "v21"),
            CreateV22Row(diffIndex: 15, regularPadIndex: 103, cadPadId: 203, 31, 40, 30, 40),
            CreateV22Row(diffIndex: 14, regularPadIndex: 104, cadPadId: null, 40, 50, NullDiff, 0),
        });

        var bucket = NotchValidationUseCase.BuildBucket(table, NullDiff);

        // Every row is a direct row of its regular pad, in table order, numbered from 1.
        Assert.Equal([1, 4], bucket.DirectRowsByRegularPadId[100].Select(static row => row.RowNumber));
        Assert.Equal([6], bucket.DirectRowsByRegularPadId[104].Select(static row => row.RowNumber));

        // Incoming flows: the larger ratio first, then the lower regular pad. A zero ratio and a V22 row without a CAD pad make no flow.
        Assert.Equal([102, 100, 101], bucket.IncomingRowsByIcDiff[(0, 20)].Select(static flow => flow.SourceRegularPadId));
        Assert.Equal([-80d], bucket.IncomingRowsByIcDiff[(0, 21)].Select(static flow => flow.RatioPercent));
        Assert.DoesNotContain((0, 22), bucket.IncomingRowsByIcDiff.Keys);
        Assert.DoesNotContain((0, 40), bucket.IncomingRowsByIcDiff.Keys);

        // Outgoing flows: the larger ratio first (by absolute value), equal ratios by target diff.
        Assert.Equal([21, 20], bucket.OutgoingRowsByRegularPadId[100].Select(static flow => flow.TargetDiffIndex));
        Assert.Equal([30, 31], bucket.OutgoingRowsByRegularPadId[103].Select(static flow => flow.TargetDiffIndex));
        Assert.DoesNotContain(104, bucket.OutgoingRowsByRegularPadId.Keys);
    }

    private static NotchTableRow CreateV22Row(
        int diffIndex,
        int regularPadIndex,
        int? cadPadId,
        int targetDiff1,
        int ratioPercent1,
        int targetDiff2,
        int ratioPercent2)
    {
        return new NotchTableRow(
            NotchAlgorithmVersion.V22,
            icIndex: 0,
            diffIndex,
            regularPadIndex,
            cadPadId,
            new[] { diffIndex, 100, targetDiff1, ratioPercent1, targetDiff2, ratioPercent2, 0 },
            "row");
    }

    private static string BuildSignature(NotchValidationReport report)
    {
        var parts = new List<string>
        {
            $"REG={report.RegularPadId};IC={report.IcIndex};DIFF={report.DiffIndex}",
            "DIRECT:" + string.Join(
                "|",
                report.DirectRows
                    .OrderBy(static row => row.RowNumber)
                    .Select(static row => $"{row.RowNumber}:{row.Row.Version}:{row.Row.DiffIndex}:{row.Row.RegularPadIndex}:{row.Row.CadPadId}")),
            "IN:" + string.Join(
                "|",
                report.IncomingRows
                    .OrderBy(static row => row.SourceRegularPadId)
                    .ThenBy(static row => row.TargetDiffIndex)
                    .ThenBy(static row => row.SourceCadPadId)
                    .Select(static row =>
                        $"{row.SourceRegularPadId}->{row.TargetDiffIndex}:{row.RatioPercent:0.####}:{row.SourceCadPadId}")),
            "OUT:" + string.Join(
                "|",
                report.OutgoingRows
                    .OrderBy(static row => row.TargetDiffIndex)
                    .ThenBy(static row => row.SourceCadPadId)
                    .Select(static row =>
                        $"{row.SourceRegularPadId}->{row.TargetDiffIndex}:{row.RatioPercent:0.####}:{row.SourceCadPadId}"))
        };
        return string.Join(Environment.NewLine, parts);
    }

    private static RegularPad CreateRegularPad(int regularPadId, int icIndex, int diffIndex)
    {
        return TestGeometryFactory.CreateRegularPad(
            row: 0,
            col: 0,
            regularPadId,
            minX: 0,
            minY: 0,
            maxX: 10,
            maxY: 10,
            diffIndex: diffIndex,
            icIndex: icIndex);
    }
}
