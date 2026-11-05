// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Describes the layers, drawing dimensions and appearance of one image.</summary>
public sealed record LayerImageRenderRequest(
    IReadOnlyList<LayerImageLayer> Layers,
    int PixelsX,
    int PixelsY,
    int PaddingX,
    int PaddingY,
    decimal LineWidthPixels,
    bool UseDarkTheme)
{
    /// <summary>Projects export sizing into a request without changing its drawing dimensions or padding.</summary>
    public static LayerImageRenderRequest FromSize(
        IReadOnlyList<LayerImageLayer> layers,
        LayerImageSize size,
        decimal lineWidthPixels,
        bool useDarkTheme) => new(
            layers, size.PixelsX, size.PixelsY, size.PaddingX, size.PaddingY, lineWidthPixels, useDarkTheme);
}
