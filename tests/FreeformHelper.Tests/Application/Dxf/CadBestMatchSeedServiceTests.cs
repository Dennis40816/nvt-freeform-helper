using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadBestMatchSeedServiceTests
{
    [Fact]
    public void BuildSeedByCadId_SkipsInactiveRegularAndFallsBackToNextBestActiveRegular()
    {
        var cad = new CadPad(101, "CAD101", "L1", BuildRect(0, 0, 1, 1));
        var ordered = new[] { cad };
        var regularById = new Dictionary<int, RegularPad>
        {
            [11] = BuildRegular(11, diffIndex: 11, icIndex: 0),
            [22] = BuildRegular(22, diffIndex: 22, icIndex: 0),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [101] = new[]
            {
                new PadMatchLink(101, 11, 10d, 0.9d, 0.9d),
                new PadMatchLink(101, 22, 9d, 0.8d, 0.8d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [101] = 0,
        };

        var result = CadBestMatchSeedService.BuildSeedByCadId(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: new HashSet<int> { 22 });

        Assert.Equal(22, result[101]);
    }

    [Fact]
    public void BuildSeedByCadId_ReturnsNullWhenAllMatchedRegularsAreMaskedOut()
    {
        var cad = new CadPad(101, "CAD101", "L1", BuildRect(0, 0, 1, 1));
        var ordered = new[] { cad };
        var regularById = new Dictionary<int, RegularPad>
        {
            [11] = BuildRegular(11, diffIndex: 11, icIndex: 0),
        };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [101] = new[]
            {
                new PadMatchLink(101, 11, 10d, 0.9d, 0.9d),
            },
        };
        var cadIcById = new Dictionary<int, int>
        {
            [101] = 0,
        };

        var result = CadBestMatchSeedService.BuildSeedByCadId(
            ordered,
            cadToRegular,
            regularById,
            cadIcById,
            activeRegularPadIds: new HashSet<int>());

        Assert.Null(result[101]);
    }

    private static Polygon2 BuildRect(double minX, double minY, double maxX, double maxY)
    {
        return new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
    }

    private static RegularPad BuildRegular(int regularPadId, int diffIndex, int icIndex)
    {
        return new RegularPad(
            row: 0,
            col: 0,
            index: regularPadId,
            polygon: BuildRect(0, 0, 1, 1))
        {
            DiffIndex = diffIndex,
            IcIndex = icIndex,
        };
    }
}
