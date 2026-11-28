// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>Renders bounded previews using exactly the export renderer and theme.</summary>
public static class LayerImagePreviewService
{
    // One preview renders at a time. A newer request waits for the cancelled older one to stop at its next pad.
    private static readonly SemaphoreSlim _renderGate = new(1, 1);

    public static async Task<byte[]?> RenderPngAsync(
        LayerImageRenderRequest request, int maxSidePixels, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            // The gate is shared by every caller in the process. Neither await may resume on the caller's
            // synchronization context: if that context stops running callbacks (for example a closed window),
            // the release below would never run and every later preview would wait on the gate forever.
            await _renderGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await Task.Run(() => Render(request, maxSidePixels, token), token).ConfigureAwait(false);
            }
            finally
            {
                _renderGate.Release();
            }
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
        using var rendered = LayerImageRenderer.RenderCancellable(request with
        {
            PixelsX = plan.PixelsX,
            PixelsY = plan.PixelsY,
            PaddingX = plan.PaddingX,
            PaddingY = plan.PaddingY,
            LineWidthPixels = plan.LineWidthPixels,
        }, token);
        token.ThrowIfCancellationRequested();
        using var image = SKImage.FromBitmap(rendered);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Could not encode the preview.");
        var png = encoded.ToArray();
        token.ThrowIfCancellationRequested();
        return png;
    }
}
