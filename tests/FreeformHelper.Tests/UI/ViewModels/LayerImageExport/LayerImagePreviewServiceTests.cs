// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImagePreviewServiceTests
{
    [Fact]
    public async Task PreviewUsesThePlanForPaddedDimensionsAsync()
    {
        var context = LayerImageExportTestData.CreateContext();
        var size = context.InitialSize.WithPaddingX(10).WithPaddingY(20);
        var layers = context.InitialCheckedLayers.Select(name => new LayerImageLayer(name, context.PadsOfLayer(name))).ToList();
        var request = LayerImageRenderRequest.FromSize(layers, size, 1m, false);
        var plan = LayerImagePreview.Plan(size.PixelsX, size.PixelsY, size.PaddingX, size.PaddingY, 1m, 1200);

        var png = await LayerImagePreviewService.RenderPngAsync(request, 1200, TestContext.Current.CancellationToken);

        Assert.NotNull(png);
        using var stream = new MemoryStream(png);
        using var codec = SKCodec.Create(stream);
        Assert.NotNull(codec);
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        using var bitmap = SKBitmap.Decode(codec);
        Assert.NotNull(bitmap);
        Assert.Equal(plan.PixelsX + 2 * plan.PaddingX, bitmap.Width);
        Assert.Equal(plan.PixelsY + 2 * plan.PaddingY, bitmap.Height);
    }

    [Fact]
    public async Task EmptyOrCancelledPreviewReturnsNullAsync()
    {
        var request = new LayerImageRenderRequest(Array.Empty<LayerImageLayer>(), 100, 100, 0, 0, 1m, false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var context = LayerImageExportTestData.CreateContext();
        var layers = context.InitialCheckedLayers.Select(name => new LayerImageLayer(name, context.PadsOfLayer(name))).ToList();
        var empty = await LayerImagePreviewService.RenderPngAsync(request, 1200, TestContext.Current.CancellationToken);
        var cancelled = await LayerImagePreviewService.RenderPngAsync(request with { Layers = layers }, 1200, cancellation.Token);

        Assert.Null(empty);
        Assert.Null(cancelled);
    }
}
