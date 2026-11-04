using Avalonia.Media.Imaging;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Writes a rendered preview bitmap as PNG or BMP. The format follows the extension of the file the user saved.
/// </summary>
public static class PreviewImageWriter
{
    /// <summary>
    /// Returns true when the path asks for a BMP file. Every other extension keeps the PNG behavior.
    /// </summary>
    public static bool IsBmpPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return string.Equals(Path.GetExtension(path), ".bmp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Encodes the bitmap, then writes the file. The file is created only after encoding succeeded, so a failed
    /// conversion leaves no empty or partial file behind.
    /// </summary>
    public static async Task SaveToFileAsync(Bitmap bitmap, string path)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var encoded = new MemoryStream();
        Encode(png => bitmap.Save(png, PngBitmapEncoderOptions.Default), encoded, path);

        encoded.Position = 0;
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await encoded.CopyToAsync(stream);
    }

    /// <summary>
    /// Writes the image to the stream as BMP when <paramref name="path"/> ends in .bmp, otherwise as PNG.
    /// </summary>
    /// <param name="savePng">Writes the image as PNG to the stream it receives.</param>
    /// <param name="output">Destination stream.</param>
    /// <param name="path">The file path the user chose. Only its extension is used.</param>
    public static void Encode(Action<Stream> savePng, Stream output, string path)
    {
        ArgumentNullException.ThrowIfNull(savePng);
        ArgumentNullException.ThrowIfNull(output);
        if (!IsBmpPath(path))
        {
            savePng(output);
            return;
        }

        using var png = new MemoryStream();
        savePng(png);
        png.Position = 0;
        ConvertPngToBmp(png, output);
    }

    /// <summary>
    /// Decodes a PNG stream and writes it to the output stream as a 24-bit BMP.
    /// </summary>
    public static void ConvertPngToBmp(Stream png, Stream output)
    {
        ArgumentNullException.ThrowIfNull(png);
        ArgumentNullException.ThrowIfNull(output);
        using var decoded = SKBitmap.Decode(png);
        if (decoded is null)
        {
            throw new InvalidOperationException("The preview image could not be decoded.");
        }

        SkiaBmpEncoder.Write(decoded, output);
    }
}
