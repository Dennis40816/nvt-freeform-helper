// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Presentation;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExplanationStyleTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplanationRendersThemeResourcesAndDistinguishesUnappliedValuesAsync(bool dark)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new NotchDetailWindow
        {
            DataContext = NotchDetailPreviewSample.Build(),
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var values = window.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Where(static text => text.DataContext is NotchOutputValue).ToArray();
            Assert.NotEmpty(values);
            var muted = values.Where(static text => text.DataContext is NotchOutputValue { IsMuted: true }).ToArray();
            Assert.NotEmpty(muted);
            Assert.All(muted, text => Assert.Equal(Themed(window, "NfcTextMutedBrush"), text.Foreground));
            Assert.Equal(Themed(window, "NfcTextBrush"), values[0].Foreground);
            Assert.Equal(window.FindResource("FontFamilyCode"), values[0].FontFamily);
            Assert.Equal(13, values[0].FontSize);
            Assert.IsAssignableFrom<IBrush>(Themed(window, "BrushNotchExplanationArea"));
            var reachSwatch = Assert.Single(window.GetVisualDescendants().OfType<Rectangle>(),
                static shape => shape.Classes.Contains("toFull"));
            Assert.Equal(Themed(window, "BrushNotchExplanationReachFill"), reachSwatch.Fill);
            Assert.Equal(Themed(window, "BrushNotchExplanationReachOutline"), reachSwatch.Stroke);
            Assert.NotEmpty(reachSwatch.StrokeDashArray!);
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (ScreenshotOutputDirectory.Resolve("NFH_NOTCH_EXPLANATION_SHOTS") is { } output)
            {
                Directory.CreateDirectory(output);
                frame.Save(Path.Combine(output, dark ? "notch-detail-dark.png" : "notch-detail-light.png"), new PngBitmapEncoderOptions());
            }
            var dev = new DevView { DataContext = new DevViewModel() };
            window.Content = dev;
            await HeadlessUiPump.FlushQueueAsync();
            var preview = Assert.IsType<NotchDetailContent>(dev.FindControl<NotchDetailContent>("DevNotchDetailPreview"));
            Assert.NotNull(Assert.Single(preview.GetVisualDescendants().OfType<NotchPreviewCanvas>()).Explanation);
            Assert.Contains(preview.GetVisualDescendants().OfType<Rectangle>(), static shape => shape.Classes.Contains("toFull"));
        }
        finally
        {
            window.Close();
        }
    }

    private static object? Themed(Window window, string key) =>
        window.TryFindResource(key, window.ActualThemeVariant, out var resource) ? resource : null;
}
