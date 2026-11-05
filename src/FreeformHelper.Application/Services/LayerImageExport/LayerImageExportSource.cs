// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>Owns a stable pad snapshot and supplies layer lists and sizing through the renderer's bounds path.</summary>
public sealed class LayerImageExportSource
{
    private readonly Dictionary<string, IReadOnlyList<CadPad>> _pads;
    private readonly (int? X, int? Y) _projectPixels;

    public LayerImageExportSource(IEnumerable<CadPad> pads, int? projectPixelsX, int? projectPixelsY)
    {
        ArgumentNullException.ThrowIfNull(pads);
        _pads = pads.Where(pad => !string.IsNullOrWhiteSpace(pad.Layer))
            .GroupBy(pad => pad.Layer, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyList<CadPad>)Array.AsReadOnly(group.ToArray()), StringComparer.Ordinal);
        Entries = Array.AsReadOnly(_pads.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new LayerListEntry(pair.Key, pair.Value.Count)).ToArray());
        _projectPixels = (projectPixelsX, projectPixelsY);
    }

    public IReadOnlyList<LayerListEntry> Entries { get; }

    public IReadOnlyList<CadPad> PadsOfLayer(string name) => _pads[name];

    public LayerImageSize CreateDefaultSize(IReadOnlyList<string> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        var bounds = LayerImageBounds.Compute(
            layers.Select(name => new LayerImageLayer(name, PadsOfLayer(name))).ToArray());
        return LayerImageSize.CreateDefault(bounds.Width, bounds.Height, _projectPixels.X, _projectPixels.Y);
    }
}
