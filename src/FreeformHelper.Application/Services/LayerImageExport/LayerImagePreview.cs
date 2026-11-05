// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Scales export dimensions and strokes for a bounded preview without rendering.</summary>
public static class LayerImagePreview
{
    /// <summary>Fits the padded output to the requested side limit, preserving inputs exactly when no scaling is needed.</summary>
    /// <remarks>Scaled dimensions and line width use midpoint-to-even rounding.</remarks>
    public static LayerImagePreviewPlan Plan(
        int pixelsX,
        int pixelsY,
        int paddingX,
        int paddingY,
        decimal lineWidthPixels,
        int maxSidePixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSidePixels, 16);
        long outputWidth = pixelsX + (2L * paddingX);
        long outputHeight = pixelsY + (2L * paddingY);
        double scale = Math.Min(1d, (double)maxSidePixels / Math.Max(outputWidth, outputHeight));
        if (scale == 1d)
        {
            return new LayerImagePreviewPlan(pixelsX, pixelsY, paddingX, paddingY, lineWidthPixels, scale);
        }

        return new LayerImagePreviewPlan(
            Math.Max(2, (int)Math.Round(pixelsX * scale)),
            Math.Max(2, (int)Math.Round(pixelsY * scale)),
            Math.Max(0, (int)Math.Round(paddingX * scale)),
            Math.Max(0, (int)Math.Round(paddingY * scale)),
            Math.Round(Math.Max(1.0m, lineWidthPixels * (decimal)scale), 2),
            scale);
    }
}
