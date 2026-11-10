using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadAreaBucketServiceTests
{
    private static readonly int[] _expectedBucketIds1 = [1, 2];
    private static readonly int[] _expectedBucketIds2 = [3, 4];

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
        Assert.Equal(_expectedBucketIds1, buckets[0]);
        Assert.Equal(_expectedBucketIds2, buckets[1]);
    }

    [Fact]
    public void ResolveBucketMembers_ReturnsEmptyWhenAnchorMissing()
    {
        var pads = new List<CadPad>
        {
            CreateSquarePad(10, 80.0),
            CreateSquarePad(11, 80.01),
        };

        var bucket = CadAreaBucketService.ResolveBucketMembers(pads, 0.01, 999);

        Assert.Empty(bucket);
    }

    [Fact]
    public void BuildBucketsOrdersEqualAreaPadsByIdWithinTheirBuckets()
    {
        CadPad[] pads = [CreatePad(40, 400), CreatePad(30, 100), CreatePad(10, 100), CreatePad(20, 400)];

        var buckets = CadAreaBucketService.BuildBuckets(pads, 0);

        Assert.Equal(2, buckets.Count);
        Assert.Equal([10, 30], buckets[0]);
        Assert.Equal([20, 40], buckets[1]);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.75)]
    public void BuildBucketsClampsToleranceAtHalf(double tolerance)
    {
        CadPad[] pads = [CreatePad(1, 100), CreatePad(2, 150), CreatePad(3, 151), CreatePad(4, 200)];

        var buckets = CadAreaBucketService.BuildBuckets(pads, tolerance);

        Assert.Equal(2, buckets.Count);
        Assert.Equal([1, 2], buckets[0]);
        Assert.Equal([3, 4], buckets[1]);
    }

    [Fact]
    public void BuildBucketsGroupsZeroAreaPadsSeparatelyFromPositiveArea()
    {
        CadPad[] pads = [CreatePad(3, 100), CreatePad(2, 0), CreatePad(1, 0)];

        var buckets = CadAreaBucketService.BuildBuckets(pads, 0.5);

        Assert.Equal(2, buckets.Count);
        Assert.Equal([1, 2], buckets[0]);
        Assert.Equal([3], buckets[1]);
    }

    private static CadPad CreatePad(int id, double area)
    {
        var polygon = new Polygon2(
        [
            new Point2(0, 0),
            new Point2(area, 0),
            new Point2(area, 1),
            new Point2(0, 1),
        ]);

        return new CadPad(id, $"C{id}", "L1", polygon);
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
