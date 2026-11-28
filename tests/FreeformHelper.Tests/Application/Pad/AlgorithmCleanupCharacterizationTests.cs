// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AlgorithmCleanupCharacterizationTests
{
    [Theory]
    [InlineData(1, 3, 0.5, 0)]
    [InlineData(2, 2, 2, 2)]
    public void MatcherAndSeedKeepOriginalFirstCellOnExactTies(int rows, int cols, double cadHeight, int expectedDiff)
    {
        var settings = new GridSettings
        {
            XChannels = cols,
            YChannels = rows,
            ActiveAreaWidth = cols,
            ActiveAreaHeight = rows,
        };
        var grid = RegularGridBuilder.BuildFromSettings(settings);
        DiffIndexMapper.ApplyMapping(grid, settings);
        var cad = new CadPadSet(new[] { new CadPad(100, "CAD", "PAD", new Polygon2(new[]
        {
            new Point2(0, 0), new Point2(cols, 0), new Point2(cols, cadHeight), new Point2(0, cadHeight),
        })) });

        var match = PadMatcher.Match(cad, grid);
        var regulars = grid.Pads.ToDictionary(pad => pad.RegularPadId);
        var ic = new Dictionary<int, int> { [100] = 0 };
        var seed = CadBestMatchSeedService.BuildSeedDetailsByCadId(cad.Pads, match.CadToRegular, regulars, ic)[100];
        var audit = Assert.Single(DxfRegularMaskAuditService.BuildAuditRows(cad.Pads, match.CadToRegular, regulars, ic, null));

        Assert.Equal(rows * cols, match.CadToRegular[100].Count);
        Assert.All(match.CadToRegular[100], link =>
        {
            Assert.Equal(match.CadToRegular[100][0].RegularCoverage, link.RegularCoverage);
            Assert.Equal(match.CadToRegular[100][0].OverlapArea, link.OverlapArea);
            Assert.Equal(match.CadToRegular[100][0].CadCoverage, link.CadCoverage);
        });
        Assert.Equal(0, seed.RegularPadIndex);
        Assert.Equal(expectedDiff, seed.BestMatchFwDiffIndex);
        Assert.Equal(seed, audit.RawSeed);
        Assert.Equal(seed, audit.MaskedSeed);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void GridLookupsKeepBoundaryClamping(double x, int expected)
    {
        var pads = new[] { Regular(0, 0, 1), Regular(1, 1, 2) };
        var grid = new RegularGrid(1, 2, new double[] { 0, 1, 2 }, new double[] { 0, 1 }, pads);

        var cell = DxfIndexAssigner.ResolveGridCell(grid, new Point2(x, 0.5));
        var range = RegularGridCandidateQuery.GetCandidateRange(grid, new Rect2(x, 0.5, x, 0.5));

        Assert.Equal(expected, cell.Col);
        Assert.Equal(expected, range.colMin);
        Assert.Equal(expected, range.colMax);
    }

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 8)]
    public void SeedAndAuditKeepFirstEligibleLink(bool maskFirst, int expected)
    {
        var cad = new CadPad(100, "CAD", "PAD", Rect(0, 1));
        var regulars = new Dictionary<int, RegularPad> { [7] = Regular(7, 0, 1), [8] = Regular(8, 1, 2) };
        var links = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [100] = new[] { new PadMatchLink(100, 999, 3, 1, 1), new PadMatchLink(100, 7, 2, 0.8, 0.6), new PadMatchLink(100, 8, 1, 0.7, 0.5) },
        };
        IReadOnlySet<int>? active = maskFirst ? new HashSet<int> { 8 } : null;
        var ic = new Dictionary<int, int> { [100] = 0 };

        var seed = CadBestMatchSeedService.BuildSeedDetailsByCadId(new[] { cad }, links, regulars, ic, active)[100];
        var audit = Assert.Single(DxfRegularMaskAuditService.BuildAuditRows(new[] { cad }, links, regulars, ic, active));

        Assert.Equal(expected, seed.RegularPadIndex);
        Assert.Equal(seed, audit.MaskedSeed);
    }

    [Fact]
    public void GridLookupsKeepTheirDifferentClampsForMismatchedEdgeCounts()
    {
        var pads = Enumerable.Range(0, 10).Select(index => Regular(index, index, index + 1)).ToArray();
        pads[9] = Regular(9, 100, 101);
        var grid = new RegularGrid(1, 10, new double[] { 0, 1, 2 }, new double[] { 0, 1 }, pads);

        var cell = DxfIndexAssigner.ResolveGridCell(grid, new Point2(100.5, 0.5));
        var range = RegularGridCandidateQuery.GetCandidateRange(grid, new Rect2(100.5, 0.5, 100.5, 0.5));

        Assert.Equal(9, cell.Col);
        Assert.Equal(1, range.colMin);
    }

    private static RegularPad Regular(int id, double minX, double maxX) => new(0, id, id, Rect(minX, maxX)) { DiffIndex = id };

    private static Polygon2 Rect(double minX, double maxX) => new(new[]
    {
        new Point2(minX, 0), new Point2(maxX, 0), new Point2(maxX, 1), new Point2(minX, 1),
    });
}
