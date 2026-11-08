// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MaskAuditCallerBehaviorTests
{
    [Fact]
    public void BuildAuditRows_StaleLinkWithoutCadIc_RetainsRemainingGeometrySuggestion()
    {
        using var culture = TestCulture.UseEnUs();
        var cad = TestGeometryFactory.CreateCadPad(43, "signal", 0, 0, 1, 1);
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 100, 0, 0, 1, 1, diffIndex: 7, icIndex: 2);
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = [new(43, 999, 1d, 1d, 1d), new(43, 100, 0.8d, 0.8d, 0.8d)],
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            [cad], links, new Dictionary<int, RegularPad> { [100] = regular },
            new Dictionary<int, int>(), activeRegularPadIds: null);

        var row = Assert.Single(rows);
        var candidate = Assert.Single(row.GeometryCandidates!);
        Assert.Equal(100, candidate.RegularPadIndex);
        Assert.Equal(2, candidate.IcIndex);
        Assert.Equal(100, row.RawSeed.RegularPadIndex);
        Assert.Equal(7, row.MaskedSeed.BestMatchFwDiffIndex);
        Assert.Equal(7, row.SuggestedDiffIndex);
        Assert.Equal(7, row.RepairSuggestionDiffIndex);
        Assert.Equal(DxfRegularMaskAuditStatus.Unchanged, row.Status);
        Assert.Equal("geometry-only-suggestion", row.ReasonCode.ToContractString());
        Assert.Equal("Mask kept geometry seed at REG 100 / diff 7. CSV confirms a single active candidate.", row.Message);
    }

    [Fact]
    public void BuildAuditRows_CsvMask_ExposesBlockedAndDuplicateReasonContracts()
    {
        using var culture = TestCulture.UseEnUs();
        var ordered = new[]
        {
            TestGeometryFactory.CreateCadPad(43, "signal", 0, 0, 1, 1),
            TestGeometryFactory.CreateCadPad(7, "signal", 0, 2, 1, 3),
            TestGeometryFactory.CreateCadPad(91, "signal", 0, 4, 1, 5),
        };
        var regulars = new Dictionary<int, RegularPad>
        {
            [100] = TestGeometryFactory.CreateRegularPad(0, 0, 100, 0, 0, 1, 1, diffIndex: 10),
            [101] = TestGeometryFactory.CreateRegularPad(1, 0, 101, 0, 2, 1, 3, diffIndex: 11),
            [102] = TestGeometryFactory.CreateRegularPad(2, 0, 102, 0, 4, 1, 5, diffIndex: 20),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [43] = [new(43, 100, 1d, 1d, 1d)],
            [7] = [new(7, 101, 1d, 1d, 1d)],
            [91] = [new(91, 102, 1d, 1d, 1d)],
        };
        var assignments = new Dictionary<int, int> { [43] = 10, [7] = 10, [91] = 20 };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, links, regulars, new Dictionary<int, int> { [43] = 0, [7] = 0, [91] = 0 },
            new HashSet<int> { 100, 101 }, assignments);

        string[] expectedReasons = ["duplicate-conflict", "duplicate-conflict", "inactive-blocked"];
        Assert.Equal(expectedReasons, rows.Select(row => row.ReasonCode.ToContractString()));
        Assert.Equal(new int?[] { 10, 10, 20 }, rows.Select(row => row.PrimaryAssignedDiffIndex));
        var blocked = Assert.Single(rows, row => row.CadPadId == 91);
        Assert.Equal(DxfRegularMaskAuditStatus.MaskRemovedMatch, blocked.Status);
        Assert.Empty(blocked.CsvConfirmedCandidates);
        Assert.Null(blocked.SuggestedDiffIndex);
        Assert.Equal(20, blocked.PassiveCompensationDiffIndex);
        Assert.Equal(
            "Raw geometry seed REG 102 / diff 20 is inactive in Regular Visibility Mask (SeeRegular.csv). " +
            "No CSV-active candidate remains.", blocked.Message);
    }

    [Fact]
    public void BuildAuditRows_PropagatedAssignment_PreservesThePublicSourceContract()
    {
        using var culture = TestCulture.UseEnUs();
        var cad = TestGeometryFactory.CreateCadPad(43, "signal", 0, 0, 1, 1);
        var seed = new CadBestMatchSeed(100, 0, 10, 1d, 1d);
        DxfRegularMaskAuditCandidate[] candidates = [new(100, 0, 10, 1d, 1d)];
        var propagated = CadOutputFwDiffAssignmentDecision.Empty(43, 0, CadOutputFwDiffAssignmentMode.GeometryOnly) with
        {
            RawSeed = seed,
            MaskedSeed = seed,
            GeometryCandidates = candidates,
            CsvConfirmedCandidates = candidates,
            CurrentPrimaryDiffIndex = 12,
            DecisionSource = CadOutputFwDiffAssignmentDecisionSource.PropagatedOverride,
        };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            [cad], new Dictionary<int, IReadOnlyList<PadMatchLink>>(), new Dictionary<int, RegularPad>(),
            new Dictionary<int, int>(), activeRegularPadIds: null,
            precomputedDecisionByCadId: new Dictionary<int, CadOutputFwDiffAssignmentDecision> { [43] = propagated });

        var row = Assert.Single(rows);
        Assert.Equal("propagated-override", row.DecisionSource.ToContractString());
        Assert.Equal(12, row.CurrentAssignedDiffIndex);
        Assert.Equal(12, row.PrimaryAssignedDiffIndex);
        Assert.Equal(10, row.SuggestedDiffIndex);
    }
}
