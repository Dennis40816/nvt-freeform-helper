// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AxisLabelMetricsPolicyTests
{
    [Theory]
    [InlineData(0.25, 0, 12, 0, 100, 6)]
    [InlineData(1, 0, 12, 0, 100, 12)]
    [InlineData(4, 0, 12, 0, 100, 24)]
    [InlineData(1e-6, 0, 12, 0, 100, 0.012)]
    [InlineData(1e-12, 0, 12, 0, 100, 0.012)]
    [InlineData(1, 10, 12, 0, 100, 6)]
    [InlineData(1, 10, 12, 8, 100, 8)]
    [InlineData(0.25, 0, 12, 8, 100, 8)]
    [InlineData(4, 0, 12, 2, 16, 16)]
    [InlineData(4, 10, 12, 2, 16, 6)]
    public void FontSizePreservesZoomScalingStepLimitAndClamps(
        double zoom, double step, double baseSize, double minSize, double maxSize, double expected)
    {
        var actual = AxisLabelMetricsPolicy.FontSize(zoom, step, baseSize, minSize, maxSize);

        Assert.Equal(expected, actual, precision: 12);
    }

    [Theory]
    [InlineData(10, 12, 0, 12, 8, 20, 1)]
    [InlineData(0, 12, 10, 12, 8, 20, 1)]
    [InlineData(10, 12, 10, 12, 8, 20, 2)]
    [InlineData(10, 12, 10, 12.001, 8, 20, 3)]
    [InlineData(1, 12, 3, 12, 8, 20, 3)]
    [InlineData(50, 12, 10, 12, 8, 20, 1)]
    [InlineData(10, 12, 10, 32, 8, 20, 4)]
    [InlineData(10, 32, 10, 12, 8, 20, 4)]
    [InlineData(10, 12, 10, 12, 8, 40, 4)]
    [InlineData(1, 0, 10, 0, 0, 0, 2)]
    [InlineData(10, 12, 10, 12, 18, 20, 3)]
    public void StridePreservesSpacingTransitionsAndCountClamp(
        double step, double fontSize, int count, double maxLabelWidth, double minGap, double space20, int expected)
    {
        var actual = AxisLabelMetricsPolicy.Stride(step, fontSize, count, maxLabelWidth, minGap, space20);

        Assert.Equal(expected, actual);
    }
}
