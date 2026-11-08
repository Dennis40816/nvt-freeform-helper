// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CanvasTokenResourceBehaviorTests
{
    [AvaloniaFact]
    public void Constructor_BrushAndColorTokens_UseTheirColorsForAllCanvasDefaults()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new LoadingModeTestScope();
        var app = global::Avalonia.Application.Current ?? throw new InvalidOperationException("Headless application is missing.");
        var originalResources = app.Resources;
        var cad = Color.FromRgb(12, 34, 56);
        var regular = Color.FromRgb(78, 90, 123);
        var selected = Color.FromRgb(145, 167, 189);
        try
        {
            app.Resources = new ResourceDictionary
            {
                ["ColorCanvasCadLine"] = new SolidColorBrush(cad),
                ["ColorCanvasRegularLine"] = regular,
                ["ColorCanvasRegularSelected"] = new SolidColorBrush(selected),
            };

            var vm = files.CreateViewModel();

            Assert.Equal(cad, vm.CadLineColor);
            Assert.Equal(regular, vm.RegularLineColor);
            Assert.Equal(selected, vm.RegularSelectedColor);
        }
        finally
        {
            app.Resources = originalResources;
        }
    }

    [AvaloniaFact]
    public void Constructor_MissingOrUnsupportedColorTokens_KeepsTransparentDefaults()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new LoadingModeTestScope();
        var app = global::Avalonia.Application.Current ?? throw new InvalidOperationException("Headless application is missing.");
        var originalResources = app.Resources;
        try
        {
            app.Resources = new ResourceDictionary
            {
                ["ColorCanvasCadLine"] = new LinearGradientBrush(),
                ["ColorCanvasRegularLine"] = "not a color resource",
            };

            var vm = files.CreateViewModel();

            Assert.Equal(default(Color), vm.CadLineColor);
            Assert.Equal(default(Color), vm.RegularLineColor);
            Assert.Equal(default(Color), vm.RegularSelectedColor);
        }
        finally
        {
            app.Resources = originalResources;
        }
    }
}
