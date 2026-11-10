// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CornerCircleImageEncoderTests
{
    [Fact]
    public void Png_HasTheSameSizeAndPixelsAsTheImage()
    {
        var image = BuildImage(64, 48, 10);
        using var stream = new MemoryStream();

        CornerCircleImageEncoder.Encode(image, stream, "circles.png");

        stream.Position = 0;
        using var decoded = SKBitmap.Decode(stream);
        Assert.Equal(64, decoded.Width);
        Assert.Equal(48, decoded.Height);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                Assert.Equal(image.Pixels[(y * image.Width) + x], decoded.GetPixel(x, y).Red);
            }
        }
    }

    [Fact]
    public void Bmp_HasTheSameSizeAndBlackAndWhitePixels()
    {
        var image = BuildImage(64, 48, 10);
        using var stream = new MemoryStream();

        CornerCircleImageEncoder.Encode(image, stream, "circles.BMP");

        var bytes = stream.ToArray();
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
        Assert.Equal(64, BitConverter.ToInt32(bytes, 18));
        Assert.Equal(48, Math.Abs(BitConverter.ToInt32(bytes, 22)));
        stream.Position = 0;
        using var decoded = SKBitmap.Decode(stream);
        Assert.Equal(64, decoded.Width);
        Assert.Equal(48, decoded.Height);
        Assert.Equal(255, decoded.GetPixel(10, 10).Red);
        Assert.Equal(0, decoded.GetPixel(32, 24).Red);
    }

    [Theory]
    [InlineData("a.png", false)]
    [InlineData("a.bmp", true)]
    [InlineData("a.BMP", true)]
    [InlineData("a", false)]
    public void IsBmpPath_FollowsTheExtension(string path, bool expected)
    {
        Assert.Equal(expected, CornerCircleImageEncoder.IsBmpPath(path));
    }

    [Fact]
    public async Task SaveToFileAsync_WritesAFileOfTheImageSize()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "circles.png");

            await CornerCircleImageEncoder.SaveToFileAsync(BuildImage(40, 30, 6), path);

            using var decoded = SKBitmap.Decode(path);
            Assert.Equal(40, decoded.Width);
            Assert.Equal(30, decoded.Height);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static CornerCircleImage BuildImage(int width, int height, double radius)
    {
        var area = new Polygon2([new Point2(0, 0), new Point2(width, 0), new Point2(width, height), new Point2(0, height)]);
        var layout = CornerCircleLayoutCalculator.Calculate(new CornerCircleLayoutRequest(area, radius));
        return CornerCircleImageRenderer.Render(width, height, layout);
    }
}
