// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>File options have no effect on preview rendering. Mutable state is UI thread only.</summary>
public sealed class LayerImageFileSettings : ObservableObject
{
    private DxfLayerImageFormat _format = DxfLayerImageFormat.Png;
    private bool _oneFilePerLayer;

    public DxfLayerImageFormat Format
    {
        get => _format;
        set => SetProperty(ref _format, Enum.IsDefined(value) ? value : DxfLayerImageFormat.Png);
    }

    public bool OneFilePerLayer
    {
        get => _oneFilePerLayer;
        set => SetProperty(ref _oneFilePerLayer, value);
    }

    public IReadOnlyList<ImageFormatOption> FormatOptions { get; } = new List<ImageFormatOption>
    {
        new(DxfLayerImageFormat.Png, "PNG"),
        new(DxfLayerImageFormat.Bmp, "BMP"),
        new(DxfLayerImageFormat.Jpg, "JPG"),
    }.AsReadOnly();

    public sealed record ImageFormatOption(DxfLayerImageFormat Value, string Display);
}
