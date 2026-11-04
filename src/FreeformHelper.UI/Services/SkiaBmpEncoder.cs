using FreeformHelper.Application.Export;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Encodes a Skia bitmap as a 24-bit BMP. Skia has no BMP encoder, so the file is written by <see cref="BmpWriter"/>.
/// </summary>
public static class SkiaBmpEncoder
{
    private static readonly SKColor _background = SKColors.White;

    /// <summary>
    /// Writes the bitmap to the stream. Transparent pixels are composited onto white because BMP here has no alpha.
    /// </summary>
    public static void Write(SKBitmap source, Stream output)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        if (source.Width < 1 || source.Height < 1)
        {
            throw new InvalidOperationException("Cannot encode an empty bitmap.");
        }

        using var opaque = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(opaque))
        {
            canvas.Clear(_background);
            canvas.DrawBitmap(source, 0f, 0f);
            canvas.Flush();
        }

        BmpWriter.WriteBgra32AsBmp24(output, opaque.Width, opaque.Height, opaque.GetPixelSpan(), opaque.RowBytes);
    }
}
