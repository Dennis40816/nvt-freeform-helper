// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Calculates axis-label font sizes and strides from captured canvas values.</summary>
public static class AxisLabelMetricsPolicy
{
    /// <summary>Scales the font with zoom, constrains it by step, and clamps it to the supplied limits.</summary>
    public static double FontSize(double zoom, double step, double baseSize, double minSize, double maxSize)
    {
        // Font size scales with the square root of zoom to make it visually balanced.
        var sizeFromZoom = baseSize * Math.Sqrt(Math.Max(zoom, 1e-6));
        if (step > 0)
        {
            // Also constrain font size by the space available (step size).
            // This prevents labels from overlapping when zoomed out.
            var sizeFromStep = step * 0.6; // Use 60% of the step to ensure some padding.
            sizeFromZoom = Math.Min(sizeFromZoom, sizeFromStep);
        }

        return Math.Clamp(sizeFromZoom, minSize, maxSize); // Clamp to overall min/max.
    }

    /// <summary>Returns the label stride for the supplied spacing, clamped to the label count.</summary>
    public static int Stride(double step, double fontSize, int count, double maxLabelWidth, double minGap, double space20)
    {
        if (count <= 0 || step <= 0)
        {
            return 1;
        }

        var expectedLabelWidth = Math.Max(fontSize, Math.Max(1.0, maxLabelWidth));
        var axisLabelMinGap = Math.Max(minGap, 1.0);
        var minSpacing = Math.Max(space20, expectedLabelWidth + axisLabelMinGap);
        var stride = (int)Math.Ceiling(minSpacing / step);
        return Math.Clamp(stride, 1, count);
    }
}
