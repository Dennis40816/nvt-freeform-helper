using FreeformHelper.UI.ViewModels;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Encodes a rendered layer image as PNG, JPG or BMP.
/// </summary>
public sealed class DxfLayerImageExportService
{
    /// <summary>
    /// Encodes the bitmap to the stream in the requested format. BMP is written by <see cref="SkiaBmpEncoder"/>
    /// because Skia has no BMP encoder.
    /// </summary>
    public static void EncodeImage(SKBitmap bitmap, Stream output, DxfLayerImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(output);
        if (format == DxfLayerImageFormat.Bmp)
        {
            SkiaBmpEncoder.Write(bitmap, output);
            return;
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            format == DxfLayerImageFormat.Jpg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png,
            format == DxfLayerImageFormat.Jpg ? 95 : 100);

        if (data is null)
        {
            throw new InvalidOperationException("Failed to encode DXF layer image.");
        }

        data.SaveTo(output);
    }
}
