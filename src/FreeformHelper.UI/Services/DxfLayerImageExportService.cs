using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Renders one DXF layer (CAD pads) to raster image with fixed layer-bounds resolution.
/// </summary>
public sealed class DxfLayerImageExportService
{
    private const double BoundsEpsilon = 1e-9;
    private static readonly SKColor LightBackground = new(255, 255, 255, 255);
    private static readonly SKColor LightStroke = new(0, 0, 0, 255);
    private static readonly SKColor DarkBackground = new(42, 42, 42, 255);
    private static readonly SKColor DarkStroke = new(255, 255, 255, 255);

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

        if (widthPixels < 2 || heightPixels < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPixels), "Image size must be >= 2x2.");
        }

        if (lineWidthPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineWidthPixels), "Line width must be > 0.");
        }

        if (paddingXPixels < 0 || paddingYPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paddingXPixels), "Padding must be >= 0.");
        }

        var bounds = ComputeBounds(pads);
        if (bounds.IsEmpty || bounds.Width < BoundsEpsilon || bounds.Height < BoundsEpsilon)
        {
            throw new InvalidOperationException("Selected layer bounds are empty.");
        }

        var bg = useDarkTheme ? DarkBackground : LightBackground;
        var stroke = useDarkTheme ? DarkStroke : LightStroke;
        var strokeWidthPixels = (double)lineWidthPixels;
        int outputWidthPixels;
        int outputHeightPixels;
        try
        {
            checked
            {
                outputWidthPixels = widthPixels + (paddingXPixels * 2);
                outputHeightPixels = heightPixels + (paddingYPixels * 2);
            }
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPixels), "Output image resolution is too large.");
        }

        if (outputWidthPixels < 2 || outputHeightPixels < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPixels), "Output image size must be >= 2x2.");
        }

        using var bitmap = new SKBitmap(outputWidthPixels, outputHeightPixels, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(bg);
        using var strokePaint = new SKPaint
        {
            Color = stroke,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)strokeWidthPixels,
            IsAntialias = true,
        };

        foreach (var pad in pads)
        {
            DrawPadOutline(
                canvas,
                strokePaint,
                pad,
                bounds,
                outputWidthPixels,
                outputHeightPixels,
                paddingXPixels,
                paddingYPixels,
                strokeWidthPixels);
        }

        canvas.Flush();
        SaveImage(bitmap, outputPath, format);
    }

    private static Rect2 ComputeBounds(IReadOnlyList<CadPad> pads)
    {
        var bounds = pads[0].Bounds;
        for (var i = 1; i < pads.Count; i++)
        {
            bounds = Rect2.Union(bounds, pads[i].Bounds);
        }

        return bounds;
    }

    private static void DrawPadOutline(
        SKCanvas canvas,
        SKPaint strokePaint,
        CadPad pad,
        Rect2 layerBounds,
        int widthPixels,
        int heightPixels,
        int paddingXPixels,
        int paddingYPixels,
        double strokeWidthPixels)
    {
        var vertices = NormalizeVertices(pad.Polygon.Vertices);
        if (vertices.Count < 3)
        {
            return;
        }

        using var path = new SKPath();
        var first = WorldToPixel(
            vertices[0],
            layerBounds,
            widthPixels,
            heightPixels,
            paddingXPixels,
            paddingYPixels,
            strokeWidthPixels);
        path.MoveTo(first);
        for (var i = 1; i < vertices.Count; i++)
        {
            var current = WorldToPixel(
                vertices[i],
                layerBounds,
                widthPixels,
                heightPixels,
                paddingXPixels,
                paddingYPixels,
                strokeWidthPixels);
            path.LineTo(current);
        }

        path.Close();
        canvas.DrawPath(path, strokePaint);
    }

    private static List<Point2> NormalizeVertices(IReadOnlyList<Point2> source)
    {
        var vertices = source.ToList();
        if (vertices.Count < 2)
        {
            return vertices;
        }

        if (vertices[0].DistanceTo(vertices[^1]) < BoundsEpsilon)
        {
            vertices.RemoveAt(vertices.Count - 1);
        }

        return vertices;
    }

    private static SKPoint WorldToPixel(
        Point2 point,
        Rect2 bounds,
        int width,
        int height,
        int paddingXPixels,
        int paddingYPixels,
        double strokeWidthPixels)
    {
        var maxInsetX = Math.Max(0d, (width - 1) * 0.49);
        var maxInsetY = Math.Max(0d, (height - 1) * 0.49);
        // Keep a full-stroke safety inset to avoid clipping at polygon corners (miter expansion).
        var strokeInset = Math.Max(0d, strokeWidthPixels);
        var requestedInsetX = paddingXPixels + strokeInset;
        var requestedInsetY = paddingYPixels + strokeInset;
        var insetX = Math.Min(requestedInsetX, maxInsetX);
        var insetY = Math.Min(requestedInsetY, maxInsetY);

        var drawableWidth = Math.Max(1d, (width - 1d) - insetX * 2d);
        var drawableHeight = Math.Max(1d, (height - 1d) - insetY * 2d);
        var scaleX = drawableWidth / bounds.Width;
        var scaleY = drawableHeight / bounds.Height;

        var x = insetX + (point.X - bounds.MinX) * scaleX;
        var y = insetY + (bounds.MaxY - point.Y) * scaleY;
        x = Math.Clamp(x, insetX, (width - 1d) - insetX);
        y = Math.Clamp(y, insetY, (height - 1d) - insetY);
        return new SKPoint((float)x, (float)y);
    }

    private static void SaveImage(SKBitmap bitmap, string outputPath, DxfLayerImageFormat format)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            format switch
            {
                DxfLayerImageFormat.Png => SKEncodedImageFormat.Png,
                DxfLayerImageFormat.Bmp => SKEncodedImageFormat.Bmp,
                DxfLayerImageFormat.Jpg => SKEncodedImageFormat.Jpeg,
                _ => SKEncodedImageFormat.Png,
            },
            format == DxfLayerImageFormat.Jpg ? 95 : 100);

        if (data is null)
        {
            throw new InvalidOperationException("Failed to encode DXF layer image.");
        }

        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}
