// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageSizeTests
{
    [Fact]
    public void CreateDefault_ReturnsProjectPixelsFor6480By848()
    {
        var size = LayerImageSize.CreateDefault(915.52, 119.59, 6480, 848);

        Assert.Equal(LayerImageSizeSource.Project, size.Source);
        Assert.Equal(6480, size.PixelsX);
        Assert.Equal(848, size.PixelsY);
        Assert.Equal(0, size.PaddingX);
        Assert.Equal(0, size.PaddingY);
        Assert.False(size.KeepRatio);
    }

    [Fact]
    public void CreateDefault_ReturnsAutoLandscapePixelsWithoutProjectCounts()
    {
        var size = LayerImageSize.CreateDefault(915.52, 119.59, null, null);

        Assert.Equal(LayerImageSizeSource.Auto, size.Source);
        Assert.Equal(4096, size.PixelsX);
        Assert.Equal((int)Math.Round(4096 * 119.59 / 915.52), size.PixelsY);
    }

    [Fact]
    public void CreateDefault_ReturnsAutoPortraitPixels()
    {
        var size = LayerImageSize.CreateDefault(119.59, 915.52, null, null);

        Assert.Equal(LayerImageSizeSource.Auto, size.Source);
        Assert.Equal((int)Math.Round(4096 * 119.59 / 915.52), size.PixelsX);
        Assert.Equal(4096, size.PixelsY);
    }

    [Theory]
    [InlineData(6480, null)]
    [InlineData(null, 848)]
    [InlineData(1, 848)]
    [InlineData(6480, 1)]
    [InlineData(100_001, 848)]
    [InlineData(6480, 100_001)]
    public void CreateDefault_ReturnsAutoForIncompleteOrOutOfRangeProjectCounts(int? x, int? y)
    {
        var size = LayerImageSize.CreateDefault(2, 1, x, y);

        Assert.Equal(LayerImageSizeSource.Auto, size.Source);
        Assert.Equal(4096, size.PixelsX);
        Assert.Equal(2048, size.PixelsY);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.NaN)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(1, double.NegativeInfinity)]
    public void CreateDefault_ReturnsSquareForDegenerateBounds(double width, double height)
    {
        var size = LayerImageSize.CreateDefault(width, height, null, null);

        Assert.Equal(LayerImageSizeSource.Auto, size.Source);
        Assert.Equal(4096, size.PixelsX);
        Assert.Equal(4096, size.PixelsY);
    }

    [Theory]
    [InlineData(2, 100_000)]
    [InlineData(100_000, 2)]
    public void CreateDefault_AcceptsProjectLimitsEvenWithInvalidBounds(int x, int y)
    {
        var size = LayerImageSize.CreateDefault(double.NaN, 0, x, y);

        Assert.Equal(LayerImageSizeSource.Project, size.Source);
        Assert.Equal(x, size.PixelsX);
        Assert.Equal(y, size.PixelsY);
    }

    [Theory]
    [InlineData(double.MaxValue, double.MaxValue, 4096, 4096)]
    [InlineData(double.MaxValue, double.Epsilon, 4096, 2)]
    [InlineData(double.Epsilon, double.MaxValue, 2, 4096)]
    public void CreateDefault_ClampsExtremeFiniteBounds(double width, double height, int x, int y)
    {
        var size = LayerImageSize.CreateDefault(width, height, null, null);

        Assert.Equal(x, size.PixelsX);
        Assert.Equal(y, size.PixelsY);
    }

    [Fact]
    public void OutputSize_IncludesPaddingOnBothSides()
    {
        var size = LayerImageSize.CreateDefault(2, 1, 4096, 542).WithPaddingX(200).WithPaddingY(100);

        Assert.Equal(4496, size.OutputWidth);
        Assert.Equal(742, size.OutputHeight);
        Assert.Equal(LayerImageSizeSource.Custom, size.Source);
    }

    [Theory]
    [InlineData(int.MinValue, 0)]
    [InlineData(int.MaxValue, 100_000)]
    public void WithPadding_ClampsBothAxes(int padding, int expected)
    {
        var size = LayerImageSize.CreateDefault(2, 1, null, null).WithPaddingX(padding).WithPaddingY(padding);

        Assert.Equal(expected, size.PaddingX);
        Assert.Equal(expected, size.PaddingY);
        Assert.Equal(LayerImageSizeSource.Custom, size.Source);
    }

    [Fact]
    public void OutputSize_LargeValuesDoNotOverflow()
    {
        var size = LayerImageSize.CreateDefault(1, 1, null, null)
            .WithPixelsX(int.MaxValue).WithPixelsY(int.MaxValue)
            .WithPaddingX(int.MaxValue).WithPaddingY(int.MaxValue);

        Assert.Equal(300_000, size.OutputWidth);
        Assert.Equal(300_000, size.OutputHeight);
    }

    [Fact]
    public void WithPixelsX_KeepRatioDrivesYFromTheDefaultPixels()
    {
        var original = LayerImageSize.CreateDefault(915.52, 119.59, 6480, 848).WithKeepRatio(true);

        var edited = original.WithPixelsX(3240);

        Assert.Equal(3240, edited.PixelsX);
        Assert.Equal(424, edited.PixelsY);
        Assert.Equal(LayerImageSizeSource.Custom, edited.Source);
        Assert.Equal(6480, original.PixelsX);
        Assert.Equal(848, original.PixelsY);
    }

    [Fact]
    public void WithPixelsY_KeepRatioDrivesXFromTheDefaultPixels()
    {
        var size = LayerImageSize.CreateDefault(915.52, 119.59, 6480, 848).WithKeepRatio(true).WithPixelsY(424);

        Assert.Equal(3240, size.PixelsX);
        Assert.Equal(424, size.PixelsY);
        Assert.Equal(LayerImageSizeSource.Custom, size.Source);
    }

    [Fact]
    public void WithPixels_KeepRatioOffLeavesAxesIndependent()
    {
        var original = LayerImageSize.CreateDefault(2, 1, 200, 100);

        var editedX = original.WithPixelsX(500);
        var editedY = original.WithPixelsY(600);

        Assert.Equal(500, editedX.PixelsX);
        Assert.Equal(100, editedX.PixelsY);
        Assert.Equal(200, editedY.PixelsX);
        Assert.Equal(600, editedY.PixelsY);
        Assert.Equal(LayerImageSizeSource.Custom, editedX.Source);
        Assert.Equal(LayerImageSizeSource.Custom, editedY.Source);
    }

    [Theory]
    [InlineData(int.MinValue, 2)]
    [InlineData(int.MaxValue, 100_000)]
    public void WithPixels_ClampsRawInputBeforeApplyingTheRatio(int input, int expected)
    {
        var original = LayerImageSize.CreateDefault(1, 1, 100, 100).WithKeepRatio(true);

        var editedX = original.WithPixelsX(input);
        var editedY = original.WithPixelsY(input);

        Assert.Equal(expected, editedX.PixelsX);
        Assert.Equal(expected, editedX.PixelsY);
        Assert.Equal(expected, editedY.PixelsX);
        Assert.Equal(expected, editedY.PixelsY);
    }

    [Theory]
    [InlineData(2, 100_000, 100_000, 100_000)]
    [InlineData(100_000, 2, 2, 2)]
    public void WithPixelsX_ClampsTheDerivedAxis(int defaultX, int defaultY, int input, int expectedY)
    {
        var size = LayerImageSize.CreateDefault(1, 1, defaultX, defaultY).WithKeepRatio(true).WithPixelsX(input);

        Assert.Equal(input, size.PixelsX);
        Assert.Equal(expectedY, size.PixelsY);
    }

    [Theory]
    [InlineData(100_000, 2, 100_000, 100_000)]
    [InlineData(2, 100_000, 2, 2)]
    public void WithPixelsY_ClampsTheDerivedAxis(int defaultX, int defaultY, int input, int expectedX)
    {
        var size = LayerImageSize.CreateDefault(1, 1, defaultX, defaultY).WithKeepRatio(true).WithPixelsY(input);

        Assert.Equal(expectedX, size.PixelsX);
        Assert.Equal(input, size.PixelsY);
    }

    [Fact]
    public void WithKeepRatio_DoesNotChangePixelsOrSource()
    {
        var original = LayerImageSize.CreateDefault(2, 1, 200, 100).WithPixelsY(300);

        var enabled = original.WithKeepRatio(true);

        Assert.Equal(200, enabled.PixelsX);
        Assert.Equal(300, enabled.PixelsY);
        Assert.Equal(original.Source, enabled.Source);
        Assert.Equal(original, enabled.WithKeepRatio(false));
    }

    [Fact]
    public void WithPixelsX_KeepsTheOriginalRatioAfterIndependentEdits()
    {
        var size = LayerImageSize.CreateDefault(2, 1, 200, 100)
            .WithPixelsY(300).WithKeepRatio(true).WithPixelsX(400);

        Assert.Equal(400, size.PixelsX);
        Assert.Equal(200, size.PixelsY);
    }

    [Theory]
    [InlineData(2, 5, 3, 8)]
    [InlineData(2, 3, 3, 4)]
    public void WithPixelsX_RoundsMidpointsToEven(int defaultX, int defaultY, int input, int expectedY)
    {
        var size = LayerImageSize.CreateDefault(1, 1, defaultX, defaultY).WithKeepRatio(true).WithPixelsX(input);

        Assert.Equal(expectedY, size.PixelsY);
    }

    [Theory]
    [InlineData(6480, 848, true)]
    [InlineData(null, null, false)]
    [InlineData(null, null, true)]
    public void Reset_RestoresDefaultPixelsSourceAndPaddingWhileKeepingRatio(int? projectX, int? projectY, bool keepRatio)
    {
        var original = LayerImageSize.CreateDefault(915.52, 119.59, projectX, projectY);
        var edited = original.WithPixelsX(200).WithPixelsY(300).WithPaddingX(20).WithPaddingY(30).WithKeepRatio(keepRatio);

        var reset = edited.Reset();

        Assert.Equal(original.WithKeepRatio(keepRatio), reset);
        Assert.Equal(original.Source, reset.Source);
        Assert.Equal(0, reset.PaddingX);
        Assert.Equal(0, reset.PaddingY);
        Assert.Equal(keepRatio, reset.KeepRatio);
    }

    [Fact]
    public void CreateDefault_EquivalentInputsHaveValueEquality()
    {
        var first = LayerImageSize.CreateDefault(2, 1, 200, 100);
        var second = LayerImageSize.CreateDefault(4, 3, 200, 100);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
