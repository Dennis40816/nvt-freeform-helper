// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageRendererOutlineBehaviorTests
{
    [Fact]
    public void Render_CollapsedClosedOutline_SkipsTheSpuriousDiagonalAndDrawsTheValidPad()
    {
        var collapsed = new CadPad(2, "collapsed", "A", new Polygon2(
        [
            new Point2(0, 0), new Point2(10, 10), new Point2(0, 0),
        ]));
        var valid = LayerImageTestSupport.CreateSquare(1, "A", 0, 0, 10);
        var request = new LayerImageRenderRequest(
            [new LayerImageLayer("A", [collapsed, valid])], 64, 48, 0, 0, 2m, false);

        using var bitmap = LayerImageRenderer.Render(request);

        Assert.Equal(64, bitmap.Width);
        Assert.Equal(48, bitmap.Height);
        Assert.Equal(SKColors.White, bitmap.GetPixel(32, 24));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(2, 24));
        Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
    }
}
