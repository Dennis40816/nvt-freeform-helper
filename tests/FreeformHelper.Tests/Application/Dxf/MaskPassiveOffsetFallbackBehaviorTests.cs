// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MaskPassiveOffsetFallbackBehaviorTests
{
    private static readonly int[] _expectedCandidateDiffIndices = [13, 15];

    [Fact]
    public void BuildAuditRows_NoOverlapNearDetectedOffset_FallsBackToCoverageWithoutChangingAssignment()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 12, 13, 15]);
        CadPad[] ordered =
        [
            TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, 1, 1),
            TestGeometryFactory.CreateCadPad(101, "PAD", 1, 0, 2, 1),
            TestGeometryFactory.CreateCadPad(102, "PAD", 2, 0, 3, 1),
            TestGeometryFactory.CreateCadPad(103, "PAD", 3, 0, 4.8, 1),
        ];
        var matches = PadMatcher.Match(new CadPadSet(ordered), grid);
        var assignments = new Dictionary<int, int> { [100] = 20, [101] = 21, [102] = 22, [103] = 16 };

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            ordered, matches.CadToRegular, grid.Pads.ToDictionary(pad => pad.RegularPadId),
            ordered.ToDictionary(pad => pad.Id, _ => 0), new HashSet<int> { 0, 1, 2 }, assignments);

        var blocked = Assert.Single(rows, row => row.CadPadId == 103);
        Assert.Equal(13, blocked.RawSeed.BestMatchFwDiffIndex);
        Assert.Equal(_expectedCandidateDiffIndices, blocked.GeometryCandidates!.Select(candidate => candidate.DiffIndex));
        Assert.Empty(blocked.CsvConfirmedCandidates);
        Assert.Equal(10, blocked.DetectedOffset);
        Assert.Equal(3, blocked.OffsetSupportCount);
        Assert.Equal(0.75d, blocked.OffsetSupportRatio);
        Assert.Equal(16, blocked.PrimaryAssignedDiffIndex);
        Assert.Equal(13, blocked.PassiveCompensationDiffIndex);
        Assert.Null(blocked.SuggestedDiffIndex);
        Assert.Null(blocked.RepairSuggestionDiffIndex);
        Assert.Equal(CadOutputFwDiffAssignmentReasonCode.InactiveBlocked, blocked.ReasonCode);
        Assert.Equal(DxfRegularMaskAuditStatus.MaskRemovedMatch, blocked.Status);
        Assert.Equal(16, assignments[103]);
    }
}
