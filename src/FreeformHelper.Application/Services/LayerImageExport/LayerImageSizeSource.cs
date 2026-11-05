// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Identifies where the current layer image dimensions came from.</summary>
public enum LayerImageSizeSource
{
    /// <summary>The project supplied both pixel counts.</summary>
    Project,

    /// <summary>The drawing bounds supplied the automatic dimensions.</summary>
    Auto,

    /// <summary>The user edited a pixel count or padding.</summary>
    Custom,
}
