// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MaskSegmentOffsetBehaviorTests
{
    [Fact]
    public void BuildAuditRows_UnassignedSegmentMembers_DoNotDiluteOffsetSupportOrReceiveOffsetEvidence()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 20, 30, 40, 50]);
        var ordered = new List<CadPad>();
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        var icIndices = new Dictionary<int, int>();
        foreach (var pad in grid.Pads)
        {
            var cad = new CadPad(100 + pad.Col, "Signal", "PAD", pad.Polygon);
            ordered.Add(cad);
            links[cad.Id] = [new(cad.Id, pad.RegularPadId, 1d, 1d, 1d)];
            icIndices[cad.Id] = 0;
        }

        var assignments = new Dictionary<int, int> { [100] = 12, [101] = 22, [102] = 33 };
        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, links, grid.Pads.ToDictionary(pad => pad.RegularPadId), icIndices,
            activeRegularPadIds: null, currentAssignedDiffByCadId: assignments);

        Assert.Equal(new int?[] { 12, 22, 33, null, null }, rows.Select(row => row.PrimaryAssignedDiffIndex));
        Assert.All(rows, row => Assert.Equal(5, row.SegmentMemberCount));
        Assert.All(rows.Take(3), row =>
        {
            Assert.Equal(2, row.DetectedOffset);
            Assert.Equal(2, row.OffsetSupportCount);
            Assert.Equal(2d / 3d, row.OffsetSupportRatio);
            Assert.Equal(2d / 3d, row.SegmentConfidence);
        });
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, rows[0].ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, rows[1].ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.NeedsReview, rows[2].ReasonCode);
        Assert.All(rows.Skip(3), row =>
        {
            Assert.Null(row.DetectedOffset);
            Assert.Equal(0, row.OffsetSupportCount);
            Assert.Null(row.OffsetSupportRatio);
            Assert.Null(row.SegmentConfidence);
            Assert.Equal(CadOutputFwDiffAssignmentReasonCode.GeometryOnlySuggestion, row.ReasonCode);
        });
    }

    [Fact]
    public void BuildAuditRows_CsvChangesEverySeed_MeasuresOffsetFromMaskedDiffs()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 20, 21, 30, 31]);
        var ordered = new List<CadPad>();
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        var icIndices = new Dictionary<int, int>();
        var assignments = new Dictionary<int, int>();
        var activeIds = new HashSet<int>();
        for (var index = 0; index < 3; index++)
        {
            var raw = grid.Pads[index * 2];
            var masked = grid.Pads[index * 2 + 1];
            var cad = TestGeometryFactory.CreateCadPad(100 + index, "PAD", index * 2, 0, index * 2 + 1.5, 1);
            ordered.Add(cad);
            links[cad.Id] =
            [
                new(cad.Id, raw.RegularPadId, 1d, 1d, 2d / 3d),
                new(cad.Id, masked.RegularPadId, 0.5d, 0.5d, 1d / 3d),
            ];
            icIndices[cad.Id] = 0;
            assignments[cad.Id] = masked.DiffIndex + 2;
            activeIds.Add(masked.RegularPadId);
        }

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, links, grid.Pads.ToDictionary(pad => pad.RegularPadId), icIndices, activeIds, assignments);

        Assert.Equal(new int?[] { 10, 20, 30 }, rows.Select(row => row.RawSeed.BestMatchFwDiffIndex));
        Assert.Equal(new int?[] { 11, 21, 31 }, rows.Select(row => row.MaskedSeed.BestMatchFwDiffIndex));
        Assert.Equal(new int?[] { 13, 23, 33 }, rows.Select(row => row.PrimaryAssignedDiffIndex));
        Assert.All(rows, row =>
        {
            Assert.Equal(DxfRegularMaskAuditStatus.ChangedByMask, row.Status);
            Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, row.ReasonCode);
            Assert.Equal(2, row.DetectedOffset);
            Assert.Equal(3, row.OffsetSupportCount);
            Assert.Equal(1d, row.OffsetSupportRatio);
            Assert.Equal(1d, row.SegmentConfidence);
        });
    }
}
