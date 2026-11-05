// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>Renders bounded previews using exactly the export renderer and theme.</summary>
public static class LayerImagePreviewService
{
    public static async Task<byte[]?> RenderPngAsync(
        LayerImageRenderRequest request, int maxSidePixels, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await Task.Run(() => Render(request, maxSidePixels, token), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return null;
        }
    }

    private static byte[]? Render(LayerImageRenderRequest request, int maxSidePixels, CancellationToken token)
    {
        if (token.IsCancellationRequested || !request.Layers.Any(layer => layer.Pads.Any(pad => !pad.Bounds.IsEmpty)))
        {
            return null;
        }

        var plan = LayerImagePreview.Plan(request.PixelsX, request.PixelsY, request.PaddingX, request.PaddingY,
            request.LineWidthPixels, maxSidePixels);
        using var rendered = LayerImageRenderer.Render(request with
        {
            PixelsX = plan.PixelsX,
            PixelsY = plan.PixelsY,
            PaddingX = plan.PaddingX,
            PaddingY = plan.PaddingY,
            LineWidthPixels = plan.LineWidthPixels,
        });
        token.ThrowIfCancellationRequested();
        using var image = SKImage.FromBitmap(rendered);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Could not encode the preview.");
        var png = encoded.ToArray();
        token.ThrowIfCancellationRequested();
        return png;
    }
}
