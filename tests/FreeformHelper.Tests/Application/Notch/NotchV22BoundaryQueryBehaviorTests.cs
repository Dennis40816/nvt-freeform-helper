// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22BoundaryQueryBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryQuery_RepeatedCadAndThresholdSliver_ReturnsDistinctOwnersAndAllBlockers(bool useLargePool)
    {
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 0, 0, 0, 10, 10);
        var left = TestGeometryFactory.CreateCadPad(30, "PAD", 0, 0, 2, 10);
        var right = TestGeometryFactory.CreateCadPad(10, "PAD", 8, 0, 10, 10);
        var sliver = TestGeometryFactory.CreateCadPad(20, "PAD", 5, 0, 5.125, 8);
        var edgeTouch = TestGeometryFactory.CreateCadPad(40, "PAD", 10, 0, 12, 10);
        var cornerTouch = new CadPad(50, "CAD", "PAD", new Polygon2(
        [
            new Point2(9, 11), new Point2(11, 9), new Point2(11, 11),
        ]));
        var pads = new List<CadPad> { left, right, sliver, left, edgeTouch, cornerTouch };
        if (useLargePool)
        {
            for (var index = 0; index < 64; index++)
            {
                pads.Add(TestGeometryFactory.CreateCadPad(100 + index, "PAD", 100 + index, 100, 100.25 + index, 100.25));
            }
        }

        var context = NotchV22CompensationService.CreateBoundaryQueryContext(pads, strictOverlapRatio: 0.01);

        // The sliver's area is exactly the strict threshold (1), but it remains a collision blocker.
        Assert.Equal([10, 30], context.GetStrictOwnerCadPads(regular).Select(static pad => pad.Id).ToArray());
        Assert.Equal([10, 20], context.GetBlockingCadPads(regular, currentCadPadId: 30).Select(static pad => pad.Id).ToArray());
        Assert.Equal([20, 30], context.GetBlockingCadPads(regular, currentCadPadId: 10).Select(static pad => pad.Id).ToArray());
        Assert.Equal([10, 30], context.GetStrictOwnerCadPads(regular).Select(static pad => pad.Id).ToArray());
    }

    [Fact]
    public void BoundaryQuery_IndexedPadsSpanningBuckets_ReturnsEachOwnerOnceIncludingPoolEdges()
    {
        var pads = Enumerable.Range(0, 64).Select(index =>
        {
            var x = (index % 8) * 10;
            var y = (index / 8) * 10;
            return TestGeometryFactory.CreateCadPad(64 - index, "PAD", x, y, x + 8, y + 8);
        }).ToArray();
        var context = NotchV22CompensationService.CreateBoundaryQueryContext(pads, strictOverlapRatio: 0);
        var enclosing = TestGeometryFactory.CreateRegularPad(0, 0, 0, -1, -1, 80, 80);
        var edge = TestGeometryFactory.CreateRegularPad(0, 0, 1, 75, 75, 85, 85);
        var outside = TestGeometryFactory.CreateRegularPad(0, 0, 2, 90, 90, 100, 100);

        Assert.Equal(Enumerable.Range(1, 64), context.GetStrictOwnerCadPads(enclosing).Select(static pad => pad.Id));
        Assert.Equal(Enumerable.Range(1, 63), context.GetBlockingCadPads(enclosing, currentCadPadId: 64).Select(static pad => pad.Id));
        Assert.Equal(1, Assert.Single(context.GetStrictOwnerCadPads(edge)).Id);
        Assert.Empty(context.GetBlockingCadPads(edge, currentCadPadId: 1));
        Assert.Empty(context.GetStrictOwnerCadPads(outside));
        Assert.Empty(context.GetBlockingCadPads(outside, currentCadPadId: 1));
    }
}
