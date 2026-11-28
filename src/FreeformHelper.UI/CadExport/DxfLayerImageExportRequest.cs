// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.CadExport;

/// <summary>The image encoding selected for layer export.</summary>
public enum DxfLayerImageFormat
{
    /// <summary>Portable Network Graphics.</summary>
    Png,
    /// <summary>Windows bitmap.</summary>
    Bmp,
    /// <summary>JPEG image.</summary>
    Jpg,
}

/// <summary>The existing remembered layer image choices, normalized by the project preference owner.</summary>
public readonly record struct DxfLayerImageExportRequest(
    string LayerName,
    int WidthPixels,
    int HeightPixels,
    decimal LineWidthPixels,
    int PaddingXPixels,
    int PaddingYPixels,
    DxfLayerImageFormat Format,
    bool UseDarkTheme)
{
    /// <summary>Gets the selected format's file extension without the leading dot.</summary>
    public string FileExtension => Format switch
    {
        DxfLayerImageFormat.Png => "png",
        DxfLayerImageFormat.Bmp => "bmp",
        DxfLayerImageFormat.Jpg => "jpg",
        _ => "png",
    };
}
