// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasAreaColorCharacterizationTests
{
    [AvaloniaFact]
    public void DifferentAreaBucketsHaveDifferentColors()
    {
        var colors = ColorsOf([CreatePad(1, 100), CreatePad(2, 400)], 0.1);

        Assert.NotEqual(colors[1], colors[2]);
    }

    [AvaloniaFact]
    public void PadsWithinToleranceShareTheFirstAreaColor()
    {
        var colors = ColorsOf([CreatePad(1, 100), CreatePad(2, 108), CreatePad(3, 116)], 0.1);

        Assert.Equal(colors[1], colors[2]);
        Assert.NotEqual(colors[1], colors[3]);
    }

    [AvaloniaFact]
    public void ZeroToleranceGroupsOnlyEqualAreas()
    {
        var colors = ColorsOf([CreatePad(1, 100), CreatePad(2, 100), CreatePad(3, 101)], 0);

        Assert.Equal(colors[1], colors[2]);
        Assert.NotEqual(colors[1], colors[3]);
    }

    [AvaloniaFact]
    public void ToleranceAboveHalfUsesTheHalfToleranceBuckets()
    {
        CadPad[] pads = [CreatePad(1, 100), CreatePad(2, 150), CreatePad(3, 151), CreatePad(4, 200)];

        var colorsAtHalf = ColorsOf(pads, 0.5);
        var colorsAboveHalf = ColorsOf(pads, 0.75);

        Assert.Equal(colorsAboveHalf[1], colorsAboveHalf[2]);
        Assert.NotEqual(colorsAboveHalf[1], colorsAboveHalf[3]);
        Assert.Equal(colorsAboveHalf[3], colorsAboveHalf[4]);
        Assert.All(pads, pad => Assert.Equal(colorsAtHalf[pad.Id], colorsAboveHalf[pad.Id]));
    }

    [AvaloniaFact]
    public void EqualAreaPadsKeepTheirBucketColorsWhenIdsArriveUnsorted()
    {
        CadPad[] pads = [CreatePad(40, 400), CreatePad(30, 100), CreatePad(10, 100), CreatePad(20, 400)];

        var colors = ColorsOf(pads, 0);
        var colorsById = ColorsOf(pads.OrderBy(pad => pad.Id).ToList(), 0);

        Assert.Equal(colors[10], colors[30]);
        Assert.Equal(colors[20], colors[40]);
        Assert.NotEqual(colors[10], colors[20]);
        Assert.All(pads, pad => Assert.Equal(colorsById[pad.Id], colors[pad.Id]));
    }

    [AvaloniaFact]
    public void ZeroAreaPadsShareAColorAndSeparateFromPositiveArea()
    {
        var colors = ColorsOf([CreatePad(3, 100), CreatePad(2, 0), CreatePad(1, 0)], 0.5);

        Assert.Equal(colors[1], colors[2]);
        Assert.NotEqual(colors[1], colors[3]);
    }

    [AvaloniaFact]
    public void MoreBucketsThanMaxAreaBucketsCycleColorsInAreaOrder()
    {
        CadPad[] pads =
        [
            CreatePad(1, 400), CreatePad(2, 100), CreatePad(3, 300), CreatePad(4, 200), CreatePad(5, 500),
        ];

        var colors = ColorsOf(pads, 0, maxAreaBuckets: 2);

        Assert.NotEqual(colors[2], colors[4]);
        Assert.Equal(colors[2], colors[3]);
        Assert.Equal(colors[2], colors[5]);
        Assert.Equal(colors[4], colors[1]);
    }

    private static Dictionary<int, Color> ColorsOf(
        IReadOnlyList<CadPad> pads, double tolerance, double maxAreaBuckets = 24)
    {
        var canvas = new PadCanvas
        {
            CadPads = pads,
            ColorCadByArea = true,
            AreaBucketTolerance = tolerance,
            MaxAreaBuckets = maxAreaBuckets,
        };

        PadCanvasPrivateAccess.EnsureAreaColorCache(canvas);
        var colors = PadCanvasPrivateAccess.CadAreaColorCache(canvas);
        Assert.NotNull(colors);
        Assert.Equal(pads.Count, colors.Count);
        return colors;
    }

    private static CadPad CreatePad(int id, double area)
    {
        var polygon = new Polygon2(
        [
            new Point2(0, 0),
            new Point2(area, 0),
            new Point2(area, 1),
            new Point2(0, 1),
        ]);

        return new CadPad(id, $"C{id}", "L1", polygon);
    }
}
