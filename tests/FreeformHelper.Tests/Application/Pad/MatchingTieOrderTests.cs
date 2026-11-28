// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MatchingTieOrderTests
{
    [Theory]
    [InlineData(9, 3)]
    [InlineData(3, 9)]
    public void MatcherKeepsOldPickForTwoIdenticalCadPads(int firstId, int secondId)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1);
        var cad = new CadPadSet([Cad(firstId, 0, 1), Cad(secondId, 0, 1)]);

        var result = PadMatcher.Match(cad, grid);

        Assert.Equal(firstId, grid.Pads[0].MatchedCadPadId);
        Assert.Equal(firstId, result.RegularToCad[0][0].CadPadId);
    }

    [Fact]
    public void MatcherKeepsOldPickAfterCadGeometryEditAndPhysicalOrdering()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1);
        var baseline = new CadPadSet([Cad(3, 0, 2), Cad(9, 2, 4)]);
        var edited = CadEditTransforms.ApplyGeometryOverrides(baseline,
            id => id == 9 ? TestGeometryFactory.CreateRect(-1, 0, 1, 1) : null);
        var cad = new CadPadSet(DxfIndexAssigner.OrderPads(edited.Pads, grid, new GridSettings().ScanOrder));

        var result = PadMatcher.Match(cad, grid);

        Assert.Equal(9, cad.Pads[0].Id);
        Assert.Equal(3, cad.Pads[1].Id);
        Assert.Equal(9, grid.Pads[0].MatchedCadPadId);
        Assert.Equal(9, result.RegularToCad[0][0].CadPadId);
    }

    [Fact]
    public void ForwardExactTiesUseRegularIdBeyondInsertionSortSize()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 32);
        var result = PadMatcher.Match(new CadPadSet([Cad(10, 0, 32)]), grid);

        Assert.Equal(Enumerable.Range(0, 32), result.CadToRegular[10].Select(link => link.RegularPadId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SeedTiesKeepFirstLinkRegardlessOfCentroidDistance(double cadMinX)
    {
        var cad = Cad(10, cadMinX, cadMinX + 1);
        var regulars = new Dictionary<int, RegularPad>
        {
            [1] = TestGeometryFactory.CreateRegularPad(0, 1, 1, 1, 0, 2, 1, 1),
            [2] = TestGeometryFactory.CreateRegularPad(0, 0, 2, 0, 0, 1, 1, 2),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [10] = new[] { new PadMatchLink(10, 1, 1, 1, 1), new PadMatchLink(10, 2, 1, 1, 1) },
        };
        var ic = new Dictionary<int, int> { [10] = 0 };

        var seed = CadBestMatchSeedService.BuildSeedDetailsByCadId(new[] { cad }, links, regulars, ic)[10];
        var audit = Assert.Single(DxfRegularMaskAuditService.BuildAuditRows(new[] { cad }, links, regulars, ic, null));

        Assert.Equal(1, seed.RegularPadIndex);
        Assert.Equal(seed, audit.RawSeed);
    }

    [Fact]
    public void SeedEqualDistancesKeepFirstLink()
    {
        var cad = Cad(10, 0.5, 1.5);
        var regulars = new Dictionary<int, RegularPad>
        {
            [8] = TestGeometryFactory.CreateRegularPad(0, 0, 8, 0, 0, 1, 1, 8),
            [3] = TestGeometryFactory.CreateRegularPad(0, 1, 3, 1, 0, 2, 1, 3),
        };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [10] = new[] { new PadMatchLink(10, 8, 1, 1, 1), new PadMatchLink(10, 3, 1, 1, 1) },
        };

        var seed = CadBestMatchSeedService.BuildSeedByCadId(new[] { cad }, links, regulars, new Dictionary<int, int>())[10];

        Assert.Equal(8, seed);
    }

    [Fact]
    public void AnalyzerScoreTiesChooseLowerCadId()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1);
        var cad = new CadPadSet([Cad(9, 0, 1), Cad(3, 0, 1)]);

        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, new IndexMappingSettings());

        Assert.Equal(3, Assert.Single(result.Pairs).CadPadId);
    }

    private static CadPad Cad(int id, double minX, double maxX) => new(id, "CAD", "PAD", TestGeometryFactory.CreateRect(minX, 0, maxX, 1));
}
