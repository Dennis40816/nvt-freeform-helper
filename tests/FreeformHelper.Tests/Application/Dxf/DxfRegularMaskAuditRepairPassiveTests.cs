using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfRegularMaskAuditRepairPassiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildAssignmentDecisions_RepairAndPassiveIgnoreCandidatesAndOwnersFromAnotherIc(bool hasCsvMask)
    {
        var ordered = new[] { BuildCad(43), BuildCad(7) };
        var regulars = new Dictionary<int, RegularPad>
        {
            [100] = BuildRegular(100, 10, icIndex: 0),
            [101] = BuildRegular(101, 11, icIndex: 0),
            [200] = BuildRegular(200, 9, icIndex: 1),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = new[]
            {
                new PadMatchLink(43, 200, 1d, 1d, 1d),
                new PadMatchLink(43, 100, 1d, 1d, 1d),
                new PadMatchLink(43, 101, 0.9d, 0.9d, 0.9d),
            },
            [7] = new[] { new PadMatchLink(7, 200, 1d, 1d, 1d) },
        };
        var icIndices = new Dictionary<int, int> { [43] = 0, [7] = 1 };
        var assignments = new Dictionary<int, int> { [43] = 12, [7] = 10 };
        IReadOnlySet<int>? activeIds = hasCsvMask ? new HashSet<int>() : null;

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, icIndices, activeIds, assignments);

        var repair = decisions[43];
        var expectedRegularIds = new[] { 100, 101 };
        Assert.Equal(expectedRegularIds, repair.GeometryCandidates.Select(candidate => candidate.RegularPadIndex));
        Assert.All(repair.GeometryCandidates, candidate => Assert.Equal(0, candidate.IcIndex));
        Assert.Equal(10, repair.RawSeed.BestMatchFwDiffIndex);
        Assert.Equal(12, repair.CurrentPrimaryDiffIndex);
        Assert.Equal(hasCsvMask ? null : (int?)10, repair.RepairSuggestionDiffIndex);
        if (hasCsvMask)
        {
            Assert.Empty(repair.CsvConfirmedCandidates);
            Assert.Null(repair.Confidence);
        }
        else
        {
            Assert.Equal(0.84d, repair.Confidence.GetValueOrDefault(), 6);
        }

        Assert.Equal(
            hasCsvMask ? CadOutputFwDiffAssignmentReasonCode.InactiveBlocked : CadOutputFwDiffAssignmentReasonCode.NeedsReview,
            repair.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, repair.DecisionSource);
        Assert.Equal(hasCsvMask ? 10 : (int?)null, repair.PassiveCompensationDiffIndex);
        Assert.Null(repair.DetectedOffset);
        Assert.Equal(10, decisions[7].CurrentPrimaryDiffIndex);
        Assert.Equal(hasCsvMask ? null : (int?)9, decisions[7].RepairSuggestionDiffIndex);
        Assert.Equal(hasCsvMask ? 9 : (int?)null, decisions[7].PassiveCompensationDiffIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildAssignmentDecisions_RepairPenalizesOtherOwnersButExcludesSelf(bool hasDuplicateOwner)
    {
        var ordered = new[] { BuildCad(43), BuildCad(7) };
        var regulars = new Dictionary<int, RegularPad>
        {
            [100] = BuildRegular(100, 10),
            [101] = BuildRegular(101, 11),
            [102] = BuildRegular(102, 10, row: 3),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = new[]
            {
                new PadMatchLink(43, 100, 1d, 1d, 1d),
                new PadMatchLink(43, 101, 0.9d, 0.9d, 0.9d),
            },
            [7] = new[] { new PadMatchLink(7, 102, 1d, 1d, 1d) },
        };
        var icIndices = new Dictionary<int, int> { [43] = 0, [7] = 0 };
        var assignments = new Dictionary<int, int> { [43] = 10, [7] = hasDuplicateOwner ? 10 : 12 };

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, icIndices, activeRegularPadIds: null, currentAssignedDiffByCadId: assignments);

        var repair = decisions[43];
        var expectedDiffs = new[] { 10, 11 };
        Assert.Equal(expectedDiffs, repair.GeometryCandidates.Select(candidate => candidate.DiffIndex));
        Assert.Equal(10, repair.CurrentPrimaryDiffIndex);
        Assert.Equal(hasDuplicateOwner ? 11 : (int?)null, repair.RepairSuggestionDiffIndex);
        Assert.Equal(hasDuplicateOwner ? 0.82d : 1d, repair.Confidence.GetValueOrDefault(), 6);
        Assert.Equal(
            hasDuplicateOwner ? CadOutputFwDiffAssignmentReasonCode.DuplicateConflict : CadOutputFwDiffAssignmentReasonCode.NeedsReview,
            repair.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, repair.DecisionSource);
        Assert.Null(repair.PassiveCompensationDiffIndex);
        Assert.Equal(assignments[7], decisions[7].CurrentPrimaryDiffIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildAssignmentDecisions_RowMismatchChoiceRespectsManualOverride(bool hasManualOverride)
    {
        var ordered = new[] { BuildCad(43), BuildCad(7) };
        var regulars = new Dictionary<int, RegularPad>
        {
            [100] = BuildRegular(100, 10),
            [101] = BuildRegular(101, 9),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = new[]
            {
                new PadMatchLink(43, 100, 1d, 1d, 1d),
                new PadMatchLink(43, 101, 0.1d, 0.1d, 0.1d),
            },
            [7] = new[] { new PadMatchLink(7, 100, 1d, 1d, 1d) },
        };
        var icIndices = new Dictionary<int, int> { [43] = 0, [7] = 0 };
        var assignments = new Dictionary<int, int> { [43] = 10, [7] = 10 };
        var manualOverrides = hasManualOverride ? new Dictionary<int, int> { [43] = 999 } : null;

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, icIndices, new HashSet<int> { 100, 101 }, assignments, manualOverrides);

        var repair = decisions[43];
        var expectedDiffs = new[] { 10, 9 };
        Assert.Equal(expectedDiffs, repair.CsvConfirmedCandidates.Select(candidate => candidate.DiffIndex));
        Assert.Equal(10, repair.CurrentPrimaryDiffIndex);
        Assert.Equal(hasManualOverride ? null : (int?)9, repair.RepairSuggestionDiffIndex);
        Assert.Equal(hasManualOverride ? 0.65d : 0.8d, repair.Confidence.GetValueOrDefault(), 6);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.DuplicateConflict, repair.ReasonCode);
        Assert.Equal(
            hasManualOverride ? CadOutputFwDiffAssignmentDecisionSource.ManualOverride : CadOutputFwDiffAssignmentDecisionSource.Seed,
            repair.DecisionSource);
        Assert.Null(repair.PassiveCompensationDiffIndex);
        Assert.Null(repair.DetectedOffset);
        Assert.Equal(10, decisions[7].CurrentPrimaryDiffIndex);
        Assert.Equal(0.65d, decisions[7].Confidence.GetValueOrDefault(), 6);
    }

    [Fact]
    public void BuildAssignmentDecisions_LowConfidenceRepairKeepsSeedSuggestion()
    {
        var ordered = new[] { BuildCad(43) };
        var regulars = new Dictionary<int, RegularPad>
        {
            [100] = BuildRegular(100, 10),
            [101] = BuildRegular(101, 24),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = new[]
            {
                new PadMatchLink(43, 100, 1d, 1d, 1d),
                new PadMatchLink(43, 101, 1d, 1d, 1d),
            },
        };

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, new Dictionary<int, int> { [43] = 0 }, activeRegularPadIds: null,
            currentAssignedDiffByCadId: new Dictionary<int, int> { [43] = 20 });

        var repair = Assert.Single(decisions).Value;
        var expectedDiffs = new[] { 10, 24 };
        Assert.Equal(expectedDiffs, repair.GeometryCandidates.Select(candidate => candidate.DiffIndex));
        Assert.Equal(20, repair.CurrentPrimaryDiffIndex);
        Assert.Equal(10, repair.RepairSuggestionDiffIndex);
        Assert.Equal(0.68d, repair.Confidence.GetValueOrDefault(), 6);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.NeedsReview, repair.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, repair.DecisionSource);
        Assert.Null(repair.PassiveCompensationDiffIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildAssignmentDecisions_NoSeedSkipsRepairAndPassiveAndKeepsPrimary(bool hasCsvMask)
    {
        var ordered = new[] { BuildCad(43) };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        var regulars = new Dictionary<int, RegularPad>();
        var icIndices = new Dictionary<int, int> { [43] = 0 };
        var assignments = new Dictionary<int, int> { [43] = 42 };
        var manualOverrides = new Dictionary<int, int> { [43] = 999 };
        IReadOnlySet<int>? activeIds = hasCsvMask ? new HashSet<int>() : null;

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, icIndices, activeIds, assignments, manualOverrides);

        var decision = Assert.Single(decisions).Value;
        Assert.Equal(42, decision.CurrentPrimaryDiffIndex);
        Assert.Equal(
            hasCsvMask ? CadOutputFwDiffAssignmentMode.CsvConstrained : CadOutputFwDiffAssignmentMode.GeometryOnly,
            decision.Mode);
        Assert.Empty(decision.GeometryCandidates);
        Assert.Empty(decision.CsvConfirmedCandidates);
        Assert.False(decision.RawSeed.HasMatch);
        Assert.False(decision.MaskedSeed.HasMatch);
        Assert.Null(decision.RepairSuggestionDiffIndex);
        Assert.Null(decision.PassiveCompensationDiffIndex);
        Assert.Null(decision.Confidence);
        Assert.Null(decision.DetectedOffset);
        Assert.Null(decision.IcIndex);
        Assert.Null(decision.RowIndex);
        Assert.Null(decision.SegmentIndex);
        Assert.Equal(0, decision.SegmentMemberCount);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.NeedsReview, decision.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.ManualOverride, decision.DecisionSource);
    }

    [Fact]
    public void BuildAssignmentDecisions_CsvRowWithoutOverlappingCandidatesKeepsPrimaryAndNullSignals()
    {
        var ordered = new[] { BuildCad(43) };
        var regulars = new Dictionary<int, RegularPad> { [100] = BuildRegular(100, 10) };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = new[] { new PadMatchLink(43, 100, 0d, 0d, 0d) },
        };

        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered, links, regulars, new Dictionary<int, int> { [43] = 0 }, new HashSet<int>(),
            currentAssignedDiffByCadId: new Dictionary<int, int> { [43] = 12 });

        var decision = Assert.Single(decisions).Value;
        Assert.Equal(CadOutputFwDiffAssignmentMode.CsvConstrained, decision.Mode);
        Assert.True(decision.RawSeed.HasMatch);
        Assert.False(decision.MaskedSeed.HasMatch);
        Assert.Empty(decision.CsvConfirmedCandidates);

        // The raw seed gives the pad a segment row, so it enters the repair/passive traversal.
        Assert.Equal(2, decision.RowIndex);
        Assert.Equal(12, decision.CurrentPrimaryDiffIndex);
        Assert.Null(decision.RepairSuggestionDiffIndex);
        Assert.Null(decision.PassiveCompensationDiffIndex);
        Assert.Null(decision.Confidence);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.InactiveBlocked, decision.ReasonCode);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSource.Seed, decision.DecisionSource);
    }

    private static CadPad BuildCad(int id) => new(id, $"CAD{id}", "L1", BuildRect());

    private static Polygon2 BuildRect() => new(new[]
    {
        new Point2(0, 0),
        new Point2(1, 0),
        new Point2(1, 1),
        new Point2(0, 1),
    });

    private static RegularPad BuildRegular(int regularPadId, int diffIndex, int icIndex = 0, int row = 2) =>
        new(row, col: 0, index: regularPadId, polygon: BuildRect())
        {
            DiffIndex = diffIndex,
            IcIndex = icIndex,
        };
}
