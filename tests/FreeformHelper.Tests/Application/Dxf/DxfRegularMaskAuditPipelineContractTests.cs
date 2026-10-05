using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfRegularMaskAuditPipelineContractTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssignmentAndAudit_SegmentOffsetPrecedesRepairAndChangesPassiveDiff(bool hasOffset)
    {
        var ordered = new[]
        {
            new CadPad(43, "CAD43", "L1", BuildRect()),
            new CadPad(7, "CAD7", "L1", BuildRect()),
            new CadPad(91, "CAD91", "L1", BuildRect()),
            new CadPad(18, "CAD18", "L1", BuildRect()),
        };
        var regularById = new Dictionary<int, RegularPad>
        {
            [100] = BuildRegular(100, 10, icIndex: 0, row: 2),
            [101] = BuildRegular(101, 12, icIndex: 0, row: 2),
            [102] = BuildRegular(102, 11, icIndex: 0, row: 2),
            [103] = BuildRegular(103, 13, icIndex: 0, row: 2),
            [104] = BuildRegular(104, 12, icIndex: 0, row: 2),
            [105] = BuildRegular(105, 14, icIndex: 0, row: 2),
            [106] = BuildRegular(106, 13, icIndex: 0, row: 2),
            [107] = BuildRegular(107, 15, icIndex: 0, row: 2),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = new[]
            {
                new PadMatchLink(43, 100, 1d, 1d, 1d),
                new PadMatchLink(43, 101, 0.9d, 0.9d, 0.9d),
            },
            [7] = new[]
            {
                new PadMatchLink(7, 102, 1d, 1d, 1d),
                new PadMatchLink(7, 103, 0.9d, 0.9d, 0.9d),
            },
            [91] = new[]
            {
                new PadMatchLink(91, 104, 1d, 1d, 1d),
                new PadMatchLink(91, 105, 0.9d, 0.9d, 0.9d),
            },
            [18] = new[]
            {
                new PadMatchLink(18, 106, 1d, 1d, 1d),
                new PadMatchLink(18, 107, 0.9d, 0.9d, 0.9d),
            },
        };
        var cadIcById = new Dictionary<int, int> { [43] = 0, [7] = 0, [91] = 0, [18] = 0 };
        var currentAssigned = new Dictionary<int, int>
        {
            [43] = hasOffset ? 12 : 10,
            [7] = hasOffset ? 13 : 11,
            [91] = hasOffset ? 14 : 12,
            [18] = 16,
        };
        var manualOverrides = new Dictionary<int, int> { [43] = currentAssigned[43] };
        var activeRegularIds = new HashSet<int> { 100, 101, 102, 103, 104, 105 };

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, cadToRegular, regularById, cadIcById, activeRegularIds, currentAssigned, manualOverrides);
        var orderedDecisions = ordered.Select(pad => decisions[pad.Id]).ToArray();

        // Fixed outputs from the current production seams; only the three anchor assignments vary.
        var expectedCadIds = new[] { 43, 7, 91, 18 };
        var expectedOrderedIndices = new[] { 0, 1, 2, 3 };
        var expectedGeometryCounts = new[] { 2, 2, 2, 2 };
        var expectedRepairCounts = new[] { 2, 2, 2, 0 };
        Assert.Equal(4, decisions.Count);
        Assert.Equal(expectedCadIds, orderedDecisions.Select(decision => decision.CadPadId));
        Assert.Equal(expectedOrderedIndices, orderedDecisions.Select(decision => decision.OrderedCadIndex));
        Assert.Equal(new int?[] { 10, 11, 12, 13 }, orderedDecisions.Select(decision => decision.RawSeed.BestMatchFwDiffIndex));
        Assert.Equal(new int?[] { 10, 11, 12, null }, orderedDecisions.Select(decision => decision.MaskedSeed.BestMatchFwDiffIndex));
        Assert.Equal(expectedGeometryCounts, orderedDecisions.Select(decision => decision.GeometryCandidates.Count));
        Assert.Equal(expectedRepairCounts, orderedDecisions.Select(decision => decision.RepairCandidateCount));
        Assert.Equal(
            hasOffset ? new int?[] { 12, 13, 14, 16 } : new int?[] { 10, 11, 12, 16 },
            orderedDecisions.Select(decision => decision.CurrentPrimaryDiffIndex));
        Assert.Equal(
            hasOffset ? new int?[] { 10, 11, 12, null } : new int?[] { null, 13, 14, null },
            orderedDecisions.Select(decision => decision.RepairSuggestionDiffIndex));
        // The inactive row keeps primary diff 16: detected +2 selects 15 instead of the higher-coverage 13.
        Assert.Equal(
            new int?[] { null, null, null, hasOffset ? 15 : 13 },
            orderedDecisions.Select(decision => decision.PassiveCompensationDiffIndex));
        Assert.Equal(
            hasOffset ? new double?[] { 0.9d, 0.9d, 0.9d, null } : new double?[] { 1d, 1d, 0.95d, null },
            orderedDecisions.Select(decision => decision.Confidence));
        Assert.Equal(
            new[]
            {
                CadOutputFwDiffAssignmentReasonCode.NeedsReview,
                hasOffset ? CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected : CadOutputFwDiffAssignmentReasonCode.NeedsReview,
                hasOffset ? CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected : CadOutputFwDiffAssignmentReasonCode.NeedsReview,
                CadOutputFwDiffAssignmentReasonCode.InactiveBlocked,
            },
            orderedDecisions.Select(decision => decision.ReasonCode));
        Assert.Equal(
            new[]
            {
                CadOutputFwDiffAssignmentDecisionSource.ManualOverride,
                CadOutputFwDiffAssignmentDecisionSource.Seed,
                CadOutputFwDiffAssignmentDecisionSource.Seed,
                CadOutputFwDiffAssignmentDecisionSource.Seed,
            },
            orderedDecisions.Select(decision => decision.DecisionSource));
        Assert.All(orderedDecisions, decision =>
        {
            Assert.Equal(CadOutputFwDiffAssignmentMode.CsvConstrained, decision.Mode);
            Assert.Equal(0, decision.IcIndex);
            Assert.Equal(2, decision.RowIndex);
            Assert.Equal(0, decision.SegmentIndex);
            Assert.Equal(4, decision.SegmentMemberCount);
            Assert.Equal(hasOffset ? 2 : (int?)null, decision.DetectedOffset);
            Assert.Equal(hasOffset ? 3 : 0, decision.OffsetSupportCount);
            Assert.Equal(hasOffset ? 0.75d : (double?)null, decision.OffsetSupportRatio);
            Assert.Equal(hasOffset ? 0.75d : (double?)null, decision.SegmentConfidence);
        });

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, cadToRegular, regularById, cadIcById, activeRegularIds, currentAssigned, manualOverrides);
        AssertAuditProjection(orderedDecisions, rows);
        Assert.Equal(new int?[] { 10, 11, 12, null }, rows.Select(row => row.SuggestedDiffIndex));
        Assert.Equal(
            new[]
            {
                DxfRegularMaskAuditStatus.Unchanged,
                DxfRegularMaskAuditStatus.Unchanged,
                DxfRegularMaskAuditStatus.Unchanged,
                DxfRegularMaskAuditStatus.MaskRemovedMatch,
            },
            rows.Select(row => row.Status));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssignmentAndAudit_NullAndEmptyMaskKeepGeometryAndCsvBoundaries(bool hasCsvMask)
    {
        var ordered = new[]
        {
            new CadPad(70, "CAD70", "L1", BuildRect()),
            new CadPad(2, "CAD2", "L1", BuildRect()),
            new CadPad(5, "CAD5", "L1", BuildRect()),
        };
        var regularById = new Dictionary<int, RegularPad>
        {
            [200] = BuildRegular(200, 30, icIndex: 1, row: 4),
            [201] = BuildRegular(201, 40, icIndex: 1, row: 4),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [70] = new[] { new PadMatchLink(70, 200, 0.8d, 0.8d, 0.7d) },
            [5] = new[] { new PadMatchLink(5, 201, 0d, 0d, 0d) },
        };
        var cadIcById = new Dictionary<int, int> { [70] = 1, [2] = 1, [5] = 1 };
        IReadOnlySet<int>? activeRegularIds = hasCsvMask ? new HashSet<int>() : null;

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, cadToRegular, regularById, cadIcById, activeRegularIds);
        var orderedDecisions = ordered.Select(pad => decisions[pad.Id]).ToArray();

        var expectedCadIds = new[] { 70, 2, 5 };
        var expectedOrderedIndices = new[] { 0, 1, 2 };
        var expectedGeometryCounts = new[] { 1, 0, 1 };
        var expectedRepairCounts = hasCsvMask ? new[] { 0, 0, 0 } : new[] { 1, 0, 1 };
        var expectedSegmentMemberCounts = new[] { 1, 0, 1 };
        var candidateReason = hasCsvMask
            ? CadOutputFwDiffAssignmentReasonCode.InactiveBlocked
            : CadOutputFwDiffAssignmentReasonCode.GeometryOnlySuggestion;
        Assert.Equal(3, decisions.Count);
        Assert.Equal(expectedCadIds, orderedDecisions.Select(decision => decision.CadPadId));
        Assert.Equal(expectedOrderedIndices, orderedDecisions.Select(decision => decision.OrderedCadIndex));
        Assert.Equal(new int?[] { 30, null, 40 }, orderedDecisions.Select(decision => decision.RawSeed.BestMatchFwDiffIndex));
        Assert.Equal(
            hasCsvMask ? new int?[] { null, null, null } : new int?[] { 30, null, 40 },
            orderedDecisions.Select(decision => decision.MaskedSeed.BestMatchFwDiffIndex));
        Assert.Equal(expectedGeometryCounts, orderedDecisions.Select(decision => decision.GeometryCandidates.Count));
        Assert.Equal(expectedRepairCounts, orderedDecisions.Select(decision => decision.RepairCandidateCount));
        Assert.Equal(
            hasCsvMask ? new int?[] { null, null, null } : new int?[] { 30, null, 40 },
            orderedDecisions.Select(decision => decision.RepairSuggestionDiffIndex));
        Assert.Equal(
            hasCsvMask ? new int?[] { 30, null, null } : new int?[] { null, null, null },
            orderedDecisions.Select(decision => decision.PassiveCompensationDiffIndex));
        Assert.Equal(
            hasCsvMask ? new double?[] { null, null, null } : new double?[] { 1d, null, 0d },
            orderedDecisions.Select(decision => decision.Confidence));
        Assert.Equal(
            new[]
            {
                candidateReason,
                CadOutputFwDiffAssignmentReasonCode.NeedsReview,
                candidateReason,
            },
            orderedDecisions.Select(decision => decision.ReasonCode));
        Assert.Equal(new int?[] { 1, null, 1 }, orderedDecisions.Select(decision => decision.IcIndex));
        Assert.Equal(new int?[] { 4, null, 4 }, orderedDecisions.Select(decision => decision.RowIndex));
        Assert.Equal(new int?[] { 0, null, 1 }, orderedDecisions.Select(decision => decision.SegmentIndex));
        Assert.Equal(expectedSegmentMemberCounts, orderedDecisions.Select(decision => decision.SegmentMemberCount));
        Assert.All(orderedDecisions, decision =>
        {
            Assert.Equal(
                hasCsvMask ? CadOutputFwDiffAssignmentMode.CsvConstrained : CadOutputFwDiffAssignmentMode.GeometryOnly,
                decision.Mode);
            Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, decision.DecisionSource);
            Assert.Null(decision.CurrentPrimaryDiffIndex);
            Assert.Null(decision.DetectedOffset);
            Assert.Equal(0, decision.OffsetSupportCount);
            Assert.Null(decision.OffsetSupportRatio);
            Assert.Null(decision.SegmentConfidence);
        });

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, cadToRegular, regularById, cadIcById, activeRegularIds);
        AssertAuditProjection(orderedDecisions, rows);
        Assert.Equal(
            hasCsvMask ? new int?[] { null, null, null } : new int?[] { 30, null, 40 },
            rows.Select(row => row.SuggestedDiffIndex));
        Assert.Equal(
            new[]
            {
                hasCsvMask ? DxfRegularMaskAuditStatus.MaskRemovedMatch : DxfRegularMaskAuditStatus.Unchanged,
                DxfRegularMaskAuditStatus.NoRawBestMatch,
                hasCsvMask ? DxfRegularMaskAuditStatus.MaskRemovedMatch : DxfRegularMaskAuditStatus.Unchanged,
            },
            rows.Select(row => row.Status));
    }

    private static void AssertAuditProjection(
        CadOutputFwDiffAssignmentDecision[] decisions,
        IReadOnlyList<DxfRegularMaskAuditRow> rows)
    {
        Assert.Equal(decisions.Length, rows.Count);
        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
            var row = rows[index];
            Assert.Equal(decision.CadPadId, row.CadPadId);
            Assert.Equal(decision.OrderedCadIndex, row.OrderedCadIndex);
            Assert.Equal(decision.Mode, row.Mode);
            Assert.Equal(decision.RawSeed, row.RawSeed);
            Assert.Equal(decision.MaskedSeed, row.MaskedSeed);
            Assert.Equal(decision.GeometryCandidates, row.GeometryCandidates);
            Assert.Equal(decision.CsvConfirmedCandidates, row.CsvConfirmedCandidates);
            Assert.Equal(decision.CurrentPrimaryDiffIndex, row.CurrentAssignedDiffIndex);
            Assert.Equal(decision.CurrentPrimaryDiffIndex, row.PrimaryAssignedDiffIndex);
            Assert.Equal(decision.RepairSuggestionDiffIndex, row.RepairSuggestionDiffIndex);
            Assert.Equal(decision.PassiveCompensationDiffIndex, row.PassiveCompensationDiffIndex);
            Assert.Equal(decision.RepairCandidateCount, row.RepairCandidateCount);
            Assert.Equal(decision.Confidence, row.CsvConfirmedConfidence);
            Assert.Equal(decision.ReasonCode, row.ReasonCode);
            Assert.Equal(decision.DecisionSource, row.DecisionSource);
            Assert.Equal(decision.DetectedOffset, row.DetectedOffset);
            Assert.Equal(decision.OffsetSupportCount, row.OffsetSupportCount);
            Assert.Equal(decision.OffsetSupportRatio, row.OffsetSupportRatio);
            Assert.Equal(decision.SegmentConfidence, row.SegmentConfidence);
            Assert.Equal(decision.IcIndex, row.IcIndex);
            Assert.Equal(decision.RowIndex, row.RowIndex);
            Assert.Equal(decision.SegmentIndex, row.SegmentIndex);
            Assert.Equal(decision.SegmentMemberCount, row.SegmentMemberCount);
        }
    }

    private static Polygon2 BuildRect() => new(new[]
    {
        new Point2(0, 0),
        new Point2(1, 0),
        new Point2(1, 1),
        new Point2(0, 1),
    });

    private static RegularPad BuildRegular(int regularPadId, int diffIndex, int icIndex, int row) =>
        new(row, col: 0, index: regularPadId, polygon: BuildRect())
        {
            DiffIndex = diffIndex,
            IcIndex = icIndex,
        };
}
