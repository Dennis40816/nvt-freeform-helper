using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests.Application.Dxf;

public sealed class CadOutputFwDiffIndexDuplicateReviewServiceTests
{
    [Fact]
    public void BuildGroups_ReturnsDuplicateGroups_WithRawAndMaskedSuggestions()
    {
        var cadPads = new[]
        {
            BuildCadPad(1001, "SIG", 0, 0, 10, 10),
            BuildCadPad(1002, "SIG", 10, 0, 20, 10),
            BuildCadPad(1003, "SIG", 20, 0, 30, 10),
        };

        var currentDiffByCadId = new Dictionary<int, int>
        {
            [1001] = 1541,
            [1002] = 1541,
            [1003] = 1542,
        };
        var cadIcIndexByCadId = new Dictionary<int, int>
        {
            [1001] = 0,
            [1002] = 0,
            [1003] = 0,
        };
        var rawSeeds = new Dictionary<int, CadBestMatchSeed>
        {
            [1001] = new(3101, 0, 1541, 0.82, 0.76),
            [1002] = new(3102, 0, 1542, 0.77, 0.71),
            [1003] = new(3103, 0, 1543, 0.73, 0.68),
        };
        var maskedSeeds = new Dictionary<int, CadBestMatchSeed>
        {
            [1001] = new(3101, 0, 1541, 0.82, 0.76),
            [1002] = new(3104, 0, 1544, 0.55, 0.49),
            [1003] = new(3103, 0, 1543, 0.73, 0.68),
        };

        var groups = CadOutputFwDiffIndexDuplicateReviewService.BuildGroups(
            cadPads,
            currentDiffByCadId,
            cadIcIndexByCadId,
            rawSeeds,
            maskedSeeds);

        var group = Assert.Single(groups);
        Assert.Equal(0, group.IcIndex);
        Assert.Equal(1541, group.DiffIndex);
        Assert.Collection(
            group.Entries,
            first =>
            {
                Assert.Equal(1001, first.CadPadId);
                Assert.Equal(1541, first.CurrentDiffIndex);
                Assert.Equal(1541, first.RawBestDiffIndex);
                Assert.Equal(1541, first.MaskedBestDiffIndex);
            },
            second =>
            {
                Assert.Equal(1002, second.CadPadId);
                Assert.Equal(1541, second.CurrentDiffIndex);
                Assert.Equal(1542, second.RawBestDiffIndex);
                Assert.Equal(1544, second.MaskedBestDiffIndex);
            });
    }

    private static CadPad BuildCadPad(int id, string layer, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
        return new CadPad(
            id,
            $"CAD{id}",
            layer,
            polygon);
    }
}
