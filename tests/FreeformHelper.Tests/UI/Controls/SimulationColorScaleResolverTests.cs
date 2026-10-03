using Avalonia.Media;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationColorScaleResolverTests
{
    [Fact]
    public void ResolvePositiveColor_WhenValueIsZero_ReturnsZeroColor()
    {
        var zero = Color.Parse("#2EE66B");
        var mid = Color.Parse("#FFC83D");
        var warm = Color.Parse("#FF9124");
        var high = Color.Parse("#FF4D3D");

        var color = SimulationColorScaleResolver.ResolvePositiveColor(0d, 400d, zero, mid, warm, high);

        Assert.Equal(zero, color);
    }

    [Fact]
    public void ResolvePositiveColor_WhenValueIsLowPositive_StaysNearGreen()
    {
        var zero = Color.Parse("#2EE66B");
        var mid = Color.Parse("#FFC83D");
        var warm = Color.Parse("#FF9124");
        var high = Color.Parse("#FF4D3D");

        var color = SimulationColorScaleResolver.ResolvePositiveColor(30d, 400d, zero, mid, warm, high);

        Assert.Equal(zero, color);
    }

    [Fact]
    public void ResolvePositiveColor_WhenValueIsMidPositive_TrendsWarm()
    {
        var zero = Color.Parse("#2EE66B");
        var mid = Color.Parse("#FFC83D");
        var warm = Color.Parse("#FF9124");
        var high = Color.Parse("#FF4D3D");

        var color = SimulationColorScaleResolver.ResolvePositiveColor(220d, 400d, zero, mid, warm, high);

        Assert.True(color.R >= color.G);
        Assert.True(color.G > color.B);
    }

    [Fact]
    public void ResolvePositiveColor_WhenValueIsHighPositive_TrendsToRed()
    {
        var zero = Color.Parse("#2EE66B");
        var mid = Color.Parse("#FFC83D");
        var warm = Color.Parse("#FF9124");
        var high = Color.Parse("#FF4D3D");

        var color = SimulationColorScaleResolver.ResolvePositiveColor(400d, 400d, zero, mid, warm, high);

        Assert.True(color.R >= color.G);
        Assert.True(color.R > color.B);
    }

    [Fact]
    public void ResolveNegativeColor_WhenValueIsSlightlyNegative_StaysNearGreen()
    {
        var low = Color.Parse("#2D7DFF");
        var zero = Color.Parse("#2EE66B");

        var color = SimulationColorScaleResolver.ResolveNegativeColor(-40d, 200d, low, zero);

        Assert.Equal(zero, color);
    }

    [Fact]
    public void ResolveNegativeColor_WhenValueIsStronglyNegative_TrendsToBlue()
    {
        var low = Color.Parse("#2D7DFF");
        var zero = Color.Parse("#2EE66B");

        var color = SimulationColorScaleResolver.ResolveNegativeColor(-200d, 200d, low, zero);

        Assert.True(color.B > color.G);
        Assert.True(color.B > color.R);
    }

    [Fact]
    public void ComputeAutoScaleRange_UsesPercentileClampInsteadOfRawExtreme()
    {
        var range = SimulationColorScaleResolver.ComputeAutoScaleRange(new[] { -500d, -15d, 0d, 10d, 20d, 40d, 200d });

        Assert.Equal(-500d, range.Minimum);
        Assert.Equal(200d, range.Maximum);
        Assert.True(range.NegativeClampAbs < 500d);
        Assert.True(range.PositiveClamp < 200d);
    }
}
