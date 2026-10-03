using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerComputationServiceTests
{
    [Fact]
    public void BuildSnapshot_ComputesAaCornersGuidesAndBistRectWithCopperSafeCoordinates()
    {
        var grid = BuildGrid(rows: 2, cols: 2, cellWidth: 10d, cellHeight: 5d);
        var request = new CoordinatePlannerRequest(
            MachineOriginX: 100d,
            MachineOriginY: 200d,
            MachineWidth: 200d,
            MachineHeight: 100d,
            PixelWidth: 1000,
            PixelHeight: 500,
            HorizontalGuideCount: 5,
            VerticalGuideCount: 5,
            CopperPillarDiameter: 10d,
            ShowBistRectangle: true,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 100d,
            CustomArrayTopLeftMachineY: 200d,
            CustomArrayTopRightMachineX: 300d,
            CustomArrayTopRightMachineY: 200d,
            CustomArrayBottomRightMachineX: 300d,
            CustomArrayBottomRightMachineY: 300d,
            CustomArrayBottomLeftMachineX: 100d,
            CustomArrayBottomLeftMachineY: 300d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(grid, request);

        Assert.Equal(8, snapshot.Points.Count);
        Assert.Equal(10, snapshot.Lines.Count);
        Assert.Single(snapshot.Rectangles);
        Assert.Equal(5d, snapshot.CopperPillarRadiusMachine, 6);

        var aaTopLeft = Assert.Single(snapshot.Points, point => point.Key == "aa-tl");
        Assert.Equal(0d, aaTopLeft.PixelX, 6);
        Assert.Equal(0d, aaTopLeft.PixelY, 6);
        Assert.Equal(100d, aaTopLeft.MachineX, 6);
        Assert.Equal(200d, aaTopLeft.MachineY, 6);
        Assert.Equal(105d, aaTopLeft.SafeMachineX, 6);
        Assert.Equal(205d, aaTopLeft.SafeMachineY, 6);

        var aaBottomRight = Assert.Single(snapshot.Points, point => point.Key == "aa-br");
        Assert.Equal(1000d, aaBottomRight.PixelX, 6);
        Assert.Equal(500d, aaBottomRight.PixelY, 6);
        Assert.Equal(300d, aaBottomRight.MachineX, 6);
        Assert.Equal(300d, aaBottomRight.MachineY, 6);
        Assert.Equal(295d, aaBottomRight.SafeMachineX, 6);
        Assert.Equal(295d, aaBottomRight.SafeMachineY, 6);

        var horizontalGuide = Assert.Single(snapshot.Lines, line => line.Key == "h-1");
        Assert.Equal(0d, horizontalGuide.StartPixelY, 6);
        Assert.Equal(200d, horizontalGuide.StartMachineY, 6);
        Assert.Equal(0d, horizontalGuide.StartPixelX, 6);
        Assert.Equal(1000d, horizontalGuide.EndPixelX, 6);

        var horizontalGuideLast = Assert.Single(snapshot.Lines, line => line.Key == "h-5");
        Assert.Equal(500d, horizontalGuideLast.StartPixelY, 6);
        Assert.Equal(300d, horizontalGuideLast.StartMachineY, 6);

        var verticalGuide = Assert.Single(snapshot.Lines, line => line.Key == "v-5");
        Assert.Equal(1000d, verticalGuide.StartPixelX, 6);
        Assert.Equal(300d, verticalGuide.StartMachineX, 6);
        Assert.Equal(0d, verticalGuide.StartPixelY, 6);
        Assert.Equal(500d, verticalGuide.EndPixelY, 6);

        var bistRect = snapshot.Rectangles[0];
        Assert.Equal(250d, bistRect.PixelLeft, 6);
        Assert.Equal(125d, bistRect.PixelTop, 6);
        Assert.Equal(750d, bistRect.PixelRight, 6);
        Assert.Equal(375d, bistRect.PixelBottom, 6);
        Assert.Equal(150d, bistRect.MachineLeft, 6);
        Assert.Equal(225d, bistRect.MachineTop, 6);
        Assert.Equal(250d, bistRect.MachineRight, 6);
        Assert.Equal(275d, bistRect.MachineBottom, 6);
        Assert.Equal(155d, bistRect.SafeMachineLeft, 6);
        Assert.Equal(230d, bistRect.SafeMachineTop, 6);
        Assert.Equal(245d, bistRect.SafeMachineRight, 6);
        Assert.Equal(270d, bistRect.SafeMachineBottom, 6);
    }

    [Fact]
    public void BuildSnapshot_ComputesCustomFourPointArrayWithSafeDots()
    {
        var grid = BuildGrid(rows: 2, cols: 2, cellWidth: 10d, cellHeight: 5d);
        var request = new CoordinatePlannerRequest(
            MachineOriginX: 100d,
            MachineOriginY: 200d,
            MachineWidth: 200d,
            MachineHeight: 100d,
            PixelWidth: 1000,
            PixelHeight: 500,
            HorizontalGuideCount: 0,
            VerticalGuideCount: 0,
            CopperPillarDiameter: 10d,
            ShowBistRectangle: false,
            ShowCustomArray: true,
            CustomArrayColumnCount: 3,
            CustomArrayRowCount: 2,
            CustomArrayTopLeftMachineX: 100d,
            CustomArrayTopLeftMachineY: 200d,
            CustomArrayTopRightMachineX: 300d,
            CustomArrayTopRightMachineY: 200d,
            CustomArrayBottomRightMachineX: 300d,
            CustomArrayBottomRightMachineY: 300d,
            CustomArrayBottomLeftMachineX: 100d,
            CustomArrayBottomLeftMachineY: 300d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(grid, request);

        Assert.Equal(14, snapshot.Points.Count);
        Assert.Equal(4, snapshot.Lines.Count(line => line.Kind == CoordinatePlannerLineKind.CustomArrayEdge));
        Assert.Equal(4, snapshot.Points.Count(point => point.Kind == CoordinatePlannerPointKind.CustomArrayCorner));
        Assert.Equal(6, snapshot.Points.Count(point => point.Kind == CoordinatePlannerPointKind.CustomArrayDot));

        var topLeftCorner = Assert.Single(snapshot.Points, point => point.Key == "array-tl");
        Assert.Equal(100d, topLeftCorner.MachineX, 6);
        Assert.Equal(200d, topLeftCorner.MachineY, 6);
        Assert.Equal(105d, topLeftCorner.SafeMachineX, 6);
        Assert.Equal(205d, topLeftCorner.SafeMachineY, 6);

        var firstDot = Assert.Single(snapshot.Points, point => point.Key == "array-dot-r1-c1");
        Assert.Equal(100d, firstDot.MachineX, 6);
        Assert.Equal(200d, firstDot.MachineY, 6);
        Assert.Equal(105d, firstDot.SafeMachineX, 6);
        Assert.Equal(205d, firstDot.SafeMachineY, 6);

        var middleDot = Assert.Single(snapshot.Points, point => point.Key == "array-dot-r1-c2");
        Assert.Equal(200d, middleDot.MachineX, 6);
        Assert.Equal(200d, middleDot.MachineY, 6);
        Assert.Equal(200d, middleDot.SafeMachineX, 6);
        Assert.Equal(205d, middleDot.SafeMachineY, 6);

        var lastDot = Assert.Single(snapshot.Points, point => point.Key == "array-dot-r2-c3");
        Assert.Equal(300d, lastDot.MachineX, 6);
        Assert.Equal(300d, lastDot.MachineY, 6);
        Assert.Equal(295d, lastDot.SafeMachineX, 6);
        Assert.Equal(295d, lastDot.SafeMachineY, 6);
    }

    [Fact]
    public void BuildSnapshot_WhenGuideBoundsDiffer_UsesGuideBoundsOnlyForGuideLines()
    {
        var activeAreaBounds = new Rect2(10d, 20d, 110d, 220d);
        var guideBounds = new Rect2(0d, 0d, 1d, 1d);
        var request = new CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 100d,
            MachineHeight: 200d,
            PixelWidth: 1000,
            PixelHeight: 2000,
            HorizontalGuideCount: 3,
            VerticalGuideCount: 3,
            CopperPillarDiameter: 0d,
            ShowBistRectangle: false,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 0d,
            CustomArrayTopLeftMachineY: 0d,
            CustomArrayTopRightMachineX: 100d,
            CustomArrayTopRightMachineY: 0d,
            CustomArrayBottomRightMachineX: 100d,
            CustomArrayBottomRightMachineY: 200d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 200d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, guideBounds, request);

        var aaTopLeft = Assert.Single(snapshot.Points, point => point.Key == "aa-tl");
        Assert.Equal(10d, aaTopLeft.World.X, 6);
        Assert.Equal(220d, aaTopLeft.World.Y, 6);

        var firstHorizontalGuide = Assert.Single(snapshot.Lines, line => line.Key == "h-1");
        Assert.Equal(0d, firstHorizontalGuide.StartWorld.X, 6);
        Assert.Equal(1d, firstHorizontalGuide.StartWorld.Y, 6);
        Assert.Equal(1d, firstHorizontalGuide.EndWorld.X, 6);
        Assert.Equal(1d, firstHorizontalGuide.EndWorld.Y, 6);

        var lastVerticalGuide = Assert.Single(snapshot.Lines, line => line.Key == "v-3");
        Assert.Equal(1d, lastVerticalGuide.StartWorld.X, 6);
        Assert.Equal(1d, lastVerticalGuide.StartWorld.Y, 6);
        Assert.Equal(1d, lastVerticalGuide.EndWorld.X, 6);
        Assert.Equal(0d, lastVerticalGuide.EndWorld.Y, 6);
    }

    [Fact]
    public void BuildSnapshot_WhenGuideModeIsPitch_UsesPitchAndInsetOffsets()
    {
        var activeAreaBounds = new Rect2(0d, 0d, 100d, 50d);
        var request = BuildGuideModeRequest(
            horizontalMode: CoordinateGuideGenerationMode.Pitch,
            verticalMode: CoordinateGuideGenerationMode.Pitch,
            horizontalPitch: 20d,
            verticalPitch: 30d,
            horizontalInset: 10d,
            verticalInset: 5d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, request);

        var horizontalMachineY = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY)
            .ToArray();
        var verticalMachineX = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => line.StartMachineX)
            .ToArray();

        Assert.Equal([10d, 30d, 50d, 70d, 90d], horizontalMachineY);
        Assert.Equal([5d, 35d, 65d, 95d], verticalMachineX);
    }

    [Fact]
    public void BuildSnapshot_WhenGuideModeIsExplicitPositions_FiltersByInsetAndSortsPositions()
    {
        var activeAreaBounds = new Rect2(0d, 0d, 100d, 50d);
        var request = BuildGuideModeRequest(
            horizontalMode: CoordinateGuideGenerationMode.ExplicitPositions,
            verticalMode: CoordinateGuideGenerationMode.ExplicitPositions,
            horizontalInset: 10d,
            verticalInset: 5d,
            horizontalPositions: [90d, 10d, 101d, 50d],
            verticalPositions: [2d, 95d, 40d]);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, request);

        var horizontalMachineY = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY)
            .ToArray();
        var verticalMachineX = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => line.StartMachineX)
            .ToArray();

        Assert.Equal([10d, 50d, 90d], horizontalMachineY);
        Assert.Equal([40d, 95d], verticalMachineX);
    }

    [Fact]
    public void BuildSnapshot_WhenCustomPointAndPathProvided_EmitsCustomArtifacts()
    {
        var activeAreaBounds = new Rect2(0d, 0d, 100d, 100d);
        var request = BuildGuideModeRequest(
            horizontalMode: CoordinateGuideGenerationMode.Count,
            verticalMode: CoordinateGuideGenerationMode.Count,
            horizontalPositions: null,
            verticalPositions: null) with
        {
            CustomPoints =
            [
                new CoordinateCustomPointRequest("1", "Probe A", 25d, 30d),
            ],
            CustomPaths =
            [
                new CoordinateCustomPathRequest("1", "Edge sweep", 10d, 20d, 90d, 20d, 3),
            ],
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, request);

        var customPoint = Assert.Single(snapshot.Points, point => point.Kind == CoordinatePlannerPointKind.CustomPoint);
        Assert.Equal("custom-point-1", customPoint.Key);
        Assert.Equal(25d, customPoint.MachineX, 6);
        var customPath = Assert.Single(snapshot.Lines, line => line.Kind == CoordinatePlannerLineKind.CustomPath);
        Assert.Equal("custom-path-1", customPath.Key);
        Assert.Equal(10d, customPath.StartMachineX, 6);
        Assert.Equal(90d, customPath.EndMachineX, 6);
        var pathSteps = snapshot.Points
            .Where(static point => point.Kind == CoordinatePlannerPointKind.CustomPathStep)
            .ToArray();
        Assert.Equal(3, pathSteps.Length);
        Assert.Equal("custom-path-1-step-2", pathSteps[1].Key);
        Assert.Equal(50d, pathSteps[1].MachineX, 6);
    }

    private static RegularGrid BuildGrid(int rows, int cols, double cellWidth, double cellHeight)
    {
        var pads = new List<RegularPad>(rows * cols);
        var xEdges = Enumerable.Range(0, cols + 1).Select(value => value * cellWidth).ToArray();
        var yEdges = Enumerable.Range(0, rows + 1).Select(value => value * cellHeight).ToArray();

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var left = col * cellWidth;
                var top = row * cellHeight;
                pads.Add(new RegularPad(
                    row,
                    col,
                    row * cols + col,
                    new Polygon2(new[]
                    {
                        new Point2(left, top),
                        new Point2(left + cellWidth, top),
                        new Point2(left + cellWidth, top + cellHeight),
                        new Point2(left, top + cellHeight),
                    })));
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    private static CoordinatePlannerRequest BuildGuideModeRequest(
        CoordinateGuideGenerationMode horizontalMode,
        CoordinateGuideGenerationMode verticalMode,
        double horizontalPitch = 0d,
        double verticalPitch = 0d,
        double horizontalInset = 0d,
        double verticalInset = 0d,
        IReadOnlyList<double>? horizontalPositions = null,
        IReadOnlyList<double>? verticalPositions = null)
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
            CopperPillarDiameter: 0d,
            ShowBistRectangle: false,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 0d,
            CustomArrayTopLeftMachineY: 0d,
            CustomArrayTopRightMachineX: 100d,
            CustomArrayTopRightMachineY: 0d,
            CustomArrayBottomRightMachineX: 100d,
            CustomArrayBottomRightMachineY: 100d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 100d,
            HorizontalGuideMode: horizontalMode,
            VerticalGuideMode: verticalMode,
            HorizontalGuidePitch: horizontalPitch,
            VerticalGuidePitch: verticalPitch,
            HorizontalGuideInset: horizontalInset,
            VerticalGuideInset: verticalInset,
            HorizontalGuidePositions: horizontalPositions,
            VerticalGuidePositions: verticalPositions);
    }
}
