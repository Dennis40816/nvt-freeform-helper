using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchValidationUseCaseTests
{
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
