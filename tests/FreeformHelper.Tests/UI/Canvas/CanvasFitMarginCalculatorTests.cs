using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CanvasFitMarginCalculatorTests
{
    private static double Fallback(string key, double fallback) => fallback;

    [Fact]
    public void Calculate_WithoutPads_AddsTheOverlayInsetToTheTopMarginOnly()
    {
        var margins = CanvasFitMarginCalculator.Calculate(null, overlayTopInset: 44d, Fallback);

        Assert.Equal(new CanvasFitMargins(20d, 64d, 20d, 20d), margins);
    }

    [Fact]
    public void Calculate_WithNegativeInset_TreatsItAsZero()
    {
        var margins = CanvasFitMarginCalculator.Calculate([], overlayTopInset: -10d, Fallback);

        Assert.Equal(new CanvasFitMargins(20d, 20d, 20d, 20d), margins);
    }

    [Fact]
    public void Calculate_UsesTheResourceLookupForTheBaseMargin()
    {
        var margins = CanvasFitMarginCalculator.Calculate(
            null,
            overlayTopInset: 0d,
            static (key, fallback) => key == "Space20" ? 30d : fallback);

        Assert.Equal(new CanvasFitMargins(30d, 30d, 30d, 30d), margins);
    }

    [Fact]
    public void Calculate_WithOneIcGroup_LeavesRoomForTheRulerLabelsOnly()
    {
        var pads = TestGeometryFactory.CreateRegularGrid(rows: 20, cols: 3).Pads;

        var margins = CanvasFitMarginCalculator.Calculate(pads, overlayTopInset: 0d, Fallback);

        // Left: 20 base + 9.6 side margin + 15.6 for two row digits + 2 safety.
        // Top: 20 base + 13.2 label margin + 12 label height + 2 safety.
        AssertMargins(new CanvasFitMargins(47.2d, 47.2d, 20d, 20d), margins);
    }

    [Fact]
    public void Calculate_WithSeveralIcGroupsInTheFirstRow_AddsTheIcGroupBlocksToTheTopMargin()
    {
        var pads = TestGeometryFactory.CreateRegularGrid(rows: 20, cols: 3, icIndexSelector: (_, col) => col).Pads;

        var margins = CanvasFitMarginCalculator.Calculate(pads, overlayTopInset: 0d, Fallback);

        // The block adds 4 padding and 20 height to the top margin of the single IC group case.
        AssertMargins(new CanvasFitMargins(47.2d, 71.2d, 20d, 20d), margins);
    }

    [Theory]
    [InlineData(1, 47.2d)]
    [InlineData(3, 71.2d)]
    public void Calculate_WithPads_AddsTheOverlayInsetToTheTopMarginOnly(int icGroups, double topWithoutInset)
    {
        var pads = TestGeometryFactory.CreateRegularGrid(rows: 20, cols: 3, icIndexSelector: (_, col) => col % icGroups).Pads;

        var margins = CanvasFitMarginCalculator.Calculate(pads, overlayTopInset: 44d, Fallback);

        AssertMargins(new CanvasFitMargins(47.2d, topWithoutInset + 44d, 20d, 20d), margins);
    }

    private static void AssertMargins(CanvasFitMargins expected, CanvasFitMargins actual)
    {
        Assert.Equal(expected.Left, actual.Left, 6);
        Assert.Equal(expected.Top, actual.Top, 6);
        Assert.Equal(expected.Right, actual.Right, 6);
        Assert.Equal(expected.Bottom, actual.Bottom, 6);
    }
}
