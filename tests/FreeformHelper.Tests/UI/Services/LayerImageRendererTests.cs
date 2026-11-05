// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageRendererTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderMatchesEveryPixelOfSingleLayerPngExport(bool useDarkTheme)
    {
        var pads = new[]
        {
            LayerImageTestSupport.CreateSquare(1, "A", 0, 0, 10),
            LayerImageTestSupport.CreateSquare(2, "A", 20, 10, 15),
        };
        var request = new LayerImageRenderRequest(new[] { new LayerImageLayer("A", pads) }, 64, 48, 0, 0, 2m, useDarkTheme);
        string path = TestFiles.NewTempFilePath("layer-image-equivalence-", ".png");
        try
        {
            DxfLayerImageExportService.ExportLayerImage(pads, 64, 48, 2m, 0, 0, DxfLayerImageFormat.Png, useDarkTheme, path);

            using var rendered = LayerImageRenderer.Render(request);
            using var decoded = SKBitmap.Decode(path);
            Assert.NotNull(decoded);
            Assert.Equal(64, rendered.Width);
            Assert.Equal(48, rendered.Height);
            Assert.Equal(SKColorType.Rgba8888, rendered.ColorType);
            Assert.Equal(SKAlphaType.Premul, rendered.AlphaType);
            Assert.Equal(decoded.Width, rendered.Width);
            Assert.Equal(decoded.Height, rendered.Height);
            Assert.Equal(decoded.Pixels, rendered.Pixels);
        }
        finally
        {
            LayerImageTestSupport.DeleteTempFile(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderUsesDistinctNonThemeColorsForTwoLayers(bool useDarkTheme)
    {
        var request = CreateTwoLayerRequest(useDarkTheme);

        using var bitmap = LayerImageRenderer.Render(request);

        var first = bitmap.GetPixel(2, 24);
        var second = bitmap.GetPixel(61, 24);
        var background = bitmap.GetPixel(0, 0);
        var themeStroke = useDarkTheme ? SKColors.White : SKColors.Black;
        Assert.NotEqual(first, second);
        Assert.NotEqual(themeStroke, first);
        Assert.NotEqual(themeStroke, second);
        Assert.NotEqual(background, first);
        Assert.NotEqual(background, second);
    }

    [Fact]
    public void RenderMapsTheUnionIncludingTheSecondLayerOutsideTheFirst()
    {
        using var bitmap = LayerImageRenderer.Render(CreateTwoLayerRequest(false));

        var second = bitmap.GetPixel(61, 24);
        Assert.NotEqual(SKColors.White, second);
        Assert.Equal(second, bitmap.GetPixel(51, 2));
        Assert.Equal(SKColors.White, bitmap.GetPixel(51, 24));
        Assert.Equal(SKColors.White, bitmap.GetPixel(30, 24));
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(10, 2));
    }

    [Theory]
    [InlineData(false, 255, 0)]
    [InlineData(true, 42, 255)]
    public void RenderUsesThemeBackgroundAndSingleLayerStroke(bool useDarkTheme, byte backgroundValue, byte strokeValue)
    {
        using var bitmap = LayerImageRenderer.Render(CreateRequest() with { UseDarkTheme = useDarkTheme });

        Assert.Equal(new SKColor(backgroundValue, backgroundValue, backgroundValue), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(strokeValue, strokeValue, strokeValue), bitmap.GetPixel(2, 24));
    }

    [Fact]
    public void RenderKeepsZeroPaddingStrokeNearTheEdgeAndTheOuterFrameClear()
    {
        using var bitmap = LayerImageRenderer.Render(CreateRequest());

        Assert.Equal(64, bitmap.Width);
        Assert.Equal(48, bitmap.Height);
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(32, 2));
        Assert.All(Enumerable.Range(0, bitmap.Width), x =>
        {
            Assert.Equal(SKColors.White, bitmap.GetPixel(x, 0));
            Assert.Equal(SKColors.White, bitmap.GetPixel(x, bitmap.Height - 1));
        });
        Assert.All(Enumerable.Range(0, bitmap.Height), y =>
        {
            Assert.Equal(SKColors.White, bitmap.GetPixel(0, y));
            Assert.Equal(SKColors.White, bitmap.GetPixel(bitmap.Width - 1, y));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderUsesTenDistinctPaletteColorsThenWraps(bool useDarkTheme)
    {
        var layers = Enumerable.Range(0, 11).Select(index => new LayerImageLayer(
            index.ToString(CultureInfo.InvariantCulture),
            new[] { LayerImageTestSupport.CreateSquare(index, "layer", 3 * index, 0, 1) })).ToArray();
        var request = new LayerImageRenderRequest(layers, 315, 32, 0, 0, 2m, useDarkTheme);

        using var bitmap = LayerImageRenderer.Render(request);

        var colors = Enumerable.Range(0, 11).Select(index => bitmap.GetPixel(7 + (30 * index), 2)).ToArray();
        Assert.Equal(10, colors.Take(10).Distinct().Count());
        Assert.Equal(colors[0], colors[10]);
    }

    [Fact]
    public void RenderDrawsLaterLayersOverEarlierLayers()
    {
        var request = CreateTwoLayerRequest(false);
        var overlapping = request with
        {
            Layers = new[] { request.Layers[0], new LayerImageLayer("B", request.Layers[0].Pads) },
        };

        using var separateBitmap = LayerImageRenderer.Render(request);
        using var overlappingBitmap = LayerImageRenderer.Render(overlapping);

        Assert.Equal(separateBitmap.GetPixel(61, 24), overlappingBitmap.GetPixel(2, 24));
    }

    [Fact]
    public void FromSizePreservesDrawingDimensionsPaddingAndAppearance()
    {
        var size = LayerImageSize.CreateDefault(2, 1, 64, 48).WithPaddingX(4).WithPaddingY(6);
        var layers = CreateRequest().Layers;

        var request = LayerImageRenderRequest.FromSize(layers, size, 3.5m, true);

        Assert.Equal(new LayerImageRenderRequest(layers, 64, 48, 4, 6, 3.5m, true), request);
        using var bitmap = LayerImageRenderer.Render(request);
        Assert.Equal(size.OutputWidth, bitmap.Width);
        Assert.Equal(size.OutputHeight, bitmap.Height);
    }

    [Fact]
    public void RenderRejectsEmptyLayers()
    {
        var request = CreateRequest() with { Layers = Array.Empty<LayerImageLayer>() };

        var error = Assert.Throws<InvalidOperationException>(() => LayerImageRenderer.Render(request));

        Assert.Equal("No CAD pads in the selected layers.", error.Message);
    }

    [Fact]
    public void RenderRejectsLayersWithoutPads()
    {
        var request = CreateRequest() with { Layers = new[] { new LayerImageLayer("empty", Array.Empty<CadPad>()) } };

        var error = Assert.Throws<InvalidOperationException>(() => LayerImageRenderer.Render(request));

        Assert.Equal("No CAD pads in the selected layers.", error.Message);
    }

    [Theory]
    [InlineData(1, 48, 2, 0, 0, "widthPixels")]
    [InlineData(64, 1, 2, 0, 0, "widthPixels")]
    [InlineData(64, 48, 0, 0, 0, "lineWidthPixels")]
    [InlineData(64, 48, 2, -1, 0, "paddingXPixels")]
    [InlineData(64, 48, 2, 0, -1, "paddingXPixels")]
    [InlineData(int.MaxValue, 48, 2, 1, 0, "widthPixels")]
    [InlineData(64, 48, 2, 0, int.MaxValue, "widthPixels")]
    public void RenderRejectsInvalidDimensions(int x, int y, int lineWidth, int paddingX, int paddingY, string parameterName)
    {
        var request = new LayerImageRenderRequest(CreateRequest().Layers, x, y, paddingX, paddingY, lineWidth, false);

        Assert.Throws<ArgumentOutOfRangeException>(parameterName, () => LayerImageRenderer.Render(request));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1e-10)]
    public void RenderRejectsEmptyOrTooSmallBounds(double side)
    {
        var layers = new[] { new LayerImageLayer("A", new[] { LayerImageTestSupport.CreateSquare(1, "A", 0, 0, side) }) };
        var request = CreateRequest() with { Layers = layers };

        var error = Assert.Throws<InvalidOperationException>(() => LayerImageRenderer.Render(request));

        Assert.Equal("Selected layer bounds are empty.", error.Message);
    }

    private static LayerImageRenderRequest CreateRequest() => new(
        new[] { new LayerImageLayer("A", new[] { LayerImageTestSupport.CreateSquare(1, "A", 10, 10, 10) }) },
        64, 48, 0, 0, 2m, false);

    private static LayerImageRenderRequest CreateTwoLayerRequest(bool useDarkTheme) => new(
        new[]
        {
            new LayerImageLayer("A", new[] { LayerImageTestSupport.CreateSquare(1, "A", 10, 10, 10) }),
            new LayerImageLayer("B", new[] { LayerImageTestSupport.CreateSquare(2, "B", 30, 10, 10) }),
        },
        64, 48, 0, 0, 2m, useDarkTheme);
}
