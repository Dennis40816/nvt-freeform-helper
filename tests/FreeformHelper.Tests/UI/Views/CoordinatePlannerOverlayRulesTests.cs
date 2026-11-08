// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Views;
using Xunit;
using OverlayMode = FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerOverlayRulesTests
{
    private const OverlayMode Focus = OverlayMode.Focus;
    private const OverlayMode Essentials = OverlayMode.Essentials;
    private const OverlayMode All = OverlayMode.All;

    private static CoordinatePlannerLine Line(string key, CoordinatePlannerLineKind kind)
    {
        var p = new Point2(0, 0);
        return new CoordinatePlannerLine(key, key, kind, p, p, p, p, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static CoordinatePlannerPoint Point(string key, CoordinatePlannerPointKind kind)
    {
        var p = new Point2(0, 0);
        return new CoordinatePlannerPoint(key, key, kind, p, p, 0, 0, 0, 0, 0, 0);
    }

    [Theory]
    [InlineData(All, CoordinatePlannerLineKind.CustomPath, null, true)]
    [InlineData(Essentials, CoordinatePlannerLineKind.HorizontalGuide, null, true)]
    [InlineData(Essentials, CoordinatePlannerLineKind.CustomPath, null, false)]
    [InlineData(Essentials, CoordinatePlannerLineKind.CustomPath, "line", true)]
    [InlineData(Focus, CoordinatePlannerLineKind.CustomPath, null, false)]
    [InlineData(Focus, CoordinatePlannerLineKind.CustomPath, "other", true)]
    [InlineData(Focus, CoordinatePlannerLineKind.VerticalGuide, null, true)]
    public void LineIsRenderedByModeKindAndSelection(OverlayMode mode, CoordinatePlannerLineKind kind, string? selectedKey, bool expected)
    {
        Assert.Equal(expected, CoordinatePlannerOverlayRules.ShouldRenderLine(Line("line", kind), mode, selectedKey));
    }

    [Theory]
    [InlineData(All, CoordinatePlannerPointKind.CustomPoint, null, true)]
    [InlineData(Essentials, CoordinatePlannerPointKind.AaCorner, null, true)]
    [InlineData(Essentials, CoordinatePlannerPointKind.CustomArrayDot, null, false)]
    [InlineData(Essentials, CoordinatePlannerPointKind.CustomArrayDot, "point", true)]
    [InlineData(Focus, CoordinatePlannerPointKind.CustomPathStep, null, false)]
    [InlineData(Focus, CoordinatePlannerPointKind.CustomPathStep, " ", false)]
    [InlineData(Focus, CoordinatePlannerPointKind.CustomPathStep, "other", true)]
    public void PointIsRenderedByModeKindAndSelection(OverlayMode mode, CoordinatePlannerPointKind kind, string? selectedKey, bool expected)
    {
        Assert.Equal(expected, CoordinatePlannerOverlayRules.ShouldRenderPoint(Point("point", kind), mode, selectedKey));
    }

    [Theory]
    [InlineData(All, false, false)]
    [InlineData(Essentials, true, true)]
    [InlineData(Focus, true, true)]
    [InlineData(Focus, false, false)]
    public void RectangleIsLabeledOnlyWhenSelectedAndNotInAllMode(OverlayMode mode, bool isSelected, bool expected)
    {
        Assert.Equal(expected, CoordinatePlannerOverlayRules.ShouldLabelRectangle(mode, isSelected));
    }

    [Theory]
    [InlineData(All, CoordinatePlannerLineKind.CustomPath, false, true)]
    [InlineData(Focus, CoordinatePlannerLineKind.CustomPath, false, false)]
    [InlineData(Focus, CoordinatePlannerLineKind.CustomPath, true, true)]
    [InlineData(Essentials, CoordinatePlannerLineKind.CustomArrayEdge, false, true)]
    public void LineIsLabeledInAllModeWhenSelectedOrAnArrayEdge(OverlayMode mode, CoordinatePlannerLineKind kind, bool isSelected, bool expected)
    {
        Assert.Equal(expected, CoordinatePlannerOverlayRules.ShouldLabelLine(Line("line", kind), mode, isSelected));
    }

    [Theory]
    [InlineData(All, CoordinatePlannerPointKind.CustomPoint, false, true)]
    [InlineData(All, CoordinatePlannerPointKind.CustomArrayDot, false, false)]
    [InlineData(All, CoordinatePlannerPointKind.CustomArrayDot, true, true)]
    [InlineData(Focus, CoordinatePlannerPointKind.BistCorner, false, true)]
    [InlineData(Focus, CoordinatePlannerPointKind.CustomPathStep, false, false)]
    public void PointLabelFollowsModeKindAndSelection(OverlayMode mode, CoordinatePlannerPointKind kind, bool isSelected, bool expected)
    {
        Assert.Equal(expected, CoordinatePlannerOverlayRules.ShouldLabelPoint(Point("point", kind), mode, isSelected));
    }

    [Theory]
    [InlineData(Focus, "a", "b", true)]
    [InlineData(Focus, "a", "a", false)]
    [InlineData(Focus, null, "b", false)]
    [InlineData(Essentials, "a", "b", false)]
    [InlineData(All, "a", "b", false)]
    public void OverlayIsMutedOnlyInFocusModeForItemsOtherThanTheSelection(OverlayMode mode, string? selectedKey, string key, bool expected)
    {
        Assert.Equal(expected, CoordinatePlannerOverlayRules.IsMutedOverlay(mode, selectedKey, key));
    }

    [Fact]
    public void SelectionMatchesOnlyTheSameKeyWithTheSameCase()
    {
        Assert.True(CoordinatePlannerOverlayRules.IsSelected("a", "a"));
        Assert.False(CoordinatePlannerOverlayRules.IsSelected("a", "A"));
        Assert.False(CoordinatePlannerOverlayRules.IsSelected("a", null));
    }
}
