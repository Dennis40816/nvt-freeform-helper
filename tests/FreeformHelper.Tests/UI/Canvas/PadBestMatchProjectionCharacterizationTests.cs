using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadBestMatchProjectionCharacterizationTests
{
    [AvaloniaFact]
    public void ThreeLinks_SeedKeepsSuppliedOrderWhileCadHoverUsesCadCoverageThenArea()
    {
        var regularPads = new[] { CreateRegular(11), CreateRegular(22), CreateRegular(33) };
        // Synthetic evidence is already ordered by regular coverage; no matcher or shared policy is invoked.
        var links = new[]
        {
            new PadMatchLink(101, 11, 9d, RegularCoverage: 0.9d, CadCoverage: 0.2d),
            new PadMatchLink(101, 22, 7d, 0.8d, 0.8d),
            new PadMatchLink(101, 33, 8d, RegularCoverage: 0.7d, CadCoverage: 0.8d),
        };
        var canvas = new PadCanvas { RegularPads = regularPads, PadMatchLinks = links };

        Assert.Equal(new CadBestMatchSeed(11, 0, 1011, 0.9d, 0.2d), BuildSeed(regularPads, links));
        Assert.Same(regularPads[2], ResolveBestRegularByCad(canvas, 101));
    }

    [AvaloniaFact]
    public void CompletelyTiedLinks_SeedAndCadHoverKeepFirstInEitherInputOrder()
    {
        var regularPads = new[] { CreateRegular(11), CreateRegular(33) };
        foreach (var ids in new[] { new[] { 33, 11 }, new[] { 11, 33 } })
        {
            var links = ids.Select(id => new PadMatchLink(101, id, 8d, 0.8d, 0.8d)).ToArray();
            var canvas = new PadCanvas { RegularPads = regularPads, PadMatchLinks = links };

            Assert.Equal(new CadBestMatchSeed(ids[0], 0, ids[0] + 1000, 0.8d, 0.8d), BuildSeed(regularPads, links));
            Assert.Same(regularPads.Single(pad => pad.RegularPadId == ids[0]), ResolveBestRegularByCad(canvas, 101));
        }
    }

    [AvaloniaFact]
    public void ActiveMask_OnlyFiltersSeedWhileCadHoverKeepsBestAvailablePad()
    {
        var regularPads = new[] { CreateRegular(11), CreateRegular(22) };
        var links = new[]
        {
            new PadMatchLink(101, 11, 9d, 0.9d, 0.9d),
            new PadMatchLink(101, 22, 8d, 0.8d, 0.8d),
        };
        var canvas = new PadCanvas { RegularPads = regularPads, PadMatchLinks = links };

        Assert.Equal(new CadBestMatchSeed(11, 0, 1011, 0.9d, 0.9d), BuildSeed(regularPads, links));
        Assert.Same(regularPads[0], ResolveBestRegularByCad(canvas, 101));

        // The seed accepts a separate active mask; hover only receives the available pads and links.
        Assert.Equal(
            new CadBestMatchSeed(22, 0, 1022, 0.8d, 0.8d),
            BuildSeed(regularPads, links, activeRegularPadIds: new HashSet<int> { 22 }));
        Assert.Same(regularPads[0], ResolveBestRegularByCad(canvas, 101));

        Assert.Equal(CadBestMatchSeed.Empty, BuildSeed(regularPads, links, activeRegularPadIds: new HashSet<int>()));
        Assert.Same(regularPads[0], ResolveBestRegularByCad(canvas, 101));
    }

    [AvaloniaFact]
    public void MissingBestRegular_SeedFallsBackWhileCadHoverReturnsNull()
    {
        var regularPads = new[] { CreateRegular(22) };
        var links = new[]
        {
            new PadMatchLink(101, 11, 9d, 0.9d, 0.9d),
            new PadMatchLink(101, 22, 8d, 0.8d, 0.8d),
        };
        var canvas = new PadCanvas { RegularPads = regularPads, PadMatchLinks = links };

        Assert.Equal(new CadBestMatchSeed(22, 0, 1022, 0.8d, 0.8d), BuildSeed(regularPads, links));
        Assert.Null(ResolveBestRegularByCad(canvas, 101));
    }

    [AvaloniaFact]
    public void CrossIc_OnlyFiltersSeedWhenCadIcIsKnown()
    {
        var regularPads = new[] { CreateRegular(11, icIndex: 1), CreateRegular(22) };
        var links = new[]
        {
            new PadMatchLink(101, 11, 9d, 0.9d, 0.9d),
            new PadMatchLink(101, 22, 8d, 0.8d, 0.8d),
        };
        var canvas = new PadCanvas { RegularPads = regularPads, PadMatchLinks = links };

        Assert.Equal(new CadBestMatchSeed(22, 0, 1022, 0.8d, 0.8d), BuildSeed(regularPads, links));
        Assert.Same(regularPads[0], ResolveBestRegularByCad(canvas, 101));

        Assert.Equal(
            new CadBestMatchSeed(11, 1, 1011, 0.9d, 0.9d),
            BuildSeed(regularPads, links, cadIcIndexByCadId: new Dictionary<int, int>()));
        Assert.Same(regularPads[0], ResolveBestRegularByCad(canvas, 101));
    }

    [AvaloniaFact]
    public void ReverseHover_UsesRegularCoverageThenAreaWithoutRequiringCadPads()
    {
        var links = new[]
        {
            new PadMatchLink(301, 11, 9d, RegularCoverage: 0.2d, CadCoverage: 0.9d),
            new PadMatchLink(201, 11, 7d, 0.8d, 0.8d),
            new PadMatchLink(101, 11, 8d, RegularCoverage: 0.8d, CadCoverage: 0.7d),
        };
        var canvas = new PadCanvas { RegularPads = new[] { CreateRegular(11) }, PadMatchLinks = links };

        Assert.Equal(101, ResolveBestCadIdByRegular(canvas, 11));
    }

    [AvaloniaFact]
    public void ReverseHover_CompletelyTiedLinksKeepFirstInEitherInputOrder()
    {
        foreach (var ids in new[] { new[] { 301, 101 }, new[] { 101, 301 } })
        {
            var links = ids.Select(id => new PadMatchLink(id, 11, 8d, 0.8d, 0.8d)).ToArray();
            var canvas = new PadCanvas { RegularPads = new[] { CreateRegular(11) }, PadMatchLinks = links };

            Assert.Equal(ids[0], ResolveBestCadIdByRegular(canvas, 11));
        }
    }

    [AvaloniaFact]
    public void NoLinks_SeedAndBothHoverDirectionsHaveNoMatch()
    {
        var regularPads = new[] { CreateRegular(11) };
        var links = Array.Empty<PadMatchLink>();
        var canvas = new PadCanvas { RegularPads = regularPads, PadMatchLinks = links };

        Assert.Equal(CadBestMatchSeed.Empty, BuildSeed(regularPads, links));
        Assert.Null(ResolveBestRegularByCad(canvas, 101));
        Assert.Null(ResolveBestCadIdByRegular(canvas, 11));
    }

    private static CadBestMatchSeed BuildSeed(
        IReadOnlyList<RegularPad> regularPads,
        IReadOnlyList<PadMatchLink> links,
        IReadOnlySet<int>? activeRegularPadIds = null,
        IReadOnlyDictionary<int, int>? cadIcIndexByCadId = null)
    {
        var cad = new CadPad(101, "CAD101", "L1", CreateRect());
        return CadBestMatchSeedService.BuildSeedDetailsByCadId(
            new[] { cad },
            new Dictionary<int, IReadOnlyList<PadMatchLink>> { [101] = links },
            regularPads.ToDictionary(pad => pad.RegularPadId),
            cadIcIndexByCadId ?? new Dictionary<int, int> { [101] = 0 },
            activeRegularPadIds)[101];
    }

    private static RegularPad? ResolveBestRegularByCad(PadCanvas canvas, int cadPadId)
    {
        var method = typeof(PadCanvas).GetMethod("ResolveBestRegularByCad", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(canvas, new object[] { cadPadId });
        return result is null ? null : Assert.IsType<RegularPad>(result);
    }

    private static int? ResolveBestCadIdByRegular(PadCanvas canvas, int regularPadId)
    {
        var method = typeof(PadCanvas).GetMethod("ResolveBestCadIdByRegular", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(canvas, new object[] { regularPadId });
        return result is null ? null : Assert.IsType<int>(result);
    }

    private static RegularPad CreateRegular(int id, int icIndex = 0)
    {
        return new RegularPad(row: 0, col: 0, index: id, polygon: CreateRect())
        {
            DiffIndex = id + 1000,
            IcIndex = icIndex,
        };
    }

    private static Polygon2 CreateRect()
    {
        return new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(1, 0),
            new Point2(1, 1),
            new Point2(0, 1),
        });
    }
}
