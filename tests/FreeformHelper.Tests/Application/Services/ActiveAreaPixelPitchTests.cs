using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ActiveAreaPixelPitchTests
{
    [Fact]
    public void Evaluate_ReturnsNotSetWhenBothCountsAreMissing()
    {
        var result = ActiveAreaPixelPitch.Evaluate(915.52, 119.59, null, null);

        Assert.Equal(ActiveAreaPixelsStatus.NotSet, result.Status);
        Assert.False(result.IsUsable);
        Assert.Equal(0, result.PixelsX);
        Assert.Equal(0, result.PixelsY);
    }

    [Theory]
    [InlineData(6480, null)]
    [InlineData(null, 848)]
    public void Evaluate_ReturnsIncompleteWhenOnlyOneCountIsSet(int? pixelsX, int? pixelsY)
    {
        var result = ActiveAreaPixelPitch.Evaluate(915.52, 119.59, pixelsX, pixelsY);

        Assert.Equal(ActiveAreaPixelsStatus.Incomplete, result.Status);
        Assert.False(result.IsUsable);
        Assert.Equal(0, result.PixelsX);
        Assert.Equal(0, result.PixelsY);
    }

    [Theory]
    [InlineData(0, 848)]
    [InlineData(6480, -1)]
    [InlineData(100_001, 848)]
    [InlineData(6480, 100_001)]
    public void Evaluate_ReturnsInvalidWhenACountIsOutOfRange(int pixelsX, int pixelsY)
    {
        var result = ActiveAreaPixelPitch.Evaluate(915.52, 119.59, pixelsX, pixelsY);

        Assert.Equal(ActiveAreaPixelsStatus.Invalid, result.Status);
        Assert.False(result.IsUsable);
    }

    [Fact]
    public void Evaluate_AcceptsThe3635PanelAndReportsTheSamePitchInBothAxes()
    {
        var result = ActiveAreaPixelPitch.Evaluate(915.52, 119.59, 6480, 848);

        Assert.Equal(ActiveAreaPixelsStatus.Ok, result.Status);
        Assert.True(result.IsUsable);
        Assert.Equal(6480, result.PixelsX);
        Assert.Equal(848, result.PixelsY);
        Assert.Equal(915.52 / 6480, result.PitchXMm, 9);
        Assert.Equal(119.59 / 848, result.PitchYMm, 9);
        Assert.True(result.RelativeDifference < ActiveAreaPixelPitch.MismatchThreshold);
    }

    [Fact]
    public void Evaluate_WarnsWhenThePitchDiffersBetweenAxes()
    {
        var result = ActiveAreaPixelPitch.Evaluate(915.52, 119.59, 6480, 600);

        Assert.Equal(ActiveAreaPixelsStatus.PitchMismatch, result.Status);
        Assert.True(result.IsUsable);
        Assert.True(result.RelativeDifference > ActiveAreaPixelPitch.MismatchThreshold);
    }

    [Fact]
    public void Evaluate_KeepsTheCountsWhenTheAreaSizeIsNotPositive()
    {
        var result = ActiveAreaPixelPitch.Evaluate(0, 119.59, 6480, 848);

        Assert.Equal(ActiveAreaPixelsStatus.Ok, result.Status);
        Assert.Equal(6480, result.PixelsX);
        Assert.Equal(0, result.PitchXMm);
        Assert.Equal(0, result.PitchYMm);
    }

    [Fact]
    public void Evaluate_TreatsADifferenceOfExactlyTwoPercentAsOk()
    {
        // 0.98 mm against 1 mm is exactly 2 percent, yet the floating-point difference is slightly above 0.02.
        var result = ActiveAreaPixelPitch.Evaluate(98, 100, 100, 100);

        Assert.Equal(ActiveAreaPixelsStatus.Ok, result.Status);
    }

    [Fact]
    public void Evaluate_ReportsAMismatchJustAboveTwoPercent()
    {
        var result = ActiveAreaPixelPitch.Evaluate(97.9, 100, 100, 100);

        Assert.Equal(ActiveAreaPixelsStatus.PitchMismatch, result.Status);
    }
}
