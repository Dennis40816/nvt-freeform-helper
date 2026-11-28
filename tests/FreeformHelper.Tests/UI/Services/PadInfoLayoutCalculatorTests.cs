using Avalonia;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInfoLayoutCalculatorTests
{
    [Fact]
    public void BuildBounds_EmptyInput_ReturnsZeroBounds()
    {
        var bounds = PadInfoLayoutCalculator.BuildBounds(Array.Empty<Rect2>());

        Assert.Equal(new Rect2(0, 0, 0, 0), bounds);
    }

    [Fact]
    public void BuildBounds_OneRectangle_ReturnsThatRectangle()
    {
        var rectangle = new Rect2(1, 2, 3, 4);

        var bounds = PadInfoLayoutCalculator.BuildBounds([rectangle]);

        Assert.Equal(rectangle, bounds);
    }

    [Fact]
    public void BuildBounds_SeveralRectangles_ReturnsUnion()
    {
        Rect2[] rectangles = [new(1, 2, 3, 4), new(-2, 1, 2, 5), new(0, -1, 4, 2)];

        var bounds = PadInfoLayoutCalculator.BuildBounds(rectangles);

        Assert.Equal(new Rect2(-2, -1, 4, 5), bounds);
    }

    [Fact]
    public void ChooseAnchor_EmptySelection_ReturnsUnionCenter()
    {
        var anchor = PadInfoLayoutCalculator.ChooseAnchor(new Rect2(0, 0, 8, 6), Array.Empty<Rect2>());

        Assert.Equal(new Point2(4, 3), anchor);
    }

    [Fact]
    public void ChooseAnchor_NoPositiveArea_ReturnsUnionCenter()
    {
        Rect2[] selected = [new(0, 0, 0, 4), new(2, 3, 6, 3)];

        var anchor = PadInfoLayoutCalculator.ChooseAnchor(new Rect2(0, 0, 8, 6), selected);

        Assert.Equal(new Point2(4, 3), anchor);
    }

    [Fact]
    public void ChooseAnchor_UnequalAreas_ReturnsPadCenterNearestWeightedCentroid()
    {
        // Areas 4 and 16 put the centroid at (5, 1), nearest the second pad's center (6, 1).
        Rect2[] selected = [new(0, 0, 2, 2), new(4, -1, 8, 3)];

        var anchor = PadInfoLayoutCalculator.ChooseAnchor(new Rect2(0, -1, 8, 3), selected);

        Assert.Equal(new Point2(6, 1), anchor);
    }

    [Fact]
    public void ChooseAnchor_EqualDistances_KeepsFirstSelectedPad()
    {
        Rect2[] selected = [new(4, 0, 6, 2), new(0, 0, 2, 2)];

        var anchor = PadInfoLayoutCalculator.ChooseAnchor(new Rect2(0, 0, 6, 2), selected);

        Assert.Equal(new Point2(5, 1), anchor);
    }

    [Fact]
    public void ChoosePlacement_PanelFitsRight_PlacesPanelAndLinkOnRight()
    {
        var placement = PadInfoLayoutCalculator.ChoosePlacement(
            new Rect(2, 8, 2, 4), new Size(4, 6), new Rect(0, 0, 20, 20), gap: 1, minLine: 1, lineInset: 1);

        Assert.Equal(new Point(5, 7), placement.panelPoint);
        Assert.Equal(new Point(5, 10), placement.linePoint);
    }

    [Fact]
    public void ChoosePlacement_PanelFitsLeftOnly_PlacesPanelAndLinkOnLeft()
    {
        var placement = PadInfoLayoutCalculator.ChoosePlacement(
            new Rect(16, 8, 2, 4), new Size(4, 6), new Rect(0, 0, 20, 20), gap: 1, minLine: 1, lineInset: 1);

        Assert.Equal(new Point(11, 7), placement.panelPoint);
        Assert.Equal(new Point(15, 10), placement.linePoint);
    }

    [Fact]
    public void ChoosePlacement_EqualOverflow_PrefersRight()
    {
        // Both candidates overflow by 1; the right candidate at 12 is clamped to 11.
        var placement = PadInfoLayoutCalculator.ChoosePlacement(
            new Rect(9, 8, 2, 4), new Size(8, 6), new Rect(0, 0, 20, 20), gap: 1, minLine: 1, lineInset: 1);

        Assert.Equal(new Point(11, 7), placement.panelPoint);
        Assert.Equal(new Point(11, 10), placement.linePoint);
    }

    [Fact]
    public void ChoosePlacement_PanelLargerThanCanvas_ClampsToMinimumCoordinates()
    {
        var placement = PadInfoLayoutCalculator.ChoosePlacement(
            new Rect(4, 5, 2, 2), new Size(12, 10), new Rect(2, 3, 8, 6), gap: 1, minLine: 1, lineInset: 1);

        Assert.Equal(new Point(3, 4), placement.panelPoint);
        Assert.Equal(new Point(3, 6), placement.linePoint);
    }

    [Fact]
    public void ChoosePlacement_HorizontalClampContainsAnchor_UsesVerticalFallback()
    {
        // X is fixed at 1; moving above the anchor puts the panel at Y = 1 and the link at Y = 4.
        var placement = PadInfoLayoutCalculator.ChoosePlacement(
            new Rect(4, 4, 2, 4), new Size(8, 4), new Rect(0, 0, 10, 20), gap: 1, minLine: 1, lineInset: 1);

        Assert.Equal(new Point(1, 1), placement.panelPoint);
        Assert.Equal(new Point(1, 4), placement.linePoint);
    }

    [Theory]
    [InlineData(10, 3, 5, 10)] // The horizontal distance 3 already meets the minimum.
    [InlineData(10, 5, 5, 6)] // Distance 3 needs vertical distance 4, giving a 3-4-5 triangle.
    [InlineData(6, 5, 7, 10)] // Allowed Y is 8..12; neither Y = 6 nor Y = 14 fits.
    public void ChoosePlacement_MinimumLineLength_KeepsOrExtendsEndpointWithinPanel(
        double panelHeight, double minLine, double expectedPanelY, double expectedLineY)
    {
        var placement = PadInfoLayoutCalculator.ChoosePlacement(
            new Rect(1, 9, 2, 2), new Size(4, panelHeight), new Rect(0, 0, 10, 20), gap: 1, minLine: minLine, lineInset: 1);

        Assert.Equal(new Point(5, expectedPanelY), placement.panelPoint);
        Assert.Equal(new Point(5, expectedLineY), placement.linePoint);
    }

    [Theory]
    [InlineData(3, 5, 1, 5)]
    [InlineData(7, 5, 9, 5)]
    [InlineData(5, 3, 5, 1)]
    [InlineData(5, 7, 5, 9)]
    [InlineData(5, 5, 1, 5)]
    public void PushAnchorOutsidePanel_InsideAnchor_MovesToNearestEdgeWithLeftFirstOnTies(
        double anchorX, double anchorY, double expectedX, double expectedY)
    {
        var anchor = PadInfoLayoutCalculator.PushAnchorOutsidePanel(
            new Rect(2, 2, 6, 6), new Point(anchorX, anchorY), margin: 1);

        Assert.Equal(new Point(expectedX, expectedY), anchor);
    }

    [Fact]
    public void PushAnchorOutsidePanel_OutsideAnchor_ReturnsUnchanged()
    {
        var anchor = new Point(1, 5);

        var result = PadInfoLayoutCalculator.PushAnchorOutsidePanel(new Rect(2, 2, 6, 6), anchor, margin: 1);

        Assert.Equal(anchor, result);
    }

    [Theory]
    [InlineData(0, 20, 4, 9)]
    [InlineData(6, 7, 6, 7)]
    public void ClampPointToRect_NormalCanvas_ClampsToInsetBounds(
        double pointX, double pointY, double expectedX, double expectedY)
    {
        var point = PadInfoLayoutCalculator.ClampPointToRect(
            new Point(pointX, pointY), new Rect(2, 3, 10, 8), margin: 2);

        Assert.Equal(new Point(expectedX, expectedY), point);
    }

    [Fact]
    public void ClampPointToRect_CanvasSmallerThanTwiceMargin_ClampsToCenter()
    {
        var point = PadInfoLayoutCalculator.ClampPointToRect(new Point(0, 10), new Rect(2, 3, 4, 2), margin: 3);

        Assert.Equal(new Point(4, 4), point);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(10, 0)]
    public void ClampPointToRect_ZeroExtent_ReturnsUnchanged(double width, double height)
    {
        var point = new Point(0, 20);

        var result = PadInfoLayoutCalculator.ClampPointToRect(point, new Rect(2, 3, width, height), margin: 2);

        Assert.Equal(point, result);
    }

    [Theory]
    [InlineData(2, 2, 4, 4, true)]
    [InlineData(4, 1, 2, 2, false)]
    [InlineData(1, 4, 2, 2, false)]
    [InlineData(1, 1, 0, 2, false)]
    [InlineData(1, 1, 2, 0, false)]
    public void RectsOverlap_RequiresPositiveAreaIntersection(
        double x, double y, double width, double height, bool expected)
    {
        var first = new Rect(0, 0, 4, 4);
        var second = new Rect(x, y, width, height);

        Assert.Equal(expected, PadInfoLayoutCalculator.RectsOverlap(first, second));
        Assert.Equal(expected, PadInfoLayoutCalculator.RectsOverlap(second, first));
    }
}
