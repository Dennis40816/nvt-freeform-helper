using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfRegularMaskAuditServiceTests
{
    [Fact]
    public void BuildAuditRows_ReportsWhenMaskChangesBestMatch()
    {
        var cad = new CadPad(384, "CAD384", "L1", BuildRect(0, 0, 1, 1));
        var ordered = new[] { cad };
        var regularById = new Dictionary<int, RegularPad>
        {
            [583] = BuildRegular(583, diffIndex: 43, icIndex: 0),
            [579] = BuildRegular(579, diffIndex: 39, icIndex: 0),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [384] = new[]
            {
                new PadMatchLink(384, 583, 10d, 0.993d, 0.991d),
                new PadMatchLink(384, 579, 4d, 0.210d, 0.205d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [384] = 0,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [384] = 39,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: new HashSet<int> { 579 },
            currentAssigned);

        var row = Assert.Single(rows);
        Assert.Equal(DxfRegularMaskAuditStatus.ChangedByMask, row.Status);
        Assert.Equal(583, row.RawSeed.RegularPadIndex);
        Assert.Equal(43, row.RawSeed.BestMatchFwDiffIndex);
        Assert.Equal(579, row.MaskedSeed.RegularPadIndex);
        Assert.Equal(39, row.MaskedSeed.BestMatchFwDiffIndex);
        Assert.Single(row.CsvConfirmedCandidates);
        Assert.Equal(39, row.SuggestedDiffIndex);
        Assert.Equal(1.0, row.CsvConfirmedConfidence);
        Assert.Equal(39, row.CurrentAssignedDiffIndex);
        Assert.Equal(39, row.PrimaryAssignedDiffIndex);
        Assert.Equal(CadOutputFwDiffAssignmentMode.CsvConstrained, row.Mode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate, row.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, row.DecisionSource);
        Assert.Equal(1, row.RepairCandidateCount);
    }

    [Fact]
    public void BuildAuditRows_CsvConstrained_InactiveBestWithOverlap_CanReceivePassiveCompensationDiff()
    {
        var cad = new CadPad(1001, "CAD1001", "L1", BuildRect(0, 0, 1, 1));
        var ordered = new[] { cad };
        var regularById = new Dictionary<int, RegularPad>
        {
            [700] = BuildRegular(700, diffIndex: 55, icIndex: 0),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [1001] = new[]
            {
                new PadMatchLink(1001, 700, 8d, 0.9d, 0.8d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [1001] = 0,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [1001] = 55,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: new HashSet<int>(),
            currentAssigned);

        var row = Assert.Single(rows);
        Assert.Equal(DxfRegularMaskAuditStatus.MaskRemovedMatch, row.Status);
        Assert.Equal(CadOutputFwDiffAssignmentMode.CsvConstrained, row.Mode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.InactiveBlocked, row.ReasonCode);
        Assert.Equal(55, row.PrimaryAssignedDiffIndex);
        Assert.Null(row.RepairSuggestionDiffIndex);
        Assert.Equal(55, row.PassiveCompensationDiffIndex);
        Assert.Empty(row.CsvConfirmedCandidates);
    }

    [Fact]
    public void BuildAuditRows_GeometryOnly_UsesGeometryOnlySuggestionReason()
    {
        var cad = new CadPad(2002, "CAD2002", "L1", BuildRect(0, 0, 1, 1));
        var ordered = new[] { cad };
        var regularById = new Dictionary<int, RegularPad>
        {
            [801] = BuildRegular(801, diffIndex: 66, icIndex: 0),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [2002] = new[]
            {
                new PadMatchLink(2002, 801, 9d, 0.92d, 0.88d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [2002] = 0,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: null,
            currentAssignedDiffByCadId: null);

        var row = Assert.Single(rows);
        Assert.Equal(CadOutputFwDiffAssignmentMode.GeometryOnly, row.Mode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.GeometryOnlySuggestion, row.ReasonCode);
        Assert.Equal(1, row.RepairCandidateCount);
        Assert.Single(row.GeometryCandidates ?? Array.Empty<DxfRegularMaskAuditCandidate>());
        Assert.Null(row.PassiveCompensationDiffIndex);
    }

    [Fact]
    public void BuildAuditRows_CsvConstrained_InactiveWithoutOverlap_DoesNotSetPassiveCompensationDiff()
    {
        var cad = new CadPad(2101, "CAD2101", "L1", BuildRect(0, 0, 1, 1));
        var ordered = new[] { cad };
        var regularById = new Dictionary<int, RegularPad>
        {
            [811] = BuildRegular(811, diffIndex: 71, icIndex: 0),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [2101] = new[]
            {
                new PadMatchLink(2101, 811, 0d, 0d, 0d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [2101] = 0,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [2101] = 71,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: new HashSet<int>(),
            currentAssignedDiffByCadId: currentAssigned);

        var row = Assert.Single(rows);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.InactiveBlocked, row.ReasonCode);
        Assert.Null(row.PassiveCompensationDiffIndex);
    }

    [Fact]
    public void BuildAuditRows_CsvConstrained_LocalRepairPrefersCandidateWithinDiffWindow()
    {
        var cad = new CadPad(2201, "CAD2201", "L1", BuildRect(0, 0, 1, 1));
        var ordered = new[] { cad };
        var regularById = new Dictionary<int, RegularPad>
        {
            [821] = BuildRegular(821, diffIndex: 33, icIndex: 0),
            [822] = BuildRegular(822, diffIndex: 31, icIndex: 0),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [2201] = new[]
            {
                new PadMatchLink(2201, 821, 8d, 0.90d, 0.80d),
                new PadMatchLink(2201, 822, 8d, 0.87d, 0.79d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [2201] = 0,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [2201] = 30,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: new HashSet<int> { 821, 822 },
            currentAssignedDiffByCadId: currentAssigned);

        var row = Assert.Single(rows);
        Assert.Equal(31, row.RepairSuggestionDiffIndex);
    }

    [Fact]
    public void BuildAuditRows_CsvConstrained_RowMaskMismatch_UsesGreedyShiftSuggestion()
    {
        var ordered = new[]
        {
            new CadPad(4823, "CAD4823", "L1", BuildRect(0, 0, 1, 1)),
            new CadPad(4848, "CAD4848", "L1", BuildRect(1, 0, 2, 1)),
        };
        var regularById = new Dictionary<int, RegularPad>
        {
            [1201] = BuildRegular(1201, diffIndex: 324, icIndex: 0, row: 6),
            [1202] = BuildRegular(1202, diffIndex: 323, icIndex: 0, row: 6),
            [1203] = BuildRegular(1203, diffIndex: 324, icIndex: 0, row: 6),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [4823] = new[]
            {
                new PadMatchLink(4823, 1201, 8d, 0.90d, 0.80d),
                new PadMatchLink(4823, 1202, 8d, 0.40d, 0.35d),
            },
            [4848] = new[]
            {
                new PadMatchLink(4848, 1203, 8d, 0.90d, 0.85d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [4823] = 0,
            [4848] = 0,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [4823] = 324,
            [4848] = 324,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: new HashSet<int> { 1201, 1202, 1203 },
            currentAssignedDiffByCadId: currentAssigned);

        var row4823 = Assert.Single(rows, row => row.CadPadId == 4823);
        var row4848 = Assert.Single(rows, row => row.CadPadId == 4848);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.DuplicateConflict, row4823.ReasonCode);
        Assert.Equal(323, row4823.RepairSuggestionDiffIndex);
        Assert.True(row4823.CsvConfirmedConfidence.GetValueOrDefault() >= 0.80d);
        Assert.Null(row4848.RepairSuggestionDiffIndex);
    }

    [Fact]
    public void BuildAuditRows_DominantSegmentOffset_PromotesReasonToSegmentOffsetSuspected()
    {
        var ordered = new[]
        {
            new CadPad(3001, "CAD3001", "L1", BuildRect(0, 0, 1, 1)),
            new CadPad(3002, "CAD3002", "L1", BuildRect(1, 0, 2, 1)),
            new CadPad(3003, "CAD3003", "L1", BuildRect(2, 0, 3, 1)),
            new CadPad(3004, "CAD3004", "L1", BuildRect(3, 0, 4, 1)),
        };
        var regularById = new Dictionary<int, RegularPad>
        {
            [901] = BuildRegular(901, diffIndex: 10, icIndex: 0, row: 2),
            [902] = BuildRegular(902, diffIndex: 11, icIndex: 0, row: 2),
            [903] = BuildRegular(903, diffIndex: 12, icIndex: 0, row: 2),
            [904] = BuildRegular(904, diffIndex: 13, icIndex: 0, row: 2),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [3001] = new[] { new PadMatchLink(3001, 901, 8d, 0.9d, 0.8d) },
            [3002] = new[] { new PadMatchLink(3002, 902, 8d, 0.9d, 0.8d) },
            [3003] = new[] { new PadMatchLink(3003, 903, 8d, 0.9d, 0.8d) },
            [3004] = new[] { new PadMatchLink(3004, 904, 8d, 0.9d, 0.8d) },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [3001] = 0,
            [3002] = 0,
            [3003] = 0,
            [3004] = 0,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [3001] = 12, // +2
            [3002] = 13, // +2
            [3003] = 14, // +2
            [3004] = 16, // +3 (non-dominant, keep unique to avoid duplicate-conflict)
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: null,
            currentAssignedDiffByCadId: currentAssigned);

        var row1 = Assert.Single(rows, row => row.CadPadId == 3001);
        var row2 = Assert.Single(rows, row => row.CadPadId == 3002);
        var row3 = Assert.Single(rows, row => row.CadPadId == 3003);
        var row4 = Assert.Single(rows, row => row.CadPadId == 3004);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, row1.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, row2.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, row3.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.NeedsReview, row4.ReasonCode);
        Assert.Equal(2, row1.DetectedOffset);
        Assert.Equal(3, row1.OffsetSupportCount);
        Assert.Equal(0.75d, row1.OffsetSupportRatio.GetValueOrDefault(), 6);
        Assert.Equal(0.75d, row1.SegmentConfidence.GetValueOrDefault(), 6);
    }

    [Fact]
    public void BuildAuditRows_AssignsIcRowSegmentMetadata_ForSegmentRows()
    {
        var ordered = new[]
        {
            new CadPad(3101, "CAD3101", "L1", BuildRect(0, 0, 1, 1)),
            new CadPad(3102, "CAD3102", "L1", BuildRect(1, 0, 2, 1)),
            new CadPad(3103, "CAD3103", "L1", BuildRect(2, 0, 3, 1)),
        };
        var regularById = new Dictionary<int, RegularPad>
        {
            [911] = BuildRegular(911, diffIndex: 30, icIndex: 1, row: 4),
            [912] = BuildRegular(912, diffIndex: 31, icIndex: 1, row: 4),
            [913] = BuildRegular(913, diffIndex: 32, icIndex: 1, row: 4),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [3101] = new[] { new PadMatchLink(3101, 911, 8d, 0.9d, 0.8d) },
            [3102] = new[] { new PadMatchLink(3102, 912, 8d, 0.9d, 0.8d) },
            [3103] = new[] { new PadMatchLink(3103, 913, 8d, 0.9d, 0.8d) },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [3101] = 1,
            [3102] = 1,
            [3103] = 1,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [3101] = 30,
            [3102] = 31,
            [3103] = 32,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: null,
            currentAssignedDiffByCadId: currentAssigned);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(1, row.IcIndex);
            Assert.Equal(4, row.RowIndex);
            Assert.Equal(0, row.SegmentIndex);
            Assert.Equal(3, row.SegmentMemberCount);
        });
    }

    [Fact]
    public void BuildAuditRows_SegmentOffsetDetection_DoesNotPromoteManualOverrideAnchor()
    {
        var ordered = new[]
        {
            new CadPad(4001, "CAD4001", "L1", BuildRect(0, 0, 1, 1)),
            new CadPad(4002, "CAD4002", "L1", BuildRect(1, 0, 2, 1)),
            new CadPad(4003, "CAD4003", "L1", BuildRect(2, 0, 3, 1)),
        };
        var regularById = new Dictionary<int, RegularPad>
        {
            [951] = BuildRegular(951, diffIndex: 20, icIndex: 0, row: 3),
            [952] = BuildRegular(952, diffIndex: 21, icIndex: 0, row: 3),
            [953] = BuildRegular(953, diffIndex: 22, icIndex: 0, row: 3),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [4001] = new[] { new PadMatchLink(4001, 951, 8d, 0.9d, 0.8d) },
            [4002] = new[] { new PadMatchLink(4002, 952, 8d, 0.9d, 0.8d) },
            [4003] = new[] { new PadMatchLink(4003, 953, 8d, 0.9d, 0.8d) },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [4001] = 0,
            [4002] = 0,
            [4003] = 0,
        };
        var currentAssigned = new Dictionary<int, int>
        {
            [4001] = 21, // +1 (manual override anchor)
            [4002] = 22, // +1
            [4003] = 23, // +1
        };
        var manualOverrides = new Dictionary<int, int>
        {
            [4001] = 21,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: null,
            currentAssignedDiffByCadId: currentAssigned,
            manualOverrideDiffByCadId: manualOverrides);

        var anchor = Assert.Single(rows, row => row.CadPadId == 4001);
        var row2 = Assert.Single(rows, row => row.CadPadId == 4002);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.ManualOverride, anchor.DecisionSource);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.NeedsReview, anchor.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, row2.ReasonCode);
        Assert.Equal(1, anchor.DetectedOffset);
        Assert.Equal(3, anchor.OffsetSupportCount);
        Assert.Equal(1.0d, anchor.OffsetSupportRatio.GetValueOrDefault(), 6);
    }

    private static Polygon2 BuildRect(double minX, double minY, double maxX, double maxY)
    {
        return new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
    }

    private static RegularPad BuildRegular(int regularPadId, int diffIndex, int icIndex, int row = 0)
    {
        return new RegularPad(
            row: row,
            col: 0,
            index: regularPadId,
            polygon: BuildRect(0, 0, 1, 1))
        {
            DiffIndex = diffIndex,
            IcIndex = icIndex,
        };
    }
}
