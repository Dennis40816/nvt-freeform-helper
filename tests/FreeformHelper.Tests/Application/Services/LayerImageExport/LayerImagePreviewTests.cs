// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImagePreviewTests
{
    [Fact]
    public void PlanPreservesAllInputsExactlyWhenOutputFits()
    {
        var plan = LayerImagePreview.Plan(64, 48, 5, 3, 0.12345m, 74);

        Assert.Equal(new LayerImagePreviewPlan(64, 48, 5, 3, 0.12345m, 1d), plan);
    }

    [Fact]
    public void PlanScalesDrawingPaddingAndStrokeByThePaddedOutput()
    {
        var plan = LayerImagePreview.Plan(4000, 2000, 100, 200, 8m, 840);

        Assert.Equal(new LayerImagePreviewPlan(800, 400, 20, 40, 1.6m, 0.2d), plan);
    }

    [Fact]
    public void PlanUsesTheLongerOutputSideForPortraitImages()
    {
        var plan = LayerImagePreview.Plan(2000, 4000, 200, 100, 8m, 840);

        Assert.Equal(new LayerImagePreviewPlan(400, 800, 40, 20, 1.6m, 0.2d), plan);
    }

    [Fact]
    public void PlanKeepsMinimumDrawingSizeAndLineWidth()
    {
        var plan = LayerImagePreview.Plan(2, 100_000, 1, 0, 1m, 16);

        Assert.Equal(new LayerImagePreviewPlan(2, 16, 0, 0, 1.0m, 0.00016d), plan);
    }

    [Fact]
    public void PlanRoundsScaledLineWidthToTwoDecimals()
    {
        var plan = LayerImagePreview.Plan(500, 250, 0, 0, 8.33m, 100);

        Assert.Equal(1.67m, plan.LineWidthPixels);
    }

    [Fact]
    public void PlanRoundsMidpointsToEven()
    {
        var plan = LayerImagePreview.Plan(50, 1000, 5, 15, 10m, 515);

        Assert.Equal(new LayerImagePreviewPlan(25, 500, 2, 8, 5m, 0.5d), plan);
    }

    [Fact]
    public void PlanComputesLargePaddedDimensionsWithoutIntegerOverflow()
    {
        var plan = LayerImagePreview.Plan(int.MaxValue, 2, int.MaxValue, 0, 1m, 64);

        Assert.Equal(new LayerImagePreviewPlan(21, 2, 21, 0, 1.0m, 64d / (3L * int.MaxValue)), plan);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(15)]
    public void PlanRejectsSideLimitsBelow16(int maxSidePixels)
    {
        Assert.Throws<ArgumentOutOfRangeException>(nameof(maxSidePixels), () =>
            LayerImagePreview.Plan(64, 48, 0, 0, 1m, maxSidePixels));
    }
}
