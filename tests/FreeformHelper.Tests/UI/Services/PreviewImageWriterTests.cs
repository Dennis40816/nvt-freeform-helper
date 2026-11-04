using System.Buffers.Binary;
using FreeformHelper.UI.Services;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PreviewImageWriterTests
{
    [Theory]
    [InlineData("coordinate_planner.bmp", true)]
    [InlineData("coordinate_planner.BMP", true)]
    [InlineData("coordinate_planner.png", false)]
    [InlineData("coordinate_planner", false)]
    [InlineData("coordinate_planner.bmp.png", false)]
    public void IsBmpPath_FollowsTheExtension(string path, bool expected)
    {
        Assert.Equal(expected, PreviewImageWriter.IsBmpPath(path));
    }

    [Fact]
    public void ConvertPngToBmp_KeepsSizeAndPixelColors()
    {
        using var source = new SKBitmap(6, 4, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.White);
        source.SetPixel(1, 1, new SKColor(10, 200, 30));
        using var png = new MemoryStream();
        using (var image = SKImage.FromBitmap(source))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            data.SaveTo(png);
        }

        png.Position = 0;
        using var bmp = new MemoryStream();

        PreviewImageWriter.ConvertPngToBmp(png, bmp);

        var bytes = bmp.ToArray();
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal(6, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18)));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22)));
        using var decoded = SKBitmap.Decode(bytes);
        Assert.NotNull(decoded);
        Assert.Equal(new SKColor(10, 200, 30), decoded.GetPixel(1, 1));
        Assert.Equal(SKColors.White, decoded.GetPixel(0, 0));
    }

    [Fact]
    public void ConvertPngToBmp_RejectsDataThatIsNotAnImage()
    {
        using var notAnImage = new MemoryStream([1, 2, 3, 4]);
        using var bmp = new MemoryStream();

        Assert.Throws<InvalidOperationException>(() => PreviewImageWriter.ConvertPngToBmp(notAnImage, bmp));
        Assert.Equal(0, bmp.Length);
    }

    [Theory]
    [InlineData("coordinate_planner.bmp", new byte[] { 0x42, 0x4D })]
    [InlineData("coordinate_planner.BMP", new byte[] { 0x42, 0x4D })]
    [InlineData("coordinate_planner.png", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData("coordinate_planner.jpg", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData("coordinate_planner", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData("coordinate_planner.bmp.png", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    public void Encode_FollowsTheSavedExtensionAndKeepsPngForOtherNames(string path, byte[] signature)
    {
        using var output = new MemoryStream();

        PreviewImageWriter.Encode(WriteSamplePng, output, path);

        Assert.Equal(signature, output.ToArray()[..signature.Length]);
    }

    [Fact]
    public void Encode_WritesNothingWhenTheBmpConversionFails()
    {
        using var output = new MemoryStream();

        Assert.Throws<InvalidOperationException>(() =>
            PreviewImageWriter.Encode(stream => stream.Write([1, 2, 3, 4]), output, "coordinate_planner.bmp"));
        Assert.Equal(0, output.Length);
    }

    private static void WriteSamplePng(Stream stream)
    {
        using var source = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(source);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
    }
}
