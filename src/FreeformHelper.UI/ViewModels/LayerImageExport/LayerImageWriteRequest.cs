// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Admitted output jobs and drawing choices passed to the host's image writer.</summary>
public sealed record LayerImageWriteRequest(
    IReadOnlyList<LayerImageOutputJob> Jobs,
    Func<string, IReadOnlyList<CadPad>> PadsOfLayer,
    LayerImageSize Size,
    decimal LineWidthPixels,
    bool UseDarkTheme,
    DxfLayerImageFormat Format,
    string ChosenPath);
