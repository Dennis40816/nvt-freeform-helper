using Avalonia.Media.Imaging;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Writes a rendered preview bitmap as PNG or BMP. The format follows the file extension.
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
    /// Saves the bitmap to the stream as BMP when <paramref name="path"/> ends in .bmp, otherwise as PNG.
    /// </summary>
    public static void Save(Bitmap bitmap, Stream output, string path)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(output);
        if (!IsBmpPath(path))
        {
            bitmap.Save(output, PngBitmapEncoderOptions.Default);
            return;
        }

        using var png = new MemoryStream();
        bitmap.Save(png, PngBitmapEncoderOptions.Default);
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
