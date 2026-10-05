using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfRegularMaskAuditSegmentContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssignmentAndAudit_EmptyInputReturnsFreshEmptyDecisions(bool hasCsvMask)
    {
        var ordered = Array.Empty<CadPad>();
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        var regulars = new Dictionary<int, RegularPad>();
        var icIndices = new Dictionary<int, int>();
        IReadOnlySet<int>? activeIds = hasCsvMask ? new HashSet<int>() : null;

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(ordered, links, regulars, icIndices, activeIds);
        var nextDecisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(ordered, links, regulars, icIndices, activeIds);
        var rows = DxfRegularMaskAuditService.BuildAuditRows(ordered, links, regulars, icIndices, activeIds);

        Assert.Empty(decisions);
        Assert.Empty(nextDecisions);
        Assert.NotSame(decisions, nextDecisions);
        Assert.Empty(rows);
    }

    [Fact]
    public void BuildAssignmentDecisions_SeparatesIcRowsAndNoncontiguousSegmentsWithoutReordering()
    {
        var cadIds = new[] { 90, 4, 70, 11, 55, 2, 99, 3 };
        var icIndices = new[] { 1, 1, 0, 0, 1, 1, 1, 1 };
        var rowIndices = new[] { 4, 4, 4, 4, 5, 5, 4, 4 };
        var seedDiffs = new[] { 10, 11, 10, 11, 20, 21, 12, 13 };
        var offsets = new[] { 2, 2, 1, 1, 3, 3, 4, 4 };
        var segmentIndices = new[] { 0, 0, 0, 0, 0, 0, 1, 1 };
        var ordered = cadIds.Select(BuildCad).ToArray();
        var regulars = new Dictionary<int, RegularPad>();
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        var cadIcIndices = new Dictionary<int, int>();
        var assignments = new Dictionary<int, int>();
        for (var index = 0; index < cadIds.Length; index++)
        {
            var cadId = cadIds[index];
            var regularId = 100 + index;
            regulars[regularId] = BuildRegular(regularId, seedDiffs[index], icIndices[index], rowIndices[index]);
            links[cadId] = new[] { new PadMatchLink(cadId, regularId, 1d, 1d, 1d) };
            cadIcIndices[cadId] = icIndices[index];
            assignments[cadId] = seedDiffs[index] + offsets[index];
        }

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, cadIcIndices, activeRegularPadIds: null, currentAssignedDiffByCadId: assignments);

        Assert.Equal(cadIds.Length, decisions.Count);
        for (var index = 0; index < cadIds.Length; index++)
        {
            var decision = decisions[cadIds[index]];
            Assert.Equal(index, decision.OrderedCadIndex);
            Assert.Equal(icIndices[index], decision.IcIndex);
            Assert.Equal(rowIndices[index], decision.RowIndex);
            Assert.Equal(segmentIndices[index], decision.SegmentIndex);
            Assert.Equal(2, decision.SegmentMemberCount);
            Assert.Equal(offsets[index], decision.DetectedOffset);
            Assert.Equal(2, decision.OffsetSupportCount);
            Assert.Equal(1d, decision.OffsetSupportRatio);
            Assert.Equal(1d, decision.SegmentConfidence);
            Assert.Equal(CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected, decision.ReasonCode);
            Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, decision.DecisionSource);
            Assert.Equal(assignments[cadIds[index]], decision.CurrentPrimaryDiffIndex);
        }
    }

    [Theory]
    [InlineData(CadOutputFwDiffAssignmentReasonCode.NeedsReview)]
    [InlineData(CadOutputFwDiffAssignmentReasonCode.InactiveBlocked)]
    [InlineData(CadOutputFwDiffAssignmentReasonCode.DuplicateConflict)]
    [InlineData(CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate)]
    public void BuildAssignmentDecisions_ManualOverrideKeepsSeedReasonAndReceivesOffsetEvidence(
        CadOutputFwDiffAssignmentReasonCode expectedReason)
    {
        var cadIds = new[] { 43, 7, 91 };
        var ordered = cadIds.Select(BuildCad).ToArray();
        var regulars = new Dictionary<int, RegularPad>();
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        var icIndices = new Dictionary<int, int>();
        var assignments = new Dictionary<int, int>();
        var isGap = expectedReason == CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate;
        var isInactive = expectedReason == CadOutputFwDiffAssignmentReasonCode.InactiveBlocked;
        IReadOnlySet<int>? activeIds = isGap ? new HashSet<int> { 200, 201, 202 } : isInactive ? new HashSet<int>() : null;
        for (var index = 0; index < cadIds.Length; index++)
        {
            var cadId = cadIds[index];
            var regularId = 100 + index;
            var seedDiff = expectedReason == CadOutputFwDiffAssignmentReasonCode.DuplicateConflict && index == 2
                ? 10
                : 10 + index;
            regulars[regularId] = BuildRegular(regularId, seedDiff, icIndex: 0, row: 2);
            var candidates = new List<PadMatchLink> { new(cadId, regularId, 1d, 1d, 1d) };
            if (isGap)
            {
                regulars[200 + index] = BuildRegular(200 + index, seedDiff + 1, icIndex: 0, row: 2);
                candidates.Add(new PadMatchLink(cadId, 200 + index, 0.9d, 0.9d, 0.9d));
            }

            links[cadId] = candidates;
            icIndices[cadId] = 0;
            assignments[cadId] = seedDiff + (isGap ? 3 : 2);
        }

        var manualOverrides = new Dictionary<int, int> { [43] = 999 };
        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, icIndices, activeIds, assignments, manualOverrides);

        Assert.Equal(expectedReason, decisions[43].ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.ManualOverride, decisions[43].DecisionSource);
        Assert.Equal(
            isInactive ? expectedReason : CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
            decisions[7].ReasonCode);
        Assert.All(decisions.Values, decision =>
        {
            Assert.Equal(2, decision.DetectedOffset);
            Assert.Equal(3, decision.OffsetSupportCount);
            Assert.Equal(1d, decision.OffsetSupportRatio);
            Assert.Equal(1d, decision.SegmentConfidence);
            Assert.Equal(assignments[decision.CadPadId], decision.CurrentPrimaryDiffIndex);
        });
        Assert.Equal(999, manualOverrides[43]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BuildAuditRows_PartialOrEmptyPrecomputedDecisionsBypassAllStages(bool hasDecision, bool hasCsvMask)
    {
        var ordered = new[] { BuildCad(43), BuildCad(7) };
        var regulars = new Dictionary<int, RegularPad>
        {
            [100] = BuildRegular(100, 10, icIndex: 0, row: 2),
            [101] = BuildRegular(101, 11, icIndex: 0, row: 2),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = new[] { new PadMatchLink(43, 100, 1d, 1d, 1d) },
            [7] = new[] { new PadMatchLink(7, 101, 1d, 1d, 1d) },
        };
        var icIndices = new Dictionary<int, int> { [43] = 0, [7] = 0 };
        var assignments = new Dictionary<int, int> { [43] = 10, [7] = 11 };
        var manualOverrides = new Dictionary<int, int> { [7] = 11 };
        IReadOnlySet<int>? activeIds = hasCsvMask ? new HashSet<int> { 100, 101 } : null;
        var candidates = new List<DxfRegularMaskAuditCandidate> { new(100, 8, 10, 1d, 1d) };
        var seed = new CadBestMatchSeed(100, 8, 10, 1d, 1d);
        var supplied = CadOutputFwDiffAssignmentDecision.Empty(43, 99, CadOutputFwDiffAssignmentMode.GeometryOnly) with
        {
            RawSeed = seed,
            MaskedSeed = seed,
            GeometryCandidates = candidates,
            CsvConfirmedCandidates = candidates,
            CurrentPrimaryDiffIndex = 50,
            PassiveCompensationDiffIndex = 51,
            RepairSuggestionDiffIndex = 52,
            ReasonCode = CadOutputFwDiffAssignmentReasonCode.DuplicateConflict,
            DecisionSource = CadOutputFwDiffAssignmentDecisionSource.PropagatedOverride,
            Confidence = 0.42d,
            DetectedOffset = 40,
            OffsetSupportCount = 2,
            OffsetSupportRatio = 0.75d,
            SegmentConfidence = 0.75d,
            IcIndex = 8,
            RowIndex = 9,
            SegmentIndex = 3,
            SegmentMemberCount = 4,
        };
        var precomputed = new Dictionary<int, CadOutputFwDiffAssignmentDecision>();
        if (hasDecision)
        {
            precomputed[43] = supplied;
        }

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, links, regulars, icIndices, activeIds, assignments, manualOverrides, precomputed);

        var expectedCadIds = new[] { 43, 7 };
        var expectedOrderedIndices = new[] { 0, 1 };
        Assert.Equal(expectedCadIds, rows.Select(row => row.CadPadId));
        Assert.Equal(expectedOrderedIndices, rows.Select(row => row.OrderedCadIndex));
        Assert.Equal(hasDecision ? 1 : 0, precomputed.Count);
        if (hasDecision)
        {
            var row = rows[0];
            Assert.Same(supplied, precomputed[43]);
            Assert.Same(seed, row.RawSeed);
            Assert.Same(seed, row.MaskedSeed);
            Assert.Same(candidates, row.GeometryCandidates);
            Assert.Same(candidates, row.CsvConfirmedCandidates);
            Assert.Equal(CadOutputFwDiffAssignmentMode.GeometryOnly, row.Mode);
            Assert.Equal(CadOutputFwDiffAssignmentReasonCode.DuplicateConflict, row.ReasonCode);
            Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.PropagatedOverride, row.DecisionSource);
            Assert.Equal(50, row.PrimaryAssignedDiffIndex);
            Assert.Equal(51, row.PassiveCompensationDiffIndex);
            Assert.Equal(52, row.RepairSuggestionDiffIndex);
            Assert.Equal(0.42d, row.CsvConfirmedConfidence);
            Assert.Equal(40, row.DetectedOffset);
            Assert.Equal(2, row.OffsetSupportCount);
            Assert.Equal(0.75d, row.OffsetSupportRatio);
            Assert.Equal(0.75d, row.SegmentConfidence);
            Assert.Equal(8, row.IcIndex);
            Assert.Equal(9, row.RowIndex);
            Assert.Equal(3, row.SegmentIndex);
            Assert.Equal(4, row.SegmentMemberCount);
        }

        Assert.All(hasDecision ? rows.Skip(1) : rows, row =>
        {
            Assert.Equal(
                hasCsvMask ? CadOutputFwDiffAssignmentMode.CsvConstrained : CadOutputFwDiffAssignmentMode.GeometryOnly,
                row.Mode);
            Assert.False(row.RawSeed.HasMatch);
            Assert.False(row.MaskedSeed.HasMatch);
            Assert.Empty(row.GeometryCandidates!);
            Assert.Empty(row.CsvConfirmedCandidates);
            Assert.Equal(DxfRegularMaskAuditStatus.NoRawBestMatch, row.Status);
            Assert.Equal(CadOutputFwDiffAssignmentReasonCode.NeedsReview, row.ReasonCode);
            Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, row.DecisionSource);
            Assert.Null(row.CurrentAssignedDiffIndex);
            Assert.Null(row.PrimaryAssignedDiffIndex);
            Assert.Null(row.PassiveCompensationDiffIndex);
            Assert.Null(row.RepairSuggestionDiffIndex);
            Assert.Null(row.DetectedOffset);
            Assert.Null(row.IcIndex);
            Assert.Null(row.RowIndex);
            Assert.Null(row.SegmentIndex);
            Assert.Equal(0, row.SegmentMemberCount);
        });
    }

    private static CadPad BuildCad(int id) => new(id, $"CAD{id}", "L1", BuildRect());

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
