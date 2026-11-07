// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PreviewImageFileWriterBehaviorTests
{
    [AvaloniaFact]
    public async Task SavePreviewPng_OverwritesLongerFileWithDecodablePixelsAndNoOldSuffixAsync()
    {
        using var bitmap = CreateBitmap();
        var path = TestFiles.NewTempFilePath("preview-", ".png");
        TestFiles.WriteBytes(path, new byte[8192]);

        await PreviewImageWriter.SaveToFileAsync(bitmap, path);

        var bytes = await TestFiles.ReadBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }, bytes[^12..]);
        AssertSamplePixels(bytes);
    }

    [AvaloniaFact]
    public async Task SavePreviewBmp_UppercaseExtensionWritesBmpFileWithOriginalSizeAndColorsAsync()
    {
        using var bitmap = CreateBitmap();
        var path = TestFiles.NewTempFilePath("preview-", ".BMP");

        await PreviewImageWriter.SaveToFileAsync(bitmap, path);

        var bytes = await TestFiles.ReadBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
        AssertSamplePixels(bytes);
    }

    private static Bitmap CreateBitmap()
    {
        using var source = new SKBitmap(3, 2, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.White);
        source.SetPixel(0, 0, new SKColor(10, 200, 30));
        source.SetPixel(2, 1, new SKColor(220, 40, 60));
        using var image = SKImage.FromBitmap(source);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(encoded.ToArray());
        return new Bitmap(stream);
    }

    private static void AssertSamplePixels(byte[] bytes)
    {
        using var actual = SKBitmap.Decode(bytes);
        Assert.NotNull(actual);
        Assert.Equal(3, actual.Width);
        Assert.Equal(2, actual.Height);
        Assert.Equal(new SKColor(10, 200, 30), actual.GetPixel(0, 0));
        Assert.Equal(new SKColor(220, 40, 60), actual.GetPixel(2, 1));
        Assert.Equal(SKColors.White, actual.GetPixel(1, 0));
    }
}
