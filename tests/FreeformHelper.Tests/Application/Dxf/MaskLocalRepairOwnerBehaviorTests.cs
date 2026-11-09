// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MaskLocalRepairOwnerBehaviorTests
{
    [Theory]
    [InlineData(false, 10, 0.84d)]
    [InlineData(true, 11, 0.82d)]
    public void BuildAuditRows_AssignedCadWithoutGeometry_ReservesDiffOnlyWhenItsIcIsKnown(
        bool hasOwnerIc, int expectedRepairDiff, double expectedConfidence)
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        CadPad[] ordered =
        [
            TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, 1.9, 1),
            TestGeometryFactory.CreateCadPad(101, "PAD", 4, 0, 5, 1),
        ];
        var matches = PadMatcher.Match(new CadPadSet(ordered), grid);
        var icIndices = new Dictionary<int, int> { [100] = 0 };
        if (hasOwnerIc)
        {
            icIndices[101] = 0;
        }

        var assignments = new Dictionary<int, int> { [100] = 12, [101] = 10 };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, matches.CadToRegular, grid.Pads.ToDictionary(pad => pad.RegularPadId), icIndices,
            activeRegularPadIds: null, currentAssignedDiffByCadId: assignments);

        Assert.Equal(2, rows.Count);
        var repair = Assert.Single(rows, row => row.CadPadId == 100);
        Assert.Equal(10, repair.RawSeed.BestMatchFwDiffIndex);
        Assert.Equal(12, repair.PrimaryAssignedDiffIndex);
        Assert.Equal(expectedRepairDiff, repair.RepairSuggestionDiffIndex);
        Assert.Equal(expectedConfidence, repair.CsvConfirmedConfidence.GetValueOrDefault(), 6);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.NeedsReview, repair.ReasonCode);
        Assert.Null(repair.PassiveCompensationDiffIndex);
        var unmatched = Assert.Single(rows, row => row.CadPadId == 101);
        Assert.False(unmatched.RawSeed.HasMatch);
        Assert.Empty(unmatched.GeometryCandidates!);
        Assert.Equal(DxfRegularMaskAuditStatus.NoRawBestMatch, unmatched.Status);
        Assert.Equal(10, unmatched.PrimaryAssignedDiffIndex);
        Assert.Null(unmatched.RepairSuggestionDiffIndex);
        Assert.Null(unmatched.IcIndex);
        Assert.Equal(12, assignments[100]);
        Assert.Equal(10, assignments[101]);
    }
}
