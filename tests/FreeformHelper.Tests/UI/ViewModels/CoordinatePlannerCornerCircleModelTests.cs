// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerCornerCircleModelTests
{
    [Fact]
    public void NewModel_IsOffAndHasNoLayoutOrImage()
    {
        var model = new CoordinatePlannerCornerCircleModel();
        model.SetPixelSize(1920, 1080);

        Assert.False(model.HasLayout);
        Assert.Null(model.Layout);
        Assert.Null(model.BuildImage());
        Assert.Equal("Corner circles are off.", model.StatusText);
    }

    [Fact]
    public void Enable_PlacesTheCirclesAtTheActiveAreaCornersAndBuildsAnImageOfTheSameSize()
    {
        var model = new CoordinatePlannerCornerCircleModel { Radius = 100m };
        model.SetPixelSize(1920, 1080);

        model.IsEnabled = true;

        Assert.True(model.HasLayout);
        Assert.Equal(
            [new Point2(100, 100), new Point2(1820, 100), new Point2(1820, 980), new Point2(100, 980)],
            model.Layout!.Centers);
        var image = model.BuildImage();
        Assert.NotNull(image);
        Assert.Equal(1920, image.Width);
        Assert.Equal(1080, image.Height);
    }

    [Fact]
    public void RadiusThatIsTooLarge_ReportsItAndBuildsNoImage()
    {
        var model = new CoordinatePlannerCornerCircleModel { Radius = 600m };
        model.SetPixelSize(1920, 1080);

        model.IsEnabled = true;

        Assert.False(model.HasLayout);
        Assert.Equal(CornerCircleLayoutStatus.DoesNotFit, model.Layout!.Status);
        Assert.Equal(model.Layout.Message, model.StatusText);
        Assert.Null(model.BuildImage());
    }

    [Fact]
    public void ChangingRadiusOrSize_RecalculatesAndRaisesLayoutChanged()
    {
        var model = new CoordinatePlannerCornerCircleModel { IsEnabled = true };
        model.SetPixelSize(1000, 500);
        var raised = 0;
        model.LayoutChanged += (_, _) => raised++;

        model.Radius = 40m;
        model.SetPixelSize(2000, 1000);

        Assert.Equal(2, raised);
        Assert.Equal(new Rect2(40, 40, 1960, 960), model.Layout!.CenterRectangle);
    }

    [Fact]
    public void SettingTheSameSizeAgain_DoesNotRecalculate()
    {
        var model = new CoordinatePlannerCornerCircleModel { IsEnabled = true };
        model.SetPixelSize(1000, 500);
        var raised = 0;
        model.LayoutChanged += (_, _) => raised++;

        model.SetPixelSize(1000, 500);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void WithoutAPixelSize_ReportsThatTheActiveAreaHasNone()
    {
        var model = new CoordinatePlannerCornerCircleModel { IsEnabled = true };

        Assert.False(model.HasLayout);
        Assert.Equal("The active area has no pixel size.", model.StatusText);
    }

    [Fact]
    public void ConstructorPixelSize_IsUsedWhenTheModelIsEnabled()
    {
        var model = new CoordinatePlannerCornerCircleModel(800, 600) { Radius = 50m };

        model.IsEnabled = true;

        Assert.True(model.HasLayout);
        Assert.Equal(800, model.BuildImage()!.Width);
    }

    [Fact]
    public void AnActiveAreaBeyondTheImageLimit_ReportsItAndBuildsNoImage()
    {
        var model = new CoordinatePlannerCornerCircleModel(40000, 100) { IsEnabled = true };

        Assert.False(model.HasLayout);
        Assert.Equal("The active area is too large for an image.", model.StatusText);
        Assert.Null(model.BuildImage());
    }

    [Fact]
    public void DrawCenterRectangle_AddsTheRectangleEdgesToTheImage()
    {
        var model = new CoordinatePlannerCornerCircleModel { Radius = 5m, IsEnabled = true };
        model.SetPixelSize(40, 40);

        var plain = model.BuildImage();
        model.DrawCenterRectangle = true;
        var withRectangle = model.BuildImage();

        Assert.Equal(0, plain!.Pixels[(5 * 40) + 20]);
        Assert.Equal(255, withRectangle!.Pixels[(5 * 40) + 20]);
    }
}
