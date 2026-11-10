// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Writes a <see cref="CornerCircleImage"/> as a file. The file has exactly the pixel size of the image, so
/// one file pixel is one active-area pixel.
/// </summary>
public static class CornerCircleImageEncoder
{
    /// <summary>Returns true when the path asks for a BMP file. Every other extension gives PNG.</summary>
    public static bool IsBmpPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return string.Equals(Path.GetExtension(path), ".bmp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Encodes the image as BMP when <paramref name="path"/> ends in .bmp, otherwise as PNG.</summary>
    public static void Encode(CornerCircleImage image, Stream output, string path)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(output);
        if (IsBmpPath(path))
        {
            WriteBmp(image, output);
        }
        else
        {
            WritePng(image, output);
        }
    }

    /// <summary>Encodes the image, then writes the file. A failed encoding leaves no partial file.</summary>
    public static async Task SaveToFileAsync(CornerCircleImage image, string path)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var encoded = new MemoryStream();
        Encode(image, encoded, path);

        encoded.Position = 0;
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await encoded.CopyToAsync(stream);
    }

    private static void WritePng(CornerCircleImage image, Stream output)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        image.Pixels.AsSpan().CopyTo(bitmap.GetPixelSpan());
        using var skImage = SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new InvalidOperationException("Failed to encode the corner circle image.");
        }

        data.SaveTo(output);
    }

    private static void WriteBmp(CornerCircleImage image, Stream output)
    {
        const int bytesPerPixel = 4;
        var bgra = new byte[image.Pixels.Length * bytesPerPixel];
        for (var i = 0; i < image.Pixels.Length; i++)
        {
            var value = image.Pixels[i];
            var offset = i * bytesPerPixel;
            bgra[offset] = value;
            bgra[offset + 1] = value;
            bgra[offset + 2] = value;
            bgra[offset + 3] = byte.MaxValue;
        }

        BmpWriter.WriteBgra32AsBmp24(output, image.Width, image.Height, bgra, image.Width * bytesPerPixel);
    }
}
