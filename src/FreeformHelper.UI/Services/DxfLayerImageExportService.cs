using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Renders one DXF layer (CAD pads) to raster image with fixed layer-bounds resolution.
/// </summary>
public sealed class DxfLayerImageExportService
{
    public static void ExportLayerImage(
        IReadOnlyList<CadPad> pads,
        int widthPixels,
        int heightPixels,
        decimal lineWidthPixels,
        int paddingXPixels,
        int paddingYPixels,
        DxfLayerImageFormat format,
        bool useDarkTheme,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(pads);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (pads.Count == 0)
        {
            throw new InvalidOperationException("No CAD pads in selected layer.");
        }

        var request = new LayerImageRenderRequest(
            new[] { new LayerImageLayer(pads[0].Layer, pads) },
            widthPixels, heightPixels, paddingXPixels, paddingYPixels, lineWidthPixels, useDarkTheme);
        using var bitmap = LayerImageRenderer.Render(request);
        SaveImage(bitmap, outputPath, format);
    }

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

    private static void SaveImage(SKBitmap bitmap, string outputPath, DxfLayerImageFormat format)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var encoded = new MemoryStream();
        EncodeImage(bitmap, encoded, format);

        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoded.Position = 0;
        encoded.CopyTo(stream);
    }
}
