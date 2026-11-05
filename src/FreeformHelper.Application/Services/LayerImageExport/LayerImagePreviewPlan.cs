// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Describes preview drawing dimensions, padding and stroke width at one common scale.</summary>
public sealed record LayerImagePreviewPlan(
    int PixelsX,
    int PixelsY,
    int PaddingX,
    int PaddingY,
    decimal LineWidthPixels,
    double Scale);
