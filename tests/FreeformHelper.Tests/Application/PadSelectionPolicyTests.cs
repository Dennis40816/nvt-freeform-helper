// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadSelectionPolicyTests
{
    [Theory]
    [InlineData(PadSelectionPolicy.Axis.Row, 1, new[] { 40, 10 })]
    [InlineData(PadSelectionPolicy.Axis.Column, 0, new[] { 30, 10 })]
    [InlineData(PadSelectionPolicy.Axis.Ic, 2, new[] { 40, 20 })]
    public void ResolveAxisReturnsMatchingIndicesInSourceOrder(PadSelectionPolicy.Axis axis, int index, int[] expected)
    {
        var pads = CreatePads();

        var result = PadSelectionPolicy.ResolveAxis(pads, axis, index);

        Assert.Equal(expected, result);
        Assert.Equal([40, 30, 10, 20], pads.Select(pad => pad.Index));
    }

    [Theory]
    [InlineData(PadSelectionPolicy.Axis.Row)]
    [InlineData(PadSelectionPolicy.Axis.Column)]
    [InlineData(PadSelectionPolicy.Axis.Ic)]
    public void ResolveAxisWithNoMatchingValueReturnsEmpty(PadSelectionPolicy.Axis axis)
    {
        Assert.Empty(PadSelectionPolicy.ResolveAxis(CreatePads(), axis, 99));
    }

    [Theory]
    [InlineData(PadSelectionPolicy.Axis.Row)]
    [InlineData(PadSelectionPolicy.Axis.Column)]
    [InlineData(PadSelectionPolicy.Axis.Ic)]
    public void ResolveAxisWithNoPadsReturnsEmpty(PadSelectionPolicy.Axis axis)
    {
        Assert.Empty(PadSelectionPolicy.ResolveAxis([], axis, 0));
    }

    [Fact]
    public void ResolveAxisWithUnknownAxisReturnsEmpty()
    {
        Assert.Empty(PadSelectionPolicy.ResolveAxis(CreatePads(), (PadSelectionPolicy.Axis)(-1), 0));
    }

    [Fact]
    public void ResolveAxisPreservesRepeatedIndicesForTheCanvasToDeduplicate()
    {
        RegularPad[] pads =
        [
            new RegularPad(0, 0, 9, Rectangle(0, 0, 10, 10)),
            new RegularPad(0, 1, 9, Rectangle(10, 0, 20, 10)),
        ];

        Assert.Equal([9, 9], PadSelectionPolicy.ResolveAxis(pads, PadSelectionPolicy.Axis.Row, 0));
    }

    [Theory]
    [InlineData(5, 5, 15, 15, 4, 4, 16, 16)]
    [InlineData(15, 15, 5, 5, 4, 4, 16, 16)]
    [InlineData(5, 15, 15, 5, 4, 4, 16, 16)]
    [InlineData(5, 5, 5, 5, 4, 4, 6, 6)]
    [InlineData(-5, 0, 5, 0, -6, -1, 6, 1)]
    [InlineData(0, 5, 0, -5, -1, -6, 1, 6)]
    public void ExtendBoundsAddsExactlyOneWorldUnitOnEverySide(
        double anchorX, double anchorY, double currentX, double currentY,
        double minX, double minY, double maxX, double maxY)
    {
        var anchor = new Point2(anchorX, anchorY);
        var current = new Point2(currentX, currentY);

        var bounds = PadSelectionPolicy.ExtendBounds(anchor, current);

        Assert.Equal(new Rect2(minX, minY, maxX, maxY), bounds);
    }

    private static RegularPad[] CreatePads() =>
    [
        new RegularPad(1, 1, 40, Rectangle(10, 10, 20, 20)) { IcIndex = 2 },
        new RegularPad(0, 0, 30, Rectangle(0, 0, 10, 10)) { IcIndex = 1 },
        new RegularPad(1, 0, 10, Rectangle(0, 10, 10, 20)) { IcIndex = 1 },
        new RegularPad(0, 1, 20, Rectangle(10, 0, 20, 10)) { IcIndex = 2 },
    ];

    private static Polygon2 Rectangle(double minX, double minY, double maxX, double maxY) => new(
        [new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY)]);
}
