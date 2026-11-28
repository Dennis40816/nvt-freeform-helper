// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DisplaySettingsCanvasEffectTests
{
    [AvaloniaFact]
    public async Task ShowDiffIndexOverlay_AddsAndRemovesOverrideAndAnchorOutlinesAsync()
    {
        using var scene = await DisplaySettingsCanvasScene.LoadAsync();
        scene.Model.ShowRegular = false;
        var overridePad = scene.Model.CadPads[0];
        var anchorPad = scene.Model.CadPads[1];
        scene.Model.SetCadOutputFwDiffIndexOverride(overridePad.Id, 1);
        scene.Model.SetCadOutputFwDiffIndexAnchorCadPad(anchorPad.Id);
        Assert.Contains(overridePad.Id, scene.Model.CadOutputFwDiffIndexOverrideCadIds);
        Assert.Contains(anchorPad.Id, scene.Model.CadOutputFwDiffIndexAnchorCadIdsForCanvas);
        Rect[] expected =
        [
            DisplaySettingsCanvasScene.ToRect(overridePad.Bounds),
            DisplaySettingsCanvasScene.ToRect(anchorPad.Bounds),
        ];

        foreach (var enabled in new[] { false, true, false, true })
        {
            scene.Model.ShowDiffIndexOverlay = enabled;
            var drawings = await scene.DrawPadsAsync(cad: true);
            Assert.Equal(3, drawings.Count(drawing => drawing.Brush is not null));
            var overlays = drawings.Where(drawing => drawing.Brush is null).ToArray();
            Assert.Equal(enabled ? 2 : 0, overlays.Length);
            if (enabled)
            {
                foreach (var bounds in expected)
                {
                    Assert.Single(overlays, drawing => drawing.Geometry!.Bounds == bounds);
                }
            }
        }
    }

    [AvaloniaFact]
    public async Task MaxAreaBuckets_ChangesTheNumberOfCadFillColorsAsync()
    {
        using var scene = await DisplaySettingsCanvasScene.LoadAsync();
        scene.Model.ShowRegular = false;
        scene.Model.ColorCadByArea = true;
        scene.Model.AreaBucketTolerance = 0m;
        Assert.Equal(3, scene.Model.CadPads.Select(pad => pad.Area).Distinct().Count());

        foreach (var limit in new[] { 1, 2, 3, 1 })
        {
            scene.Model.MaxAreaBuckets = limit;
            var colors = await scene.FillColorsAsync(cad: true);

            Assert.Equal(limit, colors.Values.Distinct().Count());
            Assert.All(colors.Values, color => Assert.InRange((int)color.A, 1, 255));
        }
    }

    [AvaloniaFact]
    public async Task CadFillOpacity_ScalesSelectedAndDefaultFillAlphaAsync()
    {
        using var scene = await DisplaySettingsCanvasScene.LoadAsync();
        scene.Model.ShowRegular = false;
        scene.Surface.SetSelection([scene.Model.CadPads[0].Id], []);
        scene.Model.CadFillOpacity = 1m;
        var full = await scene.FillColorsAsync(cad: true);
        Assert.All(full.Values, color => Assert.InRange((int)color.A, 1, 255));

        foreach (var opacity in new[] { 0.5m, 0m, 1m })
        {
            scene.Model.CadFillOpacity = opacity;
            var colors = await scene.FillColorsAsync(cad: true);

            AssertScaledAlpha(full, colors, (double)opacity);
        }
    }

    [AvaloniaFact]
    public async Task RegularFillOpacity_ScalesSelectedAndDefaultFillAlphaAsync()
    {
        using var scene = await DisplaySettingsCanvasScene.LoadAsync();
        scene.Model.ShowCad = false;
        scene.Model.RegularSelectedFillOpacity = 0.6m;
        scene.Surface.SetSelection([], [scene.Model.RegularPads[0].Index]);
        scene.Model.RegularFillOpacity = 1m;
        var full = await scene.FillColorsAsync(cad: false);
        Assert.All(full.Values, color => Assert.InRange((int)color.A, 1, 255));

        foreach (var opacity in new[] { 0.5m, 0m, 1m })
        {
            scene.Model.RegularFillOpacity = opacity;
            var colors = await scene.FillColorsAsync(cad: false);

            AssertScaledAlpha(full, colors, (double)opacity);
        }
    }

    [AvaloniaFact]
    public async Task RegularLineWidth_ChangesEveryUnhighlightedRegularOutlineAsync()
    {
        using var scene = await DisplaySettingsCanvasScene.LoadAsync();
        scene.Model.ShowCad = false;

        foreach (var width in new[] { 0.4m, 1.25m, 0.4m })
        {
            scene.Model.RegularLineWidth = width;
            var drawings = await scene.DrawPadsAsync(cad: false);

            Assert.Equal(2, drawings.Length);
            Assert.All(drawings, drawing => Assert.Equal((double)width, drawing.Pen!.Thickness));
        }
    }

    [AvaloniaFact]
    public async Task HighlightStrokeWidthAdjust_ChangesBothSelectedOutlinesAndPreservesTheMinimumAsync()
    {
        using var scene = await DisplaySettingsCanvasScene.LoadAsync();
        scene.Model.CadLineWidth = 0.2m;
        scene.Model.RegularLineWidth = 0.4m;
        scene.Model.HighlightStrokeWidthAdjust = 0m;
        var cad = scene.Model.CadPads[0];
        var regular = scene.Model.RegularPads[0];
        scene.Surface.SetSelection([cad.Id], [regular.Index]);
        var cadBounds = DisplaySettingsCanvasScene.ToRect(cad.Bounds);
        var regularBounds = DisplaySettingsCanvasScene.ToRect(regular.Bounds);
        var initialCad = Assert.Single(await scene.DrawPadsAsync(cad: true), drawing => drawing.Geometry!.Bounds == cadBounds);
        var initialRegular = Assert.Single(await scene.DrawPadsAsync(cad: false), drawing => drawing.Geometry!.Bounds == regularBounds);
        var initialWidth = initialCad.Pen!.Thickness;
        Assert.Equal(initialWidth, initialRegular.Pen!.Thickness);
        Assert.True(initialWidth > 0.45);

        scene.Model.HighlightStrokeWidthAdjust = 0.6m;
        var widerCad = Assert.Single(await scene.DrawPadsAsync(cad: true), drawing => drawing.Geometry!.Bounds == cadBounds);
        var widerRegular = Assert.Single(await scene.DrawPadsAsync(cad: false), drawing => drawing.Geometry!.Bounds == regularBounds);
        Assert.Equal(initialWidth + 0.6, widerCad.Pen!.Thickness, 10);
        Assert.Equal(initialWidth + 0.6, widerRegular.Pen!.Thickness, 10);

        scene.Model.HighlightStrokeWidthAdjust = -2m;
        var minimumCad = Assert.Single(await scene.DrawPadsAsync(cad: true), drawing => drawing.Geometry!.Bounds == cadBounds);
        var minimumRegular = Assert.Single(await scene.DrawPadsAsync(cad: false), drawing => drawing.Geometry!.Bounds == regularBounds);
        Assert.Equal(0.45, minimumCad.Pen!.Thickness, 10);
        Assert.Equal(0.45, minimumRegular.Pen!.Thickness, 10);
        Assert.Equal(0.2m, scene.Model.CadLineWidth);
        Assert.Equal(0.4m, scene.Model.RegularLineWidth);
    }

    private static void AssertScaledAlpha(Dictionary<Rect, Color> full, Dictionary<Rect, Color> actual, double opacity)
    {
        Assert.Equal(full.Count, actual.Count);
        foreach (var (bounds, color) in full)
        {
            var expectedAlpha = (byte)Math.Round(color.A * opacity);
            Assert.Equal(expectedAlpha, actual[bounds].A);
            if (expectedAlpha > 0)
            {
                Assert.Equal(color.R, actual[bounds].R);
                Assert.Equal(color.G, actual[bounds].G);
                Assert.Equal(color.B, actual[bounds].B);
            }
        }
    }
}
