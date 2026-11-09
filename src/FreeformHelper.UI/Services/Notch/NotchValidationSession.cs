// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Diagnostics;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>Owns the Notch validation bucket cache and report building. Access is UI-thread-only.</summary>
internal sealed class NotchValidationSession
{
    private (NotchValidationBucket Bucket, NotchTable Table, int NullDiffValue)? _bucketCache;

    internal BucketBuildResult GetOrBuildBucket(NotchTable table, int nullDiffValue)
    {
        if (_bucketCache is { } cached &&
            ReferenceEquals(cached.Table, table) &&
            cached.NullDiffValue == nullDiffValue)
        {
            return new BucketBuildResult(cached.Bucket, WasRebuilt: false, ElapsedMilliseconds: 0);
        }

        var stopwatch = Stopwatch.StartNew();
        var bucket = NotchValidationUseCase.BuildBucket(table, nullDiffValue);
        _bucketCache = (bucket, table, nullDiffValue);
        return new BucketBuildResult(bucket, WasRebuilt: true, stopwatch.ElapsedMilliseconds);
    }

    internal void Invalidate() => _bucketCache = null;

    internal bool TryBuildReport(
        RegularGrid? grid,
        NotchTable? lastGeneratedTable,
        int regularPadId,
        int nullDiffValue,
        out ReportBuildResult result)
    {
        if (grid is null)
        {
            result = new ReportBuildResult(null, "NOT_READY", "Validation: build grid first.");
            return false;
        }

        var regularPad = grid.Pads.FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regularPad is null)
        {
            result = new ReportBuildResult(null, "PAD_NOT_FOUND", $"Validation: REG {regularPadId} not found.");
            return false;
        }

        if (lastGeneratedTable is null || lastGeneratedTable.Rows.Count == 0)
        {
            result = new ReportBuildResult(
                null, "NOT_READY", "Validation: no generated notch table. Run Step 5 Export once first.");
            return false;
        }

        var bucketResult = GetOrBuildBucket(lastGeneratedTable, nullDiffValue);
        var report = NotchValidationUseCase.BuildRegularReportFromBucket(regularPadId, regularPad, bucketResult.Bucket);
        result = new ReportBuildResult(report, string.Empty, string.Empty, bucketResult);
        return true;
    }

    internal sealed record BucketBuildResult(NotchValidationBucket Bucket, bool WasRebuilt, long ElapsedMilliseconds);

    internal sealed record ReportBuildResult(
        NotchValidationReport? Report,
        string FailureCode,
        string FailureMessage,
        BucketBuildResult? BucketResult = null);
}
