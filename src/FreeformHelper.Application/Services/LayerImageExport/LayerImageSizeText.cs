// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.Application.Services;

/// <summary>Formats the shared output dimensions.</summary>
public static class LayerImageSizeText
{
    /// <summary>Formats padded dimensions with invariant thousands separators.</summary>
    public static string Format(LayerImageSize size)
    {
        ArgumentNullException.ThrowIfNull(size);
        return string.Create(CultureInfo.InvariantCulture, $"{size.OutputWidth:N0} × {size.OutputHeight:N0} px");
    }
}
