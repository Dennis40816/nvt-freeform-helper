// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MaskAuditMessageBehaviorTests
{
    [Fact]
    public void BuildAuditRows_MaskLeavesMultipleAlternatives_ReportsChangedSeedAndCandidateCount()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 12]);
        var cad = TestGeometryFactory.CreateCadPad(100, "PAD", 0.25, 0, 2.5, 1);
        var matches = PadMatcher.Match(new CadPadSet([cad]), grid);

        var rows = DxfRegularMaskAuditService.BuildAuditRows(
            [cad], matches.CadToRegular, grid.Pads.ToDictionary(pad => pad.RegularPadId),
            new Dictionary<int, int> { [cad.Id] = 0 }, new HashSet<int> { 0, 2 });

        var row = Assert.Single(rows);
        Assert.Equal(DxfRegularMaskAuditStatus.ChangedByMask, row.Status);
        Assert.Equal(1, row.RawSeed.RegularPadIndex);
        Assert.Equal(11, row.RawSeed.BestMatchFwDiffIndex);
        Assert.Equal(0, row.MaskedSeed.RegularPadIndex);
        Assert.Equal(10, row.MaskedSeed.BestMatchFwDiffIndex);
        int[] expectedActiveIds = [0, 2];
        Assert.Equal(expectedActiveIds, row.CsvConfirmedCandidates.Select(candidate => candidate.RegularPadIndex));
        Assert.Equal(10, row.SuggestedDiffIndex);
        Assert.Equal(
            "Mask changed geometry seed from REG 1 / diff 11 to REG 0 / diff 10. CSV keeps 2 active candidates.",
            row.Message);
    }
}
