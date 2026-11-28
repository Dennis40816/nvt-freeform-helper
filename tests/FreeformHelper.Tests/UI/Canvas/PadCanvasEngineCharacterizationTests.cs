// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Pins polygon rejection, candidate order and cache invalidation across the engine seams.</summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasEngineCharacterizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CadHitRejectsBoundsOnlyContainment(bool indexed)
    {
        var triangle = new CadPad(91, "Triangle", "L1", new Polygon2(
            [new Point2(0, 0), new Point2(10, 0), new Point2(0, 10)]));
        var state = QueryState([triangle], [], indexed);
        var world = new Point2(9, 9);
        Assert.True(triangle.Bounds.Contains(world));

        var hit = PadCanvasSelectionEngine.HitTest(world, state);

        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.None, 0), hit);
    }

    [Theory]
    [InlineData(false, false, 91)]
    [InlineData(false, true, 17)]
    [InlineData(true, false, 91)]
    [InlineData(true, true, 17)]
    public void CadEqualAreaHitKeepsFirstCandidate(bool indexed, bool reverse, int expectedId)
    {
        CadPad[] pads = [Cad(91, 0, 0, 10, 10), Cad(17, 0, 0, 10, 10)];
        var state = QueryState(reverse ? pads.Reverse().ToArray() : pads, [], indexed);

        var hit = PadCanvasSelectionEngine.HitTest(new Point2(5, 5), state);

        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.Cad, expectedId), hit);
    }

    [Theory]
    [InlineData(true, new[] { 70, 10, 90 }, new[] { 30, 20, 10, 40 }, 3, 4)]
    [InlineData(false, new[] { 90, 70, 10 }, new[] { 40, 30, 10, 20 }, 5, 5)]
    public void RectangleSelectionPreservesQueryOrderAndCandidateCounts(
        bool indexed, int[] cadIds, int[] regularIndices, int cadCandidates, int regularCandidates)
    {
        var state = CreateQueryState(indexed);

        var result = PadCanvasSelectionEngine.SelectPadsInWorldRect(Viewport, state);

        Assert.Equal(cadIds, result.CadIds.ToArray());
        Assert.Equal(regularIndices, result.RegularIndices.ToArray());
        Assert.Equal(cadCandidates, result.CadCandidates);
        Assert.Equal(regularCandidates, result.RegularCandidates);
        Assert.Equal(indexed, result.UsedCadIndex);
        Assert.Equal(indexed, result.UsedRegularIndex);
    }

    [Fact]
    public void IndexedViewportPreservesBucketOrder()
    {
        var state = CreateQueryState(indexed: true);

        var candidates = PadCanvasVisibleDrawListBuilder.Query(Viewport, state);

        Assert.Equal([70, 10, 90], candidates.Cad.Select(pad => pad.Id).ToArray());
        Assert.Equal([30, 20, 10, 40], candidates.Regular.Select(pad => pad.Index).ToArray());
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PadReplacementInvalidatesBothVisibleCaches(bool replaceCad)
    {
        var canvas = new PadCanvas
        {
            CadPads = [Cad(1, 0, 0, 10, 10)],
            RegularPads = [new RegularPad(0, 0, 1, Rectangle(0, 0, 10, 10))],
        };
        var viewport = new Rect2(-1, -1, 11, 11);
        canvas.SetSelection([2], [2]);
        Build(canvas, viewport);
        Build(canvas, viewport);
        Assert.True(canvas.GetVisibleDrawPerfSnapshot().QueryCacheHit);
        Assert.True(canvas.GetVisibleDrawPerfSnapshot().DrawListCacheHit);

        if (replaceCad)
        {
            canvas.CadPads = [Cad(2, 1, 1, 9, 9)];
        }
        else
        {
            canvas.RegularPads = [new RegularPad(0, 0, 2, Rectangle(1, 1, 9, 9))];
        }

        Build(canvas, viewport);

        var refreshed = canvas.GetVisibleDrawPerfSnapshot();
        Assert.False(refreshed.QueryCacheHit);
        Assert.False(refreshed.DrawListCacheHit);
        Assert.Equal(replaceCad ? 1 : 0, refreshed.VisibleCadSelected);
        Assert.Equal(replaceCad ? 0 : 1, refreshed.VisibleRegularSelected);
        Assert.Equal(replaceCad ? 0 : 1, refreshed.VisibleCadUnselected);
        Assert.Equal(replaceCad ? 1 : 0, refreshed.VisibleRegularUnselected);

        if (replaceCad)
        {
            canvas.CadPads = [Cad(3, 100, 100, 110, 110)];
        }
        else
        {
            canvas.RegularPads = [new RegularPad(0, 0, 3, Rectangle(100, 100, 110, 110))];
        }

        Build(canvas, viewport);

        var moved = canvas.GetVisibleDrawPerfSnapshot();
        Assert.False(moved.QueryCacheHit);
        Assert.False(moved.DrawListCacheHit);
        Assert.Equal(replaceCad ? 0 : 1, moved.CadCandidates);
        Assert.Equal(replaceCad ? 1 : 0, moved.RegularCandidates);
    }

    private static Rect2 Viewport => new(-1, -1, 19, 19);

    private static PadCanvasQueryState CreateQueryState(bool indexed) => QueryState(
        [
            Cad(90, 16, 16, 18, 18), Cad(70, 0, 0, 20, 20), Cad(10, 1, 1, 2, 2),
            Cad(70, 0, 0, 20, 20), Cad(60, 30, 30, 31, 31),
        ],
        [
            new RegularPad(1, 1, 40, Rectangle(10, 10, 20, 20)),
            new RegularPad(0, 0, 30, Rectangle(0, 0, 10, 10)),
            new RegularPad(1, 0, 10, Rectangle(0, 10, 10, 20)),
            new RegularPad(0, 1, 20, Rectangle(10, 0, 20, 10)),
            new RegularPad(2, 0, 50, Rectangle(0, 20, 10, 30)),
        ], indexed);

    private static PadCanvasQueryState QueryState(CadPad[] cad, RegularPad[] regular, bool indexed) =>
        new(cad, regular, indexed ? new PadCanvas.CadSpatialIndex(cad) : null,
            indexed && regular.Length > 0 ? new PadCanvas.RegularSpatialIndex(regular) : null, true, true);

    private static void Build(PadCanvas canvas, Rect2 viewport) =>
        PadCanvasPrivateAccess.BuildVisibleDrawLists(canvas, viewport, true, true, false, 0.3);

    private static CadPad Cad(int id, double minX, double minY, double maxX, double maxY) =>
        new(id, "CAD", "L1", Rectangle(minX, minY, maxX, maxY));

    private static Polygon2 Rectangle(double minX, double minY, double maxX, double maxY) => new(
        [new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY)]);
}
