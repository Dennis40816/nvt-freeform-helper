using System.Buffers.Binary;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SkiaBmpEncoderTests
{
    [Fact]
    public void Write_ProducesDecodableBmpWithSameSizeAndPixels()
    {
        using var source = new SKBitmap(5, 3, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.White);
        source.SetPixel(0, 0, new SKColor(255, 0, 0));
        source.SetPixel(4, 2, new SKColor(0, 0, 255));
        using var output = new MemoryStream();

        SkiaBmpEncoder.Write(source, output);

        var bytes = output.ToArray();
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22)));
        Assert.Equal(bytes.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2)));
        using var decoded = SKBitmap.Decode(bytes);
        Assert.NotNull(decoded);
        Assert.Equal(5, decoded.Width);
        Assert.Equal(3, decoded.Height);
        Assert.Equal(new SKColor(255, 0, 0), decoded.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 255), decoded.GetPixel(4, 2));
        Assert.Equal(SKColors.White, decoded.GetPixel(2, 1));
    }

    [Fact]
    public void Write_CompositesTransparentPixelsOntoWhite()
    {
        using var source = new SKBitmap(2, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.Transparent);
        source.SetPixel(1, 0, new SKColor(0, 0, 0));
        using var output = new MemoryStream();

        SkiaBmpEncoder.Write(source, output);

        using var decoded = SKBitmap.Decode(output.ToArray());
        Assert.NotNull(decoded);
        Assert.Equal(SKColors.White, decoded.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 0), decoded.GetPixel(1, 0));
    }

    [Theory]
    [InlineData(DxfLayerImageFormat.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData(DxfLayerImageFormat.Bmp, new byte[] { 0x42, 0x4D })]
    [InlineData(DxfLayerImageFormat.Jpg, new byte[] { 0xFF, 0xD8, 0xFF })]
    public void EncodeImage_WritesTheRequestedFileSignature(DxfLayerImageFormat format, byte[] signature)
    {
        using var source = new SKBitmap(8, 8, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.White);
        using var output = new MemoryStream();

        DxfLayerImageExportService.EncodeImage(source, output, format);

        Assert.True(output.Length > signature.Length);
        Assert.Equal(signature, output.ToArray()[..signature.Length]);
    }
}
