// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CornerCircleImageRendererTests
{
    // Hand-computed for a 20 x 20 image and r = 5. Each circle fills a 10 x 10 block, because a pixel is white
    // when its center (i + 0.5, j + 0.5) is within 5 of the circle center. Row 0 of a block: dy = 4.5, so |dx| <= 2.17.
    private static readonly string[] _expectedCircleRows =
    [
        "...####...",
        ".########.",
        ".########.",
        "##########",
        "##########",
        "##########",
        "##########",
        ".########.",
        ".########.",
        "...####...",
    ];

    [Fact]
    public void Render_UsesTheExactActiveAreaSize()
    {
        var layout = LayoutFor(1920, 1080, 100);

        var image = CornerCircleImageRenderer.Render(1920, 1080, layout);

        Assert.Equal(1920, image.Width);
        Assert.Equal(1080, image.Height);
        Assert.Equal(1920 * 1080, image.Pixels.Length);
    }

    [Fact]
    public void Render_DrawsFourWhiteCirclesOnBlackPixelForPixel()
    {
        var image = CornerCircleImageRenderer.Render(20, 20, LayoutFor(20, 20, 5));

        var expected = new StringBuilder();
        for (var row = 0; row < 20; row++)
        {
            var circleRow = _expectedCircleRows[row % 10];
            expected.Append(circleRow).Append(circleRow).Append('\n');
        }

        Assert.Equal(expected.ToString(), ToText(image));
    }

    [Fact]
    public void Render_TouchesEveryImageEdgeAtTheCircleTangentPoint()
    {
        var image = CornerCircleImageRenderer.Render(1920, 1080, LayoutFor(1920, 1080, 100));

        Assert.Equal(255, image.Pixels[100]);
        Assert.Equal(255, image.Pixels[(100 * 1920) + 0]);
        Assert.Equal(255, image.Pixels[(1079 * 1920) + 1819]);
        Assert.Equal(255, image.Pixels[(979 * 1920) + 1919]);
        Assert.Equal(0, image.Pixels[(540 * 1920) + 960]);
    }

    [Fact]
    public void Render_WithRadiusZero_DrawsNothing()
    {
        var image = CornerCircleImageRenderer.Render(8, 6, LayoutFor(8, 6, 0));

        Assert.DoesNotContain((byte)255, image.Pixels);
    }

    [Fact]
    public void Render_WithCenterRectangle_AddsOnePixelWideEdgesThroughTheCenters()
    {
        var layout = LayoutFor(40, 40, 5);

        var plain = CornerCircleImageRenderer.Render(40, 40, layout);
        var image = CornerCircleImageRenderer.Render(40, 40, layout, drawCenterRectangle: true);

        Assert.Equal(0, plain.Pixels[(5 * 40) + 20]);
        Assert.Equal(255, image.Pixels[(5 * 40) + 20]);
        Assert.Equal(255, image.Pixels[(35 * 40) + 20]);
        Assert.Equal(255, image.Pixels[(20 * 40) + 5]);
        Assert.Equal(255, image.Pixels[(20 * 40) + 35]);
        Assert.Equal(0, image.Pixels[(20 * 40) + 20]);
        Assert.Equal(0, image.Pixels[(6 * 40) + 20]);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    [InlineData(40000, 10)]
    public void Render_WithASizeOutsideTheRange_Throws(int width, int height)
    {
        var layout = LayoutFor(20, 20, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => CornerCircleImageRenderer.Render(width, height, layout));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(32768, 8192, true)]
    [InlineData(32768, 32768, false)]
    [InlineData(32769, 1, false)]
    [InlineData(0, 5, false)]
    public void IsSupportedSize_MatchesTheRenderLimits(int width, int height, bool expected)
    {
        Assert.Equal(expected, CornerCircleImageRenderer.IsSupportedSize(width, height));
    }

    [Fact]
    public void Render_WithALayoutThatDoesNotFit_Throws()
    {
        var layout = LayoutFor(20, 20, 50);

        Assert.Equal(CornerCircleLayoutStatus.DoesNotFit, layout.Status);
        Assert.Throws<ArgumentException>(() => CornerCircleImageRenderer.Render(20, 20, layout));
    }

    private static CornerCircleLayoutResult LayoutFor(int width, int height, double radius)
    {
        var area = new Polygon2([new Point2(0, 0), new Point2(width, 0), new Point2(width, height), new Point2(0, height)]);
        return CornerCircleLayoutCalculator.Calculate(new CornerCircleLayoutRequest(area, radius));
    }

    private static string ToText(CornerCircleImage image)
    {
        var text = new StringBuilder();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                text.Append(image.Pixels[(y * image.Width) + x] == 255 ? '#' : '.');
            }

            text.Append('\n');
        }

        return text.ToString();
    }
}
