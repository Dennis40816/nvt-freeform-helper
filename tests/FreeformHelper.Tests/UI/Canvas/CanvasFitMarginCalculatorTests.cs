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
}
