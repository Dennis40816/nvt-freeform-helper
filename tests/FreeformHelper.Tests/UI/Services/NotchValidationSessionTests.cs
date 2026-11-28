// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchValidationSessionTests
{
    private const int NullDiff = 65535;

    [Fact]
    public void TryBuildReport_WithoutGrid_ReturnsGridFailureBeforePadAndTableChecks()
    {
        var session = new NotchValidationSession();

        var success = session.TryBuildReport(null, null, 999, NullDiff, out var result);

        Assert.False(success);
        Assert.Equal("NOT_READY", result.FailureCode);
        Assert.Equal("Validation: build grid first.", result.FailureMessage);
        Assert.Null(result.Report);
        Assert.Null(result.BucketResult);
    }

    [Fact]
    public void TryBuildReport_WithoutRequestedPad_ReturnsPadFailureBeforeTableCheck()
    {
        var session = new NotchValidationSession();
        var grid = CreateGrid();

        var success = session.TryBuildReport(grid, null, 999, NullDiff, out var result);

        Assert.False(success);
        Assert.Equal("PAD_NOT_FOUND", result.FailureCode);
        Assert.Equal("Validation: REG 999 not found.", result.FailureMessage);
        Assert.Null(result.Report);
        Assert.Null(result.BucketResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryBuildReport_WithoutGeneratedRows_ReturnsExportFailure(bool useEmptyTable)
    {
        var session = new NotchValidationSession();
        var grid = CreateGrid();
        NotchTable? table = useEmptyTable ? new NotchTable([]) : null;

        var success = session.TryBuildReport(grid, table, 100, NullDiff, out var result);

        Assert.False(success);
        Assert.Equal("NOT_READY", result.FailureCode);
        Assert.Equal("Validation: no generated notch table. Run Step 5 Export once first.", result.FailureMessage);
        Assert.Null(result.Report);
        Assert.Null(result.BucketResult);
    }

    [Fact]
    public void TryBuildReport_WithGeneratedRows_ReturnsRequestedPadAndItsFlows()
    {
        var session = new NotchValidationSession();
        var grid = CreateGrid();
        var table = CreateTable();

        var success = session.TryBuildReport(grid, table, 100, NullDiff, out var result);

        Assert.True(success);
        Assert.Empty(result.FailureCode);
        Assert.Empty(result.FailureMessage);
        var report = Assert.IsType<NotchValidationReport>(result.Report);
        Assert.Equal(100, report.RegularPadId);
        Assert.Equal(1, report.IcIndex);
        Assert.Equal(10, report.DiffIndex);
        Assert.Same(table.Rows[0], Assert.Single(report.DirectRows).Row);
        var incoming = Assert.Single(report.IncomingRows);
        Assert.Same(table.Rows[1], incoming.SourceRow.Row);
        Assert.Equal(101, incoming.SourceRegularPadId);
        Assert.Equal(10, incoming.TargetDiffIndex);
        Assert.Equal(15d, incoming.RatioPercent);
        var outgoing = Assert.Single(report.OutgoingRows);
        Assert.Same(table.Rows[0], outgoing.SourceRow.Row);
        Assert.Equal(20, outgoing.TargetDiffIndex);
        Assert.Equal(40d, outgoing.RatioPercent);
        var bucketResult = Assert.IsType<NotchValidationSession.BucketBuildResult>(result.BucketResult);
        Assert.True(bucketResult.WasRebuilt);

        Assert.True(session.TryBuildReport(grid, table, 101, NullDiff, out var second));
        Assert.Equal(101, Assert.IsType<NotchValidationReport>(second.Report).RegularPadId);
        var cached = Assert.IsType<NotchValidationSession.BucketBuildResult>(second.BucketResult);
        Assert.False(cached.WasRebuilt);
        Assert.Same(bucketResult.Bucket, cached.Bucket);
        Assert.Equal(new NotchValidationSession.BucketBuildResult(bucketResult.Bucket, false, 0), cached);
    }

    [Fact]
    public void GetOrBuildBucket_SameTableAndNullValue_ReusesBucket()
    {
        var session = new NotchValidationSession();
        var table = CreateTable();
        var first = session.GetOrBuildBucket(table, NullDiff);

        var second = session.GetOrBuildBucket(table, NullDiff);

        Assert.True(first.WasRebuilt);
        Assert.False(second.WasRebuilt);
        Assert.Same(first.Bucket, second.Bucket);
        Assert.Equal(new NotchValidationSession.BucketBuildResult(first.Bucket, false, 0), second);
    }

    [Fact]
    public void GetOrBuildBucket_DifferentTableWithSameRows_RebuildsBucket()
    {
        var session = new NotchValidationSession();
        var table = CreateTable();
        var first = session.GetOrBuildBucket(table, NullDiff);

        var second = session.GetOrBuildBucket(new NotchTable(table.Rows), NullDiff);

        Assert.True(second.WasRebuilt);
        Assert.NotSame(first.Bucket, second.Bucket);
    }

    [Fact]
    public void GetOrBuildBucket_DifferentNullValue_RebuildsBucketAndChangesNullLegs()
    {
        var session = new NotchValidationSession();
        var table = CreateTable();
        var first = session.GetOrBuildBucket(table, NullDiff);

        var second = session.GetOrBuildBucket(table, 20);

        Assert.True(second.WasRebuilt);
        Assert.NotSame(first.Bucket, second.Bucket);
        Assert.Single(first.Bucket.OutgoingRowsByRegularPadId[100]);
        Assert.DoesNotContain(100, second.Bucket.OutgoingRowsByRegularPadId.Keys);
    }

    [Fact]
    public void Invalidate_SameTableAndNullValue_RebuildsBucket()
    {
        var session = new NotchValidationSession();
        var table = CreateTable();
        var first = session.GetOrBuildBucket(table, NullDiff);

        session.Invalidate();
        var second = session.GetOrBuildBucket(table, NullDiff);

        Assert.True(second.WasRebuilt);
        Assert.NotSame(first.Bucket, second.Bucket);
    }

    private static RegularGrid CreateGrid() => TestGeometryFactory.CreateRegularGrid(
        1, 2, cellWidth: 10, cellHeight: 10,
        diffIndexSelector: static (_, col) => (col + 1) * 10,
        icIndexSelector: static (_, _) => 1,
        regularPadIdSelector: static (_, col) => 100 + col);

    private static NotchTable CreateTable() => new(
    [
        new NotchTableRow(NotchAlgorithmVersion.V22, icIndex: 1, diffIndex: 10, regularPadIndex: 100, cadPadId: 200,
            values: [10, 100, 20, 40, NullDiff, 0, 0], comment: "outgoing"),
        new NotchTableRow(NotchAlgorithmVersion.V22, icIndex: 1, diffIndex: 20, regularPadIndex: 101, cadPadId: 201,
            values: [20, 100, 10, 15, NullDiff, 0, 0], comment: "incoming"),
    ]);
}
