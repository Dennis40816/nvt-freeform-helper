// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerComputationBoundaryBehaviorTests
{
    [Fact]
    public void BuildSnapshot_ZeroArrayRowsOrColumns_KeepsEditableOutlineWithoutDots()
    {
        var request = BuildRequest();
        var bounds = new Rect2(0d, 0d, 100d, 100d);
        var requests = new[]
        {
            request with { CustomArrayColumnCount = 0, CustomArrayRowCount = 3 },
            request with { CustomArrayColumnCount = 3, CustomArrayRowCount = 0 },
        };

        foreach (var emptyArrayRequest in requests)
        {
            var snapshot = CoordinatePlannerComputationService.BuildSnapshot(bounds, emptyArrayRequest);

            Assert.Equal(4, snapshot.Points.Count(static point => point.Kind == CoordinatePlannerPointKind.CustomArrayCorner));
            Assert.Equal(4, snapshot.Lines.Count(static line => line.Kind == CoordinatePlannerLineKind.CustomArrayEdge));
            Assert.DoesNotContain(snapshot.Points, static point => point.Kind == CoordinatePlannerPointKind.CustomArrayDot);
            var bottomRight = Assert.Single(snapshot.Points, static point => point.Key == "array-br");
            Assert.Equal(70d, bottomRight.MachineX, 6);
            Assert.Equal(80d, bottomRight.MachineY, 6);
        }
    }

    [Fact]
    public void BuildSnapshot_OneByOneArray_PlacesRawAndSafeDotAtBilinearCenter()
    {
        var request = BuildRequest() with { CustomArrayColumnCount = 1, CustomArrayRowCount = 1 };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(100d, 200d, 300d, 400d), request);

        var dot = Assert.Single(snapshot.Points, static point => point.Kind == CoordinatePlannerPointKind.CustomArrayDot);
        Assert.Equal("array-dot-r1-c1", dot.Key);
        Assert.Equal(50d, dot.MachineX, 6);
        Assert.Equal(45d, dot.MachineY, 6);
        Assert.Equal(50d, dot.SafeMachineX, 6);
        Assert.Equal(45d, dot.SafeMachineY, 6);
        Assert.Equal(500d, dot.PixelX, 6);
        Assert.Equal(450d, dot.PixelY, 6);
        Assert.Equal(new Point2(200d, 310d), dot.World);
        Assert.Equal(dot.World, dot.SafeWorld);
    }

    [Fact]
    public void BuildSnapshot_BistInsetExceedsHalfRectangleHeight_CollapsesOnlySafeHeightToCenter()
    {
        var request = BuildRequest() with
        {
            MachineOriginX = 10d,
            MachineOriginY = 20d,
            MachineWidth = 200d,
            CopperPillarDiameter = 60d,
            ShowBistRectangle = true,
            ShowCustomArray = false,
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(0d, 0d, 20d, 10d), request);

        var rectangle = Assert.Single(snapshot.Rectangles);
        Assert.Equal(45d, rectangle.MachineTop, 6);
        Assert.Equal(95d, rectangle.MachineBottom, 6);
        Assert.Equal(70d, rectangle.SafeMachineTop, 6);
        Assert.Equal(70d, rectangle.SafeMachineBottom, 6);
        Assert.Equal(90d, rectangle.SafeMachineLeft, 6);
        Assert.Equal(130d, rectangle.SafeMachineRight, 6);
        Assert.Equal(new Rect2(8d, 5d, 12d, 5d), rectangle.SafeWorldBounds);
    }

    private static CoordinatePlannerRequest BuildRequest()
    {
        return new CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 100d,
            MachineHeight: 100d,
            PixelWidth: 1000,
            PixelHeight: 1000,
            HorizontalGuideCount: 0,
            VerticalGuideCount: 0,
            CopperPillarDiameter: 10d,
            ShowBistRectangle: false,
            ShowCustomArray: true,
            CustomArrayColumnCount: 2,
            CustomArrayRowCount: 2,
            CustomArrayTopLeftMachineX: 10d,
            CustomArrayTopLeftMachineY: 20d,
            CustomArrayTopRightMachineX: 90d,
            CustomArrayTopRightMachineY: 20d,
            CustomArrayBottomRightMachineX: 70d,
            CustomArrayBottomRightMachineY: 80d,
            CustomArrayBottomLeftMachineX: 30d,
            CustomArrayBottomLeftMachineY: 60d);
    }
}
