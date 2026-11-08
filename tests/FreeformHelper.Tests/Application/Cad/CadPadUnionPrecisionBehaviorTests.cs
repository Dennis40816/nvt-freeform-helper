// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadUnionPrecisionBehaviorTests
{
    [Theory]
    [InlineData(0d, 0, 0, false)]
    [InlineData(0.0000001d, 1, 0, false)]
    [InlineData(0.00002d, 1, 1, false)]
    [InlineData(0.001d, 1, 1, true)]
    public void Union_EmptyOrTinyCopper_ReturnsCountsAndOnlyUsableOutlines(
        double sideLength, int inputCount, int outputPathCount, bool hasOutline)
    {
        Polygon2[] polygons = inputCount == 0
            ? []
            : [TestGeometryFactory.CreateRect(0d, 0d, sideLength, sideLength)];

        var result = CadPadUnionService.Union(polygons);

        Assert.Equal(inputCount, result.InputPolygonCount);
        Assert.Equal(outputPathCount, result.OutputPathCount);
        Assert.Equal(0, result.HolePathCount);
        if (hasOutline)
        {
            var outline = Assert.Single(result.OuterPolygons);
            Assert.InRange(outline.Area(), sideLength * sideLength * 0.95d, sideLength * sideLength * 1.05d);
        }
        else
        {
            Assert.Empty(result.OuterPolygons);
        }
    }
}
