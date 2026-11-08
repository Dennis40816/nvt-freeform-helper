// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22FinalOutlineVisibilityBehaviorTests
{
    [Theory]
    [InlineData(false, 1, 0, 0)]
    [InlineData(true, 0, 1, 20)]
    [InlineData(true, 1, 1, 20)]
    public void BuildWithoutUsableExpansionShowsCadOnlyWhenToFullIsEnabled(
        bool isToFullEnabled, int fragmentCount, int expectedCount, double expectedArea)
    {
        var cad = TestGeometryFactory.CreateCadPad(1, "CAD", 2, 3, 6, 8);
        var zeroAreaFragment = new Polygon2(new[]
        {
            new Point2(100, 100),
            new Point2(110, 100),
            new Point2(120, 100),
        });
        var applied = Enumerable.Repeat(zeroAreaFragment, fragmentCount).ToArray();

        var result = NotchV22FinalOutlineService.Build(cad, applied, isToFullEnabled);

        Assert.Equal(expectedCount, result.Count);
        Assert.Equal(expectedArea, result.Sum(static polygon => polygon.Area()), 10);
        Assert.All(result, polygon => Assert.Equal(cad.Bounds, polygon.Bounds));
    }

    [Fact]
    public void BuildKeepsATinyCadBodyVisibleWhenTheUnionDropsIt()
    {
        var body = TestGeometryFactory.CreateRect(0, 0, 0.00001, 0.00001);
        var cad = new CadPad(2, "Tiny pad", "CAD", body);

        var result = NotchV22FinalOutlineService.Build(cad, Array.Empty<Polygon2>(), isToFullEnabled: true);

        var outline = Assert.Single(result);
        Assert.Equal(body.Bounds, outline.Bounds);
        Assert.Equal(1e-10, outline.Area(), 15);
    }
}
