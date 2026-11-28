// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MaskOffsetDominanceThresholdTests
{
    [Theory]
    [InlineData(5, 3, true)]
    [InlineData(4, 2, false)]
    public void BuildAuditRows_SharedNonzeroOffset_RequiresSixtyPercentSupport(
        int memberCount, int supportCount, bool detectsOffset)
    {
        using var culture = TestCulture.UseEnUs();
        var diffs = Enumerable.Range(1, memberCount).Select(index => index * 10).ToArray();
        var grid = TestGeometryFactory.CreateLinearRegularGrid(diffs);
        var ordered = grid.Pads
            .Select(pad => new CadPad(100 + pad.Col, "Signal", "PAD", pad.Polygon))
            .ToArray();
        var matches = PadMatcher.Match(new CadPadSet(ordered), grid);
        var assignments = new Dictionary<int, int>();
        for (var index = 0; index < memberCount; index++)
        {
            assignments[ordered[index].Id] = diffs[index] + (index < supportCount ? 2 : index + 3);
        }

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, matches.CadToRegular, grid.Pads.ToDictionary(pad => pad.RegularPadId),
            ordered.ToDictionary(pad => pad.Id, _ => 0), activeRegularPadIds: null,
            currentAssignedDiffByCadId: assignments);

        Assert.Equal(memberCount, rows.Count);
        for (var index = 0; index < memberCount; index++)
        {
            var row = rows[index];
            Assert.Equal(memberCount, row.SegmentMemberCount);
            Assert.Equal(assignments[ordered[index].Id], row.PrimaryAssignedDiffIndex);
            Assert.Equal(
                detectsOffset && index < supportCount
                    ? CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected
                    : CadOutputFwDiffAssignmentReasonCode.NeedsReview,
                row.ReasonCode);
            if (detectsOffset)
            {
                Assert.Equal(2, row.DetectedOffset);
                Assert.Equal(supportCount, row.OffsetSupportCount);
                Assert.Equal(supportCount / (double)memberCount, row.OffsetSupportRatio);
                Assert.Equal(supportCount / (double)memberCount, row.SegmentConfidence);
            }
            else
            {
                Assert.Null(row.DetectedOffset);
                Assert.Equal(0, row.OffsetSupportCount);
                Assert.Null(row.OffsetSupportRatio);
                Assert.Null(row.SegmentConfidence);
            }
        }
    }
}
