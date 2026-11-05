// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImagePreviewSettingsTests
{
    [Theory]
    [InlineData(true, 424)]
    [InlineData(false, 848)]
    public void PixelEditHonorsRatioAndPublishesOneChange(bool keepRatio, int expectedY)
    {
        var settings = new LayerImagePreviewSettings(LayerImageSize.CreateDefault(10, 2, 6480, 848)) { KeepRatio = keepRatio };
        int changes = 0;
        var notified = new List<string?>();
        settings.Changed += () => changes++;
        settings.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        settings.PixelsX = 3240m;

        Assert.Equal((decimal)expectedY, settings.PixelsY);
        Assert.Equal(1, changes);
        Assert.Contains(nameof(settings.PixelsX), notified);
        Assert.Contains(nameof(settings.PixelsY), notified);
        Assert.Contains(nameof(settings.OutputText), notified);
    }

    [Fact]
    public void ResetRestoresProjectPixelsAndClearsBothPaddingAxes()
    {
        var settings = new LayerImagePreviewSettings(LayerImageSize.CreateDefault(10, 2, 6480, 848))
        {
            PixelsX = 320,
            PixelsY = 240,
            PaddingX = 10,
            PaddingY = 20,
            KeepRatio = true,
        };

        settings.ResetToProjectSizeCommand.Execute(null);

        Assert.Equal("6,480 × 848 px", settings.OutputText);
        Assert.Equal(6480m, settings.PixelsX);
        Assert.Equal(848m, settings.PixelsY);
        Assert.Equal(0m, settings.PaddingX);
        Assert.Equal(0m, settings.PaddingY);
        Assert.True(settings.KeepRatio);
        Assert.Equal(LayerImageSizeSource.Project, settings.CurrentSize.Source);
    }

    [Fact]
    public void DecimalInputsClampWithoutOverflowAndThemeCommandToggles()
    {
        var settings = new LayerImagePreviewSettings(LayerImageSize.CreateDefault(1, 1, null, null));

        settings.PixelsX = decimal.MaxValue;
        settings.PixelsY = decimal.MinValue;
        settings.PaddingX = decimal.MinValue;
        settings.PaddingY = decimal.MaxValue;
        settings.LineWidthPixels = 500m;
        settings.ToggleThemeCommand.Execute(null);

        Assert.Equal(100_000m, settings.PixelsX);
        Assert.Equal(2m, settings.PixelsY);
        Assert.Equal(0m, settings.PaddingX);
        Assert.Equal(100_000m, settings.PaddingY);
        Assert.Equal(100m, settings.LineWidthPixels);
        Assert.True(settings.UseDarkTheme);
        settings.LineWidthPixels = -1m;
        settings.ToggleThemeCommand.Execute(null);
        Assert.Equal(0.1m, settings.LineWidthPixels);
        Assert.False(settings.UseDarkTheme);
    }
}
