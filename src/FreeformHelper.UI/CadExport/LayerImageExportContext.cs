// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.CadExport;

/// <summary>Admitted dialog inputs and host operations, independent of any window.</summary>
public sealed record LayerImageExportContext(
    IReadOnlyList<LayerListEntry> Entries,
    IReadOnlyList<string> InitialCheckedLayers,
    LayerImageSize InitialSize,
    decimal LineWidthPixels,
    bool UseDarkTheme,
    DxfLayerImageFormat Format,
    Func<string, IReadOnlyList<CadPad>> PadsOfLayer,
    Func<IReadOnlyList<string>, LayerImageSize> CreateDefaultSize,
    Func<string, string, Task<string?>> PickSavePathAsync,
    Func<LayerImageRenderRequest, int, CancellationToken, Task<byte[]?>> RenderPngAsync)
{
    /// <summary>Set by the host window code before the dialog opens; the flow writes the files through it.</summary>
    public Func<LayerImageWriteRequest, Task<IReadOnlyList<string>>>? WriteLayerImagesAsync { get; set; }
}
