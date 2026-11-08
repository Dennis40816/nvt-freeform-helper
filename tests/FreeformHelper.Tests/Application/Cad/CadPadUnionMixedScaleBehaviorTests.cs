// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadUnionMixedScaleBehaviorTests
{
    [Fact]
    public void Union_SubAreaOutlineAlongsideUsablePad_PreservesUsableCopperAndPathCounts()
    {
        var polygons = new[]
        {
            TestGeometryFactory.CreateRect(0, 0, 0.00002, 0.00002),
            TestGeometryFactory.CreateRect(10, 10, 20, 20),
        };

        var result = CadPadUnionService.Union(polygons);

        Assert.Equal(2, result.InputPolygonCount);
        Assert.Equal(2, result.OutputPathCount);
        Assert.Equal(0, result.HolePathCount);
        var outline = Assert.Single(result.OuterPolygons);
        Assert.Equal(100, outline.Area(), 9);
        Assert.Equal(10, outline.Bounds.MinX, 6);
        Assert.Equal(10, outline.Bounds.MinY, 6);
        Assert.Equal(20, outline.Bounds.MaxX, 6);
        Assert.Equal(20, outline.Bounds.MaxY, 6);
    }
}
