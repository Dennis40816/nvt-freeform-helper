// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using Nvt.Core.IO;
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

    /// <summary>
    /// Encodes the image, then publishes the file with <see cref="AtomicOutput"/>. A failed encoding writes nothing.
    /// A failed write leaves the old file or no file, never a truncated one.
    /// </summary>
    public static async Task SaveToFileAsync(CornerCircleImage image, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var encoded = new MemoryStream();
        Encode(image, encoded, path);

        // AtomicOutput names its temporary file in a failure. The status text must show the target the user picked.
        try
        {
            await AtomicOutput.WriteBytesAsync(path, encoded.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"{ex.Message} (export target '{path}')", ex);
        }
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
