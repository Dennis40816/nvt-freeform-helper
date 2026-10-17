using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinateTestPatternServiceTests
{
    private static readonly CoordinateTestPatternArea _area = new(100d, 20d, 180d, 60d);

    [Fact]
    public void GuideLines5x5_RequestsFiveHorizontalAndFiveVerticalGuides()
    {
        var result = CoordinateTestPatternService.Build(CoordinateTestPatternKind.GuideLines5x5, _area);

        Assert.Equal((5, 5), (result.HorizontalGuideCount, result.VerticalGuideCount));
        Assert.Empty(result.CustomPoints);
        Assert.Empty(result.CustomPaths);
    }

    [Fact]
    public void Diagonals_RunCornerToCornerInBothDirections()
    {
        var result = CoordinateTestPatternService.Build(CoordinateTestPatternKind.Diagonals, _area, diagonalStepCount: 7);

        Assert.Equal(2, result.CustomPaths.Count);
        var rising = result.CustomPaths[0];
        var falling = result.CustomPaths[1];
        Assert.Equal((100d, 20d, 180d, 60d, 7), (rising.StartMachineX, rising.StartMachineY, rising.EndMachineX, rising.EndMachineY, rising.StepCount));
        Assert.Equal((100d, 60d, 180d, 20d, 7), (falling.StartMachineX, falling.StartMachineY, falling.EndMachineX, falling.EndMachineY, falling.StepCount));
        Assert.NotEqual(rising.Key, falling.Key);
    }

    [Fact]
    public void Points13_AreTheCheckerboardNodesOfAFiveByFiveLattice()
    {
        var result = CoordinateTestPatternService.Build(CoordinateTestPatternKind.Points13, _area);

        Assert.Equal(13, result.CustomPoints.Count);
        Assert.Equal(13, result.CustomPoints.Select(static point => point.Key).Distinct().Count());
        var rows = result.CustomPoints
            .GroupBy(static point => point.MachineY)
            .OrderBy(static group => group.Key)
            .Select(static group => group.Select(static point => point.MachineX).Order().ToArray())
            .ToArray();
        Assert.Equal(5, rows.Length);
        Assert.Equal([100d, 140d, 180d], rows[0]);
        Assert.Equal([120d, 160d], rows[1]);
        Assert.Equal([100d, 140d, 180d], rows[2]);
        Assert.Equal([120d, 160d], rows[3]);
        Assert.Equal([100d, 140d, 180d], rows[4]);
        Assert.Equal([20d, 30d, 40d, 50d, 60d], result.CustomPoints.Select(static point => point.MachineY).Distinct().Order().ToArray());
    }

    [Fact]
    public void Points13_WithInset_PullEveryPointAwayFromTheEdge()
    {
        var result = CoordinateTestPatternService.Build(CoordinateTestPatternKind.Points13, _area, inset: 4d);

        Assert.Equal(104d, result.CustomPoints.Min(static point => point.MachineX));
        Assert.Equal(176d, result.CustomPoints.Max(static point => point.MachineX));
        Assert.Equal(24d, result.CustomPoints.Min(static point => point.MachineY));
        Assert.Equal(56d, result.CustomPoints.Max(static point => point.MachineY));
    }

    [Fact]
    public void Points13_FlowThroughThePlannerAsThirteenCustomPoints()
    {
        var pattern = CoordinateTestPatternService.Build(CoordinateTestPatternKind.Points13, _area);
        var request = new CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 200d,
            MachineHeight: 100d,
            PixelWidth: 2000,
            PixelHeight: 1000,
            HorizontalGuideCount: pattern.HorizontalGuideCount,
            VerticalGuideCount: pattern.VerticalGuideCount,
            CopperPillarDiameter: 0d,
            ShowBistRectangle: false,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 0d,
            CustomArrayTopLeftMachineY: 0d,
            CustomArrayTopRightMachineX: 200d,
            CustomArrayTopRightMachineY: 0d,
            CustomArrayBottomRightMachineX: 200d,
            CustomArrayBottomRightMachineY: 100d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 100d,
            CustomPoints: pattern.CustomPoints,
            CustomPaths: pattern.CustomPaths);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(0d, 0d, 200d, 100d), request);

        Assert.Equal(13, snapshot.Points.Count(static point => point.Kind == CoordinatePlannerPointKind.CustomPoint));
    }

    [Fact]
    public void Build_EmptyAreaAfterInset_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CoordinateTestPatternService.Build(CoordinateTestPatternKind.Points13, _area, inset: 20d));
    }

    [Theory]
    [InlineData(-1d, 10)]
    [InlineData(0d, 1)]
    public void Build_InvalidInsetOrStepCount_Throws(double inset, int steps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoordinateTestPatternService.Build(CoordinateTestPatternKind.Diagonals, _area, inset, steps));
    }
}
