// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchValidationTraceBoundaryBehaviorTests
{
    [Fact]
    public void BuildTrace_LegacyRowsWithoutV22Payload_PreservesRawRowsAndComments()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var table = new NotchTable(
        [
            new NotchTableRow(NotchAlgorithmVersion.V21, 0, 10, 0, null, [], "  "),
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 10, 0, 101, [10, 100, 11, 25, 65535, 0], "  incomplete payload  "),
        ]);
        var bucket = NotchValidationUseCase.BuildBucket(table, ushort.MaxValue);
        var report = NotchValidationUseCase.BuildRegularReportFromBucket(0, grid.Pads[0], bucket);

        var trace = NotchValidationTraceService.BuildTrace(report, grid.Pads, ushort.MaxValue);

        Assert.Empty(trace.IncomingRows);
        Assert.Empty(trace.OutgoingRows);
        Assert.Equal(2, trace.AllRows.Count);
        Assert.Collection(
            trace.DirectRows,
            row =>
            {
                Assert.Equal((1, "-", "-"), (row.RowNumber, row.ValuesText, row.CommentText));
                Assert.Empty(row.Values);
                Assert.Null(row.V22);
            },
            row =>
            {
                Assert.Equal((2, 101), (row.RowNumber, row.CadPadId));
                Assert.Equal([10, 100, 11, 25, 65535, 0], row.Values);
                Assert.Equal("10, 100, 11, 25, 65535, 0", row.ValuesText);
                Assert.Equal("incomplete payload", row.CommentText);
                Assert.Null(row.V22);
            });
    }

    [Fact]
    public void BuildTrace_FlowAreasAndMissingTargets_PreservesAvailableEvidenceAndMarksUnknownPads()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var table = new NotchTable(
        [
            new NotchTableRow(0, 10, 0, 101, new NotchV22Node(10, 100, 11, 25, ushort.MaxValue, 0, 0), "incoming"),
            new NotchTableRow(0, 11, 1, 102, new NotchV22Node(11, 100, 99, -25, ushort.MaxValue, 0, 0), "  signed flow  "),
        ]);
        var bucket = NotchValidationUseCase.BuildBucket(table, ushort.MaxValue);
        var report = NotchValidationUseCase.BuildRegularReportFromBucket(1, grid.Pads[1], bucket);
        var incoming = Assert.Single(report.IncomingRows) with { EffectiveArea = 12.5d };
        var outgoing = Assert.Single(report.OutgoingRows) with { EffectiveArea = 7.25d };
        report = report with { IncomingRows = [incoming], OutgoingRows = [outgoing] };

        var trace = NotchValidationTraceService.BuildTrace(report, grid.Pads, ushort.MaxValue);

        var incomingTrace = Assert.Single(trace.IncomingRows);
        Assert.Equal((1, 25d, 12.5d),
            (incomingTrace.TargetRegularPadId, incomingTrace.RatioPercent, incomingTrace.EffectiveArea));
        Assert.Equal(("IC1/diff10", "IC1/diff11"), (incomingTrace.SourceText, incomingTrace.TargetText));
        var outgoingTrace = Assert.Single(trace.OutgoingRows);
        Assert.Equal((-1, -25d, 7.25d),
            (outgoingTrace.TargetRegularPadId, outgoingTrace.RatioPercent, outgoingTrace.EffectiveArea));
        Assert.Equal(("IC1/diff11", "IC1/diff99"), (outgoingTrace.SourceText, outgoingTrace.TargetText));
        Assert.Equal("signed flow", outgoingTrace.CommentText);
        Assert.Equal(["DIRECT", "IN", "OUT"], trace.AllRows.Select(static row => row.KindText));

        var unknownAreaReport = report with
        {
            IncomingRows = [incoming with { EffectiveArea = double.NaN }],
            OutgoingRows = [outgoing with { EffectiveArea = double.PositiveInfinity }],
        };
        var unknownAreaTrace = NotchValidationTraceService.BuildTrace(unknownAreaReport, grid.Pads, ushort.MaxValue);

        Assert.Null(Assert.Single(unknownAreaTrace.IncomingRows).EffectiveArea);
        Assert.Null(Assert.Single(unknownAreaTrace.OutgoingRows).EffectiveArea);
        Assert.Equal(-25d, Assert.Single(unknownAreaTrace.OutgoingRows).RatioPercent);
    }
}
