// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasAxisLabelMetricsCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaTheory]
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
    public void LabelFontSizePreservesZoomScalingStepLimitAndClamps(
        double zoom, double step, double baseSize, double minSize, double maxSize, double expected)
    {
        var canvas = new PadCanvas();
        PadCanvasPrivateAccess.Zoom(canvas) = zoom;

        var actual = PadCanvasPrivateAccess.GetLabelFontSize(canvas, step, baseSize, minSize, maxSize);

        Assert.Equal(expected, actual, precision: 12);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
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
    public void AxisLabelStridePreservesSpacingTransitionsAndCountClamp(
        double step, double fontSize, int count, double maxLabelWidth, double minGap, double space20, int expected)
    {
        var canvas = new PadCanvas();
        canvas.Resources["Space8"] = 80.0;
        canvas.Resources["CanvasAxisLabelMinGap"] = minGap;
        canvas.Resources["Space20"] = space20;

        var actual = PadCanvasPrivateAccess.GetAxisLabelStride(canvas, step, fontSize, count, maxLabelWidth);

        Assert.Equal(expected, actual);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(18.0, 20.0, 3)]
    [InlineData("unsupported", "unsupported", 2)]
    public void AxisLabelStridePreservesSpacingResourceFallbacks(object space8, object space20, int expected)
    {
        var canvas = new PadCanvas();
        canvas.Resources["CanvasAxisLabelMinGap"] = "unsupported";
        canvas.Resources["Space8"] = space8;
        canvas.Resources["Space20"] = space20;

        var actual = PadCanvasPrivateAccess.GetAxisLabelStride(canvas, 10, 12, 10, 12);

        Assert.Equal(expected, actual);
    }
}
