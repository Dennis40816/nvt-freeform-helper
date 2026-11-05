// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Converters;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerPreviewBitmapConverterTests
{
    [AvaloniaFact]
    public void NullPreviewProducesNoBitmap()
    {
        using var converter = new LayerPreviewBitmapConverter();

        Assert.Null(converter.Convert(null, typeof(Bitmap), null, CultureInfo.InvariantCulture));
    }

    [AvaloniaFact]
    public async Task RebindingReusesTheBitmapAndNewBytesReplaceItAsync()
    {
        using var session = new LayerImageExportViewTestSession();
        await session.ShowAsync();
        await session.CompletePreviewAsync();
        var bytes = Assert.IsType<byte[]>(session.Model.PreviewPng);
        using var converter = new LayerPreviewBitmapConverter();

        var first = Assert.IsType<Bitmap>(converter.Convert(bytes, typeof(Bitmap), null, CultureInfo.InvariantCulture));
        var same = converter.Convert(bytes, typeof(Bitmap), null, CultureInfo.InvariantCulture);
        var replacement = Assert.IsType<Bitmap>(converter.Convert(bytes.ToArray(), typeof(Bitmap), null, CultureInfo.InvariantCulture));

        Assert.Same(first, same);
        Assert.NotSame(first, replacement);
        Assert.Null(converter.Convert(null, typeof(Bitmap), null, CultureInfo.InvariantCulture));
        await HeadlessUiPump.FlushQueueAsync();
    }
}
