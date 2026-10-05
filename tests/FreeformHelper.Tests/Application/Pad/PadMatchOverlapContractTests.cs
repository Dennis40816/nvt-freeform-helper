using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadMatchOverlapContractTests
{
    private static readonly double[] ExpectedBestMatchScores = { 1.0, 1.0, 0.0 };
    private static readonly int[] ExpectedTiedRegularIds = { 30, 20 };
    private static readonly double[] ExpectedReplacementScores = { 0.5, 0.0 };
    private static readonly double[] ExpectedEmptyGridProgress = { 0.0, 1.0 };
    private static readonly double[] ExpectedEmptyCadProgress = { 0, 0.925, 1, 1 };
    private static readonly double[] UnitYEdges = { 0.0, 1.0 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Match_PreservesManyToManyEvidenceAndBestMatch(bool throughUiService)
    {
        var grid = CreateGrid(0, 1, 2, 3);
        var cad = new CadPadSet(new[]
        {
            CreateCad(201, 0, 0, 1.5, 1),
            CreateCad(202, 0.5, 0, 2, 1),
            CreateCad(299, 4, 0, 5, 1),
        });
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = 999;
            pad.MatchScore = 0.25;
            pad.IcIndex = 7;
            pad.DiffIndex = 9;
            pad.Freeform = FreeformType.XWay;
        }

        var progress = new List<double>();
        var result = throughUiService
            ? new PadMatchService().Match(cad, grid, progress.Add)
            : PadMatcher.Match(cad, grid, progress.Add);
        var expected = new[]
        {
            new PadMatchLink(201, 30, 1, 1, 2.0 / 3),
            new PadMatchLink(201, 20, 0.5, 0.5, 1.0 / 3),
            new PadMatchLink(202, 20, 1, 1, 2.0 / 3),
            new PadMatchLink(202, 30, 0.5, 0.5, 1.0 / 3),
        };

        Assert.Equal(expected, result.Links.OrderBy(link => link.CadPadId).ToArray());
        Assert.Equal(2, result.CadToRegular.Count);
        Assert.Equal(new[] { expected[0], expected[1] }, result.CadToRegular[201]);
        Assert.Equal(new[] { expected[2], expected[3] }, result.CadToRegular[202]);
        Assert.Equal(2, result.RegularToCad.Count);
        Assert.Equal(new[] { expected[0], expected[3] }, result.RegularToCad[30]);
        Assert.Equal(new[] { expected[2], expected[1] }, result.RegularToCad[20]);
        Assert.Equal(new PadMatchTelemetry(3, 6, 5, 4, 2, 3), result.Telemetry);
        Assert.Equal(new int?[] { 201, 202, null }, grid.Pads.Select(pad => pad.MatchedCadPadId));
        Assert.Equal(ExpectedBestMatchScores, grid.Pads.Select(pad => pad.MatchScore));
        Assert.All(grid.Pads, pad =>
        {
            Assert.Equal(7, pad.IcIndex);
            Assert.Equal(9, pad.DiffIndex);
            Assert.Equal(FreeformType.XWay, pad.Freeform);
        });
        AssertProgress(new[] { 0, 0.2833333333333333, 0.5666666666666667, 0.85, 0.9, 0.95, 1, 1 }, progress);
    }

    [Fact]
    public void Match_CadLinksPreferRegularCoverageOverLargerOverlapArea()
    {
        var grid = CreateGrid(0, 1, 4);
        var cad = new CadPadSet(new[] { CreateCad(201, 0, 0, 2.5, 1) });

        var result = PadMatcher.Match(cad, grid);

        Assert.Equal(new[]
        {
            new PadMatchLink(201, 30, 1, 1, 0.4),
            new PadMatchLink(201, 20, 1.5, 0.5, 0.6),
        }, result.CadToRegular[201]);
    }

    [Fact]
    public void Match_CadLinksPreferLargerOverlapAreaWhenRegularCoverageTies()
    {
        var grid = CreateGrid(0, 1, 3);
        var cad = new CadPadSet(new[] { CreateCad(201, 0, 0, 3, 1) });

        var result = PadMatcher.Match(cad, grid);

        Assert.Equal(new[]
        {
            new PadMatchLink(201, 20, 2, 1, 2.0 / 3),
            new PadMatchLink(201, 30, 1, 1, 1.0 / 3),
        }, result.CadToRegular[201]);
    }

    [Fact]
    public void Match_RegularLinksPreferCadCoverageWhenCoverageAndAreaTie()
    {
        var grid = CreateGrid(0, 1);
        var cad = new CadPadSet(new[]
        {
            CreateCad(900, -1, 0, 1, 1),
            CreateCad(700, 0, 0, 1, 1),
            CreateCad(100, -3, 0, 1, 1),
        });

        var result = PadMatcher.Match(cad, grid);

        Assert.Equal(new[]
        {
            new PadMatchLink(700, 30, 1, 1, 1),
            new PadMatchLink(900, 30, 1, 1, 0.5),
            new PadMatchLink(100, 30, 1, 1, 0.25),
        }, result.RegularToCad[30]);
        Assert.Equal(700, grid.Pads[0].MatchedCadPadId);
        Assert.Equal(1, grid.Pads[0].MatchScore);
    }

    [Theory]
    [InlineData(900, 100)]
    [InlineData(100, 900)]
    public void Match_ExactTiesPreserveCurrentCandidateOrder(int firstCadId, int secondCadId)
    {
        var grid = CreateGrid(0, 1, 2);
        var cad = new CadPadSet(new[]
        {
            CreateCad(firstCadId, 0, 0, 2, 1),
            CreateCad(secondCadId, 0, 0, 2, 1),
        });

        var result = PadMatcher.Match(cad, grid);

        Assert.All(result.CadToRegular.Values, links =>
            Assert.Equal(ExpectedTiedRegularIds, links.Select(link => link.RegularPadId)));
        Assert.All(result.RegularToCad.Values, links =>
            Assert.Equal(new[] { firstCadId, secondCadId }, links.Select(link => link.CadPadId)));
        Assert.All(grid.Pads, pad =>
        {
            Assert.Equal(firstCadId, pad.MatchedCadPadId);
            Assert.Equal(1, pad.MatchScore);
        });
    }

    [Theory]
    [InlineData(0.0625, 0.0000005, false)]
    [InlineData(0.0625, 0.000001, false)]
    [InlineData(0.0625, 0.000002, true)]
    [InlineData(1, 0.000005, false)]
    [InlineData(1, 0.00001, false)]
    [InlineData(1, 0.00002, true)]
    public void Match_OverlapMustStrictlyExceedAbsoluteAndRegularAreaFloors(
        double regularWidth,
        double overlapArea,
        bool accepted)
    {
        var grid = CreateGrid(0, regularWidth);
        var cad = new CadPadSet(new[] { CreateCad(201, 0, 0, overlapArea, 1) });
        grid.Pads[0].MatchedCadPadId = 999;
        grid.Pads[0].MatchScore = 1;

        var result = PadMatcher.Match(cad, grid);

        Assert.Equal(new PadMatchTelemetry(1, 1, 1, accepted ? 1 : 0, 1, 1), result.Telemetry);
        if (accepted)
        {
            var expected = new PadMatchLink(201, 30, overlapArea, overlapArea / regularWidth, 1);
            Assert.Equal(expected, Assert.Single(result.Links));
            Assert.Equal(expected, Assert.Single(result.CadToRegular[201]));
            Assert.Equal(expected, Assert.Single(result.RegularToCad[30]));
            Assert.Equal(201, grid.Pads[0].MatchedCadPadId);
            Assert.Equal(expected.RegularCoverage, grid.Pads[0].MatchScore);
        }
        else
        {
            Assert.Empty(result.Links);
            Assert.Empty(result.CadToRegular);
            Assert.Empty(result.RegularToCad);
            Assert.Null(grid.Pads[0].MatchedCadPadId);
            Assert.Equal(0, grid.Pads[0].MatchScore);
        }
    }

    [Fact]
    public void Match_TelemetryDistinguishesCandidatesBoundsAndEffectiveOverlap()
    {
        var grid = CreateGrid(0, 1);
        var cad = new CadPadSet(new[]
        {
            CreateCad(201, 1, 0, 2, 1), // Boundary touch.
            CreateCad(202, 2, 0, 3, 1), // Disjoint bounds.
            new CadPad(203, "Triangle", "L1", new Polygon2(new[]
            {
                new Point2(0.75, 1.5),
                new Point2(1.5, 0.75),
                new Point2(1.5, 1.5),
            })), // Intersecting bounds without polygon overlap.
        });

        var result = PadMatcher.Match(cad, grid);

        Assert.Empty(result.Links);
        Assert.Empty(result.CadToRegular);
        Assert.Empty(result.RegularToCad);
        Assert.Equal(new PadMatchTelemetry(3, 3, 2, 0, 1, 1), result.Telemetry);
        Assert.Null(grid.Pads[0].MatchedCadPadId);
        Assert.Equal(0, grid.Pads[0].MatchScore);
    }

    [Fact]
    public void Match_TelemetryUsesAllCadPadsForAverageAndNearestRankP95()
    {
        var grid = CreateGrid(0, 1, 2, 3);
        var pads = Enumerable.Range(1, 20).Select(id => CreateCad(id, 0.125, 0, 0.625, 1)).ToList();
        pads.Add(CreateCad(99, 0.25, 0, 2.75, 1));

        var result = PadMatcher.Match(new CadPadSet(pads), grid);

        Assert.Equal(23, result.Links.Count);
        Assert.Equal(new PadMatchTelemetry(21, 23, 23, 23, 23.0 / 21, 1), result.Telemetry);
    }

    [Fact]
    public void Match_ReplacesPreviousBestMatchAndClearsUnmatchedRegularPads()
    {
        var grid = CreateGrid(0, 1, 2);
        var previous = PadMatcher.Match(
            new CadPadSet(new[] { CreateCad(201, 0, 0, 2, 1) }), grid);

        var current = PadMatcher.Match(
            new CadPadSet(new[] { CreateCad(202, 0.25, 0, 0.75, 1) }), grid);

        Assert.Equal(2, previous.Links.Count);
        Assert.Equal(201, previous.RegularToCad[30][0].CadPadId);
        Assert.Equal(new PadMatchLink(202, 30, 0.5, 0.5, 1), Assert.Single(current.Links));
        Assert.Equal(new int?[] { 202, null }, grid.Pads.Select(pad => pad.MatchedCadPadId));
        Assert.Equal(ExpectedReplacementScores, grid.Pads.Select(pad => pad.MatchScore));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Match_EmptyCadClearsMatchesAndCompletesProgress(bool emptyGrid)
    {
        var grid = emptyGrid
            ? new RegularGrid(0, 0, Array.Empty<double>(), Array.Empty<double>(), Array.Empty<RegularPad>())
            : CreateGrid(0, 1, 2);
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = 999;
            pad.MatchScore = 1;
        }
        var progress = new List<double>();

        var result = PadMatcher.Match(new CadPadSet(Array.Empty<CadPad>()), grid, progress.Add);

        Assert.Empty(result.Links);
        Assert.Empty(result.CadToRegular);
        Assert.Empty(result.RegularToCad);
        Assert.Equal(PadMatchTelemetry.Empty, result.Telemetry);
        Assert.All(grid.Pads, pad =>
        {
            Assert.Null(pad.MatchedCadPadId);
            Assert.Equal(0, pad.MatchScore);
        });
        AssertProgress(emptyGrid ? ExpectedEmptyGridProgress : ExpectedEmptyCadProgress, progress);
    }

    [Fact]
    public void Match_ProgressKeepsBothPhaseIntervalsAndDuplicateCompletion()
    {
        var grid = new RegularGridBuilder().BuildFromSettings(new GridSettings
        {
            XChannels = 250,
            YChannels = 1,
            ActiveAreaWidth = 250,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        });
        var cad = new CadPadSet(grid.Pads.Select(pad => new CadPad(pad.Index, "CAD", "L1", pad.Polygon)).ToArray());
        var progress = new List<double>();

        _ = PadMatcher.Match(cad, grid, progress.Add);

        Assert.Equal(252, progress.Count);
        Assert.Equal(0, progress[0]);
        Assert.Equal(0.0068, progress[1], precision: 12);
        Assert.Equal(0.85, progress[125], precision: 12);
        Assert.Equal(0.8512, progress[126], precision: 12);
        Assert.Equal(1, progress[250]);
        Assert.Equal(1, progress[251]);
        Assert.All(progress.Zip(progress.Skip(1)), pair => Assert.True(pair.First <= pair.Second));
    }

    private static RegularGrid CreateGrid(params double[] xEdges)
    {
        var pads = Enumerable.Range(0, xEdges.Length - 1)
            .Select(col => new RegularPad(0, col, 30 - (10 * col), CreateRectangle(xEdges[col], 0, xEdges[col + 1], 1)))
            .ToArray();
        return new RegularGrid(1, pads.Length, xEdges, UnitYEdges, pads);
    }

    private static CadPad CreateCad(int id, double minX, double minY, double maxX, double maxY)
    {
        return new CadPad(id, $"CAD{id}", "L1", CreateRectangle(minX, minY, maxX, maxY));
    }

    private static Polygon2 CreateRectangle(double minX, double minY, double maxX, double maxY)
    {
        return new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
    }

    private static void AssertProgress(double[] expected, List<double> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], precision: 12);
        }
    }
}
