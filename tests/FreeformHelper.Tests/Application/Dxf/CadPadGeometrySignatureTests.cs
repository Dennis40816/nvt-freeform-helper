using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadGeometrySignatureTests
{
    [Fact]
    public void Build_DefaultPrecision_IsCanonicalAndPreservesSubMicroTolerance()
    {
        Point2[] vertices =
        [
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(5.0000004, 5),
            new Point2(0, 10),
        ];
        var original = new Polygon2(vertices);
        var cyclic = new Polygon2(vertices[2..].Concat(vertices[..2]));
        var reversed = new Polygon2(vertices.Reverse());
        var subMicroVariant = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(4.9999996, 5),
            new Point2(0, 10),
        });

        var expected = CadPadGeometrySignature.Build(original);

        Assert.Equal(expected, CadPadGeometrySignature.Build(cyclic));
        Assert.Equal(expected, CadPadGeometrySignature.Build(reversed));
        Assert.Equal(expected, CadPadGeometrySignature.Build(subMicroVariant));
        Assert.Equal(
            "0,0;0,1;1.234569,0;",
            CadPadGeometrySignature.Build(new Polygon2(new[]
            {
                new Point2(0, 0),
                new Point2(1.2345685, 0),
                new Point2(0, 1),
            })));
    }
}
