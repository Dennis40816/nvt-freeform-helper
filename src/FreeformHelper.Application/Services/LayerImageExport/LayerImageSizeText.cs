// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.Application.Services;

/// <summary>Formats the shared output dimensions and selected-layer summary.</summary>
public static class LayerImageSizeText
{
    /// <summary>Formats padded dimensions with invariant thousands separators.</summary>
    public static string Format(LayerImageSize size)
    {
        ArgumentNullException.ThrowIfNull(size);
        return string.Create(CultureInfo.InvariantCulture, $"{size.OutputWidth:N0} × {size.OutputHeight:N0} px");
    }

    /// <summary>Formats the selected count with English singular or plural wording.</summary>
    public static string FormatSelection(int count) => string.Create(
        CultureInfo.InvariantCulture, $"{count:N0} {(count == 1 ? "layer" : "layers")} selected");
}
