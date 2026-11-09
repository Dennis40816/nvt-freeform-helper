// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerUnavailableGuideBehaviorTests
{
    private static readonly double[] _fiveGuideXs = [100d, 125d, 150d, 175d, 200d];
    private static readonly double[] _twoGuideXs = [125d, 175d];

    [Fact]
    public void BuildSnapshot_EmptyRegularGrid_ExplainsThatCoordinatePlanningRequiresPads()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 0, cols: 2);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CoordinatePlannerComputationService.BuildSnapshot(grid, BuildRequest()));

        Assert.Equal("Coordinate planner requires a non-empty regular grid.", exception.Message);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    public void BuildSnapshot_NonPositiveHorizontalPitch_OmitsOnlyHorizontalGuides(double pitch)
    {
        var request = BuildRequest() with
        {
            HorizontalGuideMode = CoordinateGuideGenerationMode.Pitch,
            VerticalGuideMode = CoordinateGuideGenerationMode.Pitch,
            HorizontalGuidePitch = pitch,
            VerticalGuidePitch = 25d,
        };
        var bounds = new Rect2(10d, 20d, 110d, 100d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(bounds, request);

        Assert.DoesNotContain(snapshot.Lines, static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide);
        Assert.Equal(_fiveGuideXs, snapshot.Lines.Select(static line => line.StartMachineX));
        Assert.All(snapshot.Lines, static line => Assert.Equal(CoordinatePlannerLineKind.VerticalGuide, line.Kind));
        Assert.Equal(bounds, snapshot.ActiveAreaBounds);
        Assert.Equal(4, snapshot.Points.Count);
        var bottomRight = Assert.Single(snapshot.Points, static point => point.Key == "aa-br");
        Assert.Equal(new Point2(110d, 20d), bottomRight.World);
        Assert.Equal((200d, 280d), (bottomRight.MachineX, bottomRight.MachineY));
    }

    [Fact]
    public void BuildSnapshot_EmptyHorizontalPositions_OmitsOnlyHorizontalGuidesWithoutFallingBackToCount()
    {
        var request = BuildRequest() with
        {
            HorizontalGuideMode = CoordinateGuideGenerationMode.ExplicitPositions,
            VerticalGuideMode = CoordinateGuideGenerationMode.ExplicitPositions,
            HorizontalGuidePositions = [],
            VerticalGuidePositions = [75d, 25d],
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(10d, 20d, 110d, 100d), request);

        Assert.DoesNotContain(snapshot.Lines, static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide);
        Assert.Equal(_twoGuideXs, snapshot.Lines.Select(static line => line.StartMachineX));
        Assert.All(snapshot.Lines, static line => Assert.Equal(CoordinatePlannerLineKind.VerticalGuide, line.Kind));
        Assert.Equal(4, snapshot.Points.Count);
        var topLeft = Assert.Single(snapshot.Points, static point => point.Key == "aa-tl");
        Assert.Equal(new Point2(10d, 100d), topLeft.World);
        Assert.Equal((100d, 200d), (topLeft.MachineX, topLeft.MachineY));
    }

    private static CoordinatePlannerRequest BuildRequest()
    {
        return new CoordinatePlannerRequest(
            MachineOriginX: 100d,
            MachineOriginY: 200d,
            MachineWidth: 100d,
            MachineHeight: 80d,
            PixelWidth: 1000,
            PixelHeight: 800,
            HorizontalGuideCount: 3,
            VerticalGuideCount: 3,
            CopperPillarDiameter: 0d,
            ShowBistRectangle: false,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 100d,
            CustomArrayTopLeftMachineY: 200d,
            CustomArrayTopRightMachineX: 200d,
            CustomArrayTopRightMachineY: 200d,
            CustomArrayBottomRightMachineX: 200d,
            CustomArrayBottomRightMachineY: 280d,
            CustomArrayBottomLeftMachineX: 100d,
            CustomArrayBottomLeftMachineY: 280d);
    }
}
