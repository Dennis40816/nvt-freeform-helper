// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Identifies how checked layers are grouped into output images.</summary>
public enum LayerImageOutputMode
{
    /// <summary>Combines all checked layers in one image.</summary>
    OneImage,

    /// <summary>Writes a separate image for each checked layer.</summary>
    OnePerLayer,
}
