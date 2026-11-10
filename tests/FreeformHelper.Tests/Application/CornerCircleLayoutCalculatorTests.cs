// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CornerCircleLayoutCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1080, 50)]
    [InlineData(100, 200, 1100, 900, 25.5)]
    [InlineData(0, 0, 1920, 1080, 0)]
    public void RectangleReturnsExactInset(double minX, double minY, double maxX, double maxY, double radius)
    {
        var polygon = Rectangle(new(minX, minY, maxX, maxY));
        var expected = new Rect2(minX + radius, minY + radius, maxX - radius, maxY - radius);

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, radius));

        Assert.Equal(CornerCircleLayoutStatus.Ok, result.Status);
        Assert.Equal(radius, result.Radius, precision: 12);
        Assert.Equal(expected, result.CenterRectangle);
        AssertCenters(expected, result.Centers);
        Assert.Empty(result.Message);
    }

    [Theory]
    [InlineData(1920, 1080, 540)]
    [InlineData(1920, 1080, 600)]
    [InlineData(1080, 1920, 540)]
    public void CollapsedOrOversizedInsetDoesNotFit(double width, double height, double radius)
    {
        var polygon = Rectangle(new(0, 0, width, height));

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, radius));

        AssertFailure(CornerCircleLayoutStatus.DoesNotFit, radius, result);
        Assert.Contains("radius is too large", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidRadiusReturnsAnEmptyDiagnostic(double radius)
    {
        var polygon = Rectangle(new(0, 0, 1920, 1080));

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, radius));

        AssertFailure(CornerCircleLayoutStatus.InvalidRadius, radius, result);
    }

    [Fact]
    public void NullActiveAreaReturnsAnEmptyDiagnostic()
    {
        var result = CornerCircleLayoutCalculator.Calculate(new(null!, 50));

        AssertFailure(CornerCircleLayoutStatus.InvalidActiveArea, 50, result);
    }

    [Fact]
    public void ZeroAreaReturnsAnEmptyDiagnostic()
    {
        var polygon = new Polygon2([new(0, 0), new(1, 1), new(2, 2)]);

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, 0));

        AssertFailure(CornerCircleLayoutStatus.InvalidActiveArea, 0, result);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, 0)]
    public void NonfiniteVertexReturnsAnEmptyDiagnostic(double x, double y)
    {
        var polygon = new Polygon2([new(x, y), new(100, 0), new(0, 100)]);

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, 0));

        AssertFailure(CornerCircleLayoutStatus.InvalidActiveArea, 0, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void PolygonRejectsFewerThanThreeVerticesBeforeCalculation(int vertexCount)
    {
        var vertices = Enumerable.Range(0, vertexCount).Select(index => new Point2(index, index));

        Assert.Throws<ArgumentException>(() => new Polygon2(vertices));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(150)]
    public void RoundedSquareUsesTheFortyFiveDegreeErodedCorners(double radius)
    {
        var polygon = RoundedSquare(1000, 200);
        var offset = 200 - (200 - radius) / Math.Sqrt(2);
        var expected = new Rect2(offset, offset, 1000 - offset, 1000 - offset);

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, radius));

        Assert.Equal(CornerCircleLayoutStatus.Ok, result.Status);
        Assert.Equal(4, result.Centers.Count);
        AssertClose(expected.MinX, result.CenterRectangle.MinX);
        AssertClose(expected.MinY, result.CenterRectangle.MinY);
        AssertClose(expected.MaxX, result.CenterRectangle.MaxX);
        AssertClose(expected.MaxY, result.CenterRectangle.MaxY);
        AssertCenters(result.CenterRectangle, result.Centers);
        Assert.All(result.Centers, center => AssertCircleInside(polygon, center, radius));
    }

    [Fact]
    public void CornerCutKeepsEveryCircleInsideAndMatchesTheKnownInsetArea()
    {
        var polygon = new Polygon2(
        [
            new(0, 0), new(1000, 0), new(1000, 400),
            new(400, 400), new(400, 1000), new(0, 1000),
        ]);

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, 50));

        Assert.Equal(CornerCircleLayoutStatus.Ok, result.Status);
        Assert.Equal(4, result.Centers.Count);
        AssertCenters(result.CenterRectangle, result.Centers);
        Assert.All(result.Centers, center => AssertCircleInside(polygon, center, 50));
        Assert.True(result.CenterRectangle.Width * result.CenterRectangle.Height >= 900 * 300);
    }

    [Fact]
    public void NotchBetweenTheCirclesDoesNotConstrainTheRectangleInterior()
    {
        var polygon = new Polygon2(
        [
            new(0, 0), new(400, 0), new(400, 600), new(600, 600),
            new(600, 0), new(1000, 0), new(1000, 1000), new(0, 1000),
        ]);
        var expected = new Rect2(50, 50, 950, 950);

        var result = CornerCircleLayoutCalculator.Calculate(new(polygon, 50));

        Assert.Equal(CornerCircleLayoutStatus.Ok, result.Status);
        Assert.Equal(expected, result.CenterRectangle);
        AssertCenters(expected, result.Centers);
        Assert.All(result.Centers, center => AssertCircleInside(polygon, center, 50));
    }

    [Fact]
    public void RepeatedCalculationReturnsEqualRecords()
    {
        var request = new CornerCircleLayoutRequest(RoundedSquare(1000, 200), 50);

        var first = CornerCircleLayoutCalculator.Calculate(request);
        var second = CornerCircleLayoutCalculator.Calculate(request);

        Assert.Equal(CornerCircleLayoutStatus.Ok, first.Status);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    private static Polygon2 Rectangle(Rect2 bounds) => new(
    [
        new(bounds.MinX, bounds.MinY), new(bounds.MaxX, bounds.MinY),
        new(bounds.MaxX, bounds.MaxY), new(bounds.MinX, bounds.MaxY),
    ]);

    private static Polygon2 RoundedSquare(double size, double cornerRadius)
    {
        Point2[] arcCenters =
        [
            new(size - cornerRadius, cornerRadius), new(size - cornerRadius, size - cornerRadius),
            new(cornerRadius, size - cornerRadius), new(cornerRadius, cornerRadius),
        ];
        var vertices = new List<Point2>();
        for (var corner = 0; corner < arcCenters.Length; corner++)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            for (var step = 0; step <= 180; step++)
            {
                var angle = (corner * 90 - 90 + step * 0.5) * Math.PI / 180;
                vertices.Add(arcCenters[corner] + new Point2(cornerRadius * Math.Cos(angle), cornerRadius * Math.Sin(angle)));
            }
        }

        return new(vertices);
    }

    private static void AssertFailure(CornerCircleLayoutStatus status, double radius, CornerCircleLayoutResult result)
    {
        Assert.Equal(status, result.Status);
        Assert.Equal(radius, result.Radius, precision: 12);
        Assert.Equal(default, result.CenterRectangle);
        Assert.Empty(result.Centers);
        Assert.NotEmpty(result.Message);
    }

    private static void AssertCenters(Rect2 expected, IReadOnlyList<Point2> centers)
    {
        Point2[] expectedCenters =
        [
            new(expected.MinX, expected.MinY), new(expected.MaxX, expected.MinY),
            new(expected.MaxX, expected.MaxY), new(expected.MinX, expected.MaxY),
        ];
        Assert.Equal(expectedCenters, centers);
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.InRange(actual, expected - 0.01, expected + 0.01);

    private static void AssertCircleInside(Polygon2 polygon, Point2 center, double radius)
    {
        var distances = Enumerable.Range(0, polygon.Vertices.Length)
            .Select(index => SegmentDistance(center, polygon.Vertices[index], polygon.Vertices[(index + 1) % polygon.Vertices.Length]))
            .ToArray();
        Assert.True(IsInside(polygon, center) || distances.Min() <= 1e-9);
        Assert.All(distances, distance => Assert.True(distance + 1e-9 >= radius));
    }

    private static bool IsInside(Polygon2 polygon, Point2 point)
    {
        var winding = 0;
        for (var index = 0; index < polygon.Vertices.Length; index++)
        {
            var start = polygon.Vertices[index];
            var end = polygon.Vertices[(index + 1) % polygon.Vertices.Length];
            var cross = (end.X - start.X) * (point.Y - start.Y) - (end.Y - start.Y) * (point.X - start.X);
            if (start.Y <= point.Y && end.Y > point.Y && cross > 0)
            {
                winding++;
            }
            else if (start.Y > point.Y && end.Y <= point.Y && cross < 0)
            {
                winding--;
            }
        }

        return winding != 0;
    }

    private static double SegmentDistance(Point2 point, Point2 start, Point2 end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        var projection = lengthSquared == 0 ? 0 :
            Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
        return Math.Sqrt(Math.Pow(point.X - start.X - projection * dx, 2) + Math.Pow(point.Y - start.Y - projection * dy, 2));
    }
}
