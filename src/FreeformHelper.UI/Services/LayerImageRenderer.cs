// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>Renders ordered CAD layers through the shared preview and export drawing path.</summary>
public static class LayerImageRenderer
{
    private const double BoundsEpsilon = 1e-9;
    private static readonly SKColor _lightBackground = new(255, 255, 255, 255);
    private static readonly SKColor _lightStroke = new(0, 0, 0, 255);
    private static readonly SKColor _darkBackground = new(42, 42, 42, 255);
    private static readonly SKColor _darkStroke = new(255, 255, 255, 255);
    private static readonly SKColor[] _lightPalette =
    [
        new(0, 90, 156), new(180, 55, 0), new(0, 115, 75), new(130, 65, 155), new(160, 30, 80),
        new(0, 115, 130), new(125, 95, 0), new(75, 75, 175), new(120, 65, 35), new(75, 110, 20),
    ];
    private static readonly SKColor[] _darkPalette =
    [
        new(95, 190, 255), new(255, 160, 80), new(95, 215, 145), new(200, 155, 255), new(255, 130, 180),
        new(80, 215, 220), new(235, 210, 90), new(150, 170, 255), new(220, 175, 140), new(180, 220, 100),
    ];

    /// <summary>Returns a new RGBA bitmap owned by the caller, mapping the union of all pad bounds.</summary>
    public static SKBitmap Render(LayerImageRenderRequest request) => RenderCancellable(request, CancellationToken.None);

    /// <summary>Renders like <see cref="Render"/> and stops at the next pad once the token is cancelled.</summary>
    public static SKBitmap RenderCancellable(LayerImageRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request.Layers);
        if (!request.Layers.Any(layer => layer.Pads.Count > 0))
        {
            throw new InvalidOperationException("No CAD pads in the selected layers.");
        }

        ValidateDimensions(request.PixelsX, request.PixelsY, request.LineWidthPixels, request.PaddingX, request.PaddingY);
        var bounds = ComputeBounds(request.Layers);
        if (bounds.IsEmpty || bounds.Width < BoundsEpsilon || bounds.Height < BoundsEpsilon)
        {
            throw new InvalidOperationException("Selected layer bounds are empty.");
        }

        var (width, height) = ComputeOutputSize(request.PixelsX, request.PixelsY, request.PaddingX, request.PaddingY);
        SKBitmap? bitmap = null;
        try
        {
            bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(request.UseDarkTheme ? _darkBackground : _lightBackground);
            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)(double)request.LineWidthPixels,
                IsAntialias = true,
            };
            DrawLayers(canvas, strokePaint, request, bounds, width, height, cancellationToken);
            canvas.Flush();
            var result = bitmap;
            bitmap = null;
            return result;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private static void ValidateDimensions(
        int widthPixels, int heightPixels, decimal lineWidthPixels, int paddingXPixels, int paddingYPixels)
    {
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
    }

    private static (int Width, int Height) ComputeOutputSize(
        int widthPixels, int heightPixels, int paddingXPixels, int paddingYPixels)
    {
        int width;
        int height;
        try
        {
            checked
            {
                width = widthPixels + (paddingXPixels * 2);
                height = heightPixels + (paddingYPixels * 2);
            }
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPixels), "Output image resolution is too large.");
        }

        if (width < 2 || height < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPixels), "Output image size must be >= 2x2.");
        }

        return (width, height);
    }

    /// <summary>Computes the common pad bounds used by sizing, preview and export.</summary>
    public static Rect2 ComputeBounds(IReadOnlyList<LayerImageLayer> layers) => LayerImageBounds.Compute(layers);

    private static void DrawLayers(
        SKCanvas canvas,
        SKPaint strokePaint,
        LayerImageRenderRequest request,
        Rect2 bounds,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var palette = request.UseDarkTheme ? _darkPalette : _lightPalette;
        var themeStroke = request.UseDarkTheme ? _darkStroke : _lightStroke;
        for (int index = 0; index < request.Layers.Count; index++)
        {
            strokePaint.Color = request.Layers.Count == 1 ? themeStroke : palette[index % palette.Length];
            foreach (var pad in request.Layers[index].Pads)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DrawPadOutline(
                    canvas, strokePaint, pad, bounds, width, height, request.PaddingX, request.PaddingY,
                    (double)request.LineWidthPixels);
            }
        }
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
}
