using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadAreaBucketServiceTests
{
    private static readonly int[] ExpectedBucketIds1 = [1, 2];
    private static readonly int[] ExpectedBucketIds2 = [3, 4];

    [Fact]
    public void BuildBuckets_GroupsPadsByRelativeTolerance()
    {
        _ = new CadAreaBucketService();
        var pads = new List<CadPad>
        {
            CreateSquarePad(1, 100.00),
            CreateSquarePad(2, 100.05),
            CreateSquarePad(3, 120.00),
            CreateSquarePad(4, 120.08),
        };

        var buckets = CadAreaBucketService.BuildBuckets(pads, 0.001);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(ExpectedBucketIds1, buckets[0]);
        Assert.Equal(ExpectedBucketIds2, buckets[1]);
    }

    [Fact]
    public void ResolveBucketMembers_ReturnsEmptyWhenAnchorMissing()
    {
        var service = new CadAreaBucketService();
        var pads = new List<CadPad>
        {
            CreateSquarePad(10, 80.0),
            CreateSquarePad(11, 80.01),
        };

        var bucket = service.ResolveBucketMembers(pads, 0.01, 999);

        Assert.Empty(bucket);
    }

    private static CadPad CreateSquarePad(int id, double area)
    {
        var side = System.Math.Sqrt(area);
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(side, 0),
            new Point2(side, side),
            new Point2(0, side),
        });

        return new CadPad(id, $"C{id}", "L1", polygon);
    }
}
