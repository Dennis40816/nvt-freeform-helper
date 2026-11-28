// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.CadExport;

/// <summary>A snapshot of the drawing and file choices; Save fills the chosen path.</summary>
public sealed record LayerImageExportRequest(
    IReadOnlyList<string> Layers,
    LayerImageSize Size,
    decimal LineWidthPixels,
    bool UseDarkTheme,
    DxfLayerImageFormat Format,
    bool OneFilePerLayer,
    string SavePath = "")
{
    /// <summary>Adapts to the existing preference snapshot without introducing another persistence path.</summary>
    public DxfLayerImageExportRequest ToRememberedRequest() => new(
        Layers.Count > 0 ? Layers[0] : string.Empty,
        Size.PixelsX, Size.PixelsY, LineWidthPixels, Size.PaddingX, Size.PaddingY, Format, UseDarkTheme);
}
