// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class Rect2EmptyGeometryBehaviorTests
{
    [Fact]
    public void FromPointsWithoutVerticesReturnsEmptyBoundsAtOrigin()
    {
        var bounds = Rect2.FromPoints(Array.Empty<Point2>());

        Assert.Equal(new Rect2(0, 0, 0, 0), bounds);
        Assert.True(bounds.IsEmpty);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void EmptyBoundsNeverIntersectAndDoNotExpandAUnion(double width, double height)
    {
        var empty = new Rect2(15, 25, 15 + width, 25 + height);
        var containing = new Rect2(10, 20, 30, 40);
        var distant = new Rect2(100, 200, 130, 240);

        var emptyFirstUnion = Rect2.Union(empty, distant);
        var emptyLastUnion = Rect2.Union(distant, empty);

        Assert.False(empty.Intersects(containing));
        Assert.False(containing.Intersects(empty));
        Assert.Equal(distant, emptyFirstUnion);
        Assert.Equal(distant, emptyLastUnion);
    }
}
