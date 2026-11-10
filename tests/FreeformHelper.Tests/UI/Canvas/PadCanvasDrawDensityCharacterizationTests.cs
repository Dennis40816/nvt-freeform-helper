// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Pins visible pad membership through the canvas, including before the policy extraction.</summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasDrawDensityCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(0.300001, 1, new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 })]
    [InlineData(0.3, 1, new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 })]
    [InlineData(0.299999, 2, new int[] { 1, 2, 4, 5, 8, 9, 10, 13 })]
    [InlineData(0.150001, 2, new int[] { 1, 2, 4, 5, 8, 9, 10, 13 })]
    [InlineData(0.15, 2, new int[] { 1, 2, 4, 5, 8, 9, 10, 13 })]
    [InlineData(0.149999, 3, new int[] { 1, 2, 6, 7, 11 })]
    [InlineData(0.100001, 3, new int[] { 1, 2, 6, 7, 11 })]
    [InlineData(0.1, 3, new int[] { 1, 2, 6, 7, 11 })]
    [InlineData(0.099999, 4, new int[] { 1, 2, 8, 9, 13 })]
    [InlineData(0.075, 4, new int[] { 1, 2, 8, 9, 13 })]
    [InlineData(0.074999, 4, new int[] { 1, 2, 8, 9, 13 })]
    [InlineData(0.01, 4, new int[] { 1, 2, 8, 9, 13 })]
    [InlineData(0.0000005, 4, new int[] { 1, 2, 8, 9, 13 })]
    [InlineData(0.0, 4, new int[] { 1, 2, 8, 9, 13 })]
    public void RegularModuloRetainsExactIdsAcrossZoomBoundaries(double zoom, int expectedStep, int[] expectedIds)
    {
        var canvas = CreateCanvas(regularPads: CreateRegularPads(), zoom: zoom);

        Build(canvas);

        AssertRegularIds(canvas, unselected: expectedIds, selected: []);
        Assert.Equal(expectedStep, canvas.GetVisibleDrawPerfSnapshot().RegularDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(0.299999, new int[] { 1, 2, 4, 5, 8, 9, 10, 13 })]
    [InlineData(0.149999, new int[] { 1, 2, 6, 7, 11 })]
    [InlineData(0.099999, new int[] { 1, 2, 8, 9, 13 })]
    public void SelectedRegularPadsSurviveEveryDecimationStep(double zoom, int[] expectedUnselectedIds)
    {
        var canvas = CreateCanvas(regularPads: CreateRegularPads(), zoom: zoom);
        canvas.SetSelection([], [3, 12]);

        Build(canvas);

        AssertRegularIds(canvas, unselected: expectedUnselectedIds, selected: [3, 12]);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(100, 1, 100)]
    [InlineData(101, 2, 51)]
    [InlineData(401, 5, 81)]
    public void CadBudgetRetainsExactSourceOrdinals(int candidateCount, int expectedStep, int expectedRetainedCount)
    {
        var canvas = CreateCanvas(cadPads: CreateCadPads(candidateCount));
        var expectedIds = Enumerable.Range(0, expectedRetainedCount)
            .Select(ordinal => 1000 - (ordinal * expectedStep)).ToArray();

        Build(canvas);

        AssertCadIds(canvas, unselected: expectedIds, selected: []);
        Assert.Equal(expectedStep, canvas.GetVisibleDrawPerfSnapshot().CadDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void SelectedAndHighlightedCadPadsDoNotConsumeTheBudget()
    {
        var canvas = CreateCanvas(cadPads: CreateCadPads(102));
        canvas.SetSelection([999], []);
        canvas.HighlightedCadPadIds = [997];
        var expectedIds = Enumerable.Range(0, 102).Select(ordinal => 1000 - ordinal).Where(id => id != 999).ToArray();

        Build(canvas);

        AssertCadIds(canvas, unselected: expectedIds, selected: [999]);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().CadDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void SelectedAndHighlightedCadPadsDoNotAdvanceTheSamplingOrdinal()
    {
        var canvas = CreateCanvas(cadPads: CreateCadPads(103));
        canvas.SetSelection([999], []);
        canvas.HighlightedCadPadIds = [997];
        int[] expectedIds = [1000, 997, .. Enumerable.Range(0, 50).Select(ordinal => 996 - (ordinal * 2))];

        Build(canvas);

        AssertCadIds(canvas, unselected: expectedIds, selected: [999]);
        Assert.Equal(2, canvas.GetVisibleDrawPerfSnapshot().CadDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void CadMinimumScreenSizeKeepsEqualityAndExemptsSelectedAndHighlightedPads()
    {
        CadPad[] pads =
        [
            new CadPad(41, "Below", "L1", CreateRectangle(8, 8)),
            new CadPad(42, "Width equal", "L1", CreateRectangle(9, 1)),
            new CadPad(43, "Height equal", "L1", CreateRectangle(1, 9)),
            new CadPad(44, "Selected", "L1", CreateRectangle(1, 1)),
            new CadPad(45, "Highlighted", "L1", CreateRectangle(1, 1)),
        ];
        var canvas = CreateCanvas(cadPads: pads);
        canvas.SetSelection([44], []);
        canvas.HighlightedCadPadIds = [45];

        Build(canvas);

        AssertCadIds(canvas, unselected: [42, 43, 45], selected: [44]);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().CadDecimatedCount);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ZeroCandidatesRetainsEmptyListsAndUnitSteps()
    {
        var canvas = CreateCanvas(zoom: 0.3);

        Build(canvas);

        AssertRegularIds(canvas, unselected: [], selected: []);
        AssertCadIds(canvas, unselected: [], selected: []);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().RegularDecimationStep);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().CadDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void LowDetailOffRetainsEveryPadEvenBelowTheScreenSizeThreshold()
    {
        var canvas = CreateCanvas(CreateRegularPads(), CreateCadPads(101), zoom: 0.01);

        Build(canvas, lowDetailMode: false);

        AssertRegularIds(canvas, unselected: Enumerable.Range(1, 13).ToArray(), selected: []);
        AssertCadIds(canvas, unselected: Enumerable.Range(0, 101).Select(ordinal => 1000 - ordinal).ToArray(), selected: []);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().RegularDecimationStep);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().CadDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void HiddenLayersRetainEmptyListsAndUnitSteps()
    {
        var canvas = CreateCanvas(CreateRegularPads(), CreateCadPads(101), zoom: 0.01);

        PadCanvasPrivateAccess.BuildVisibleDrawLists(canvas, new Rect2(-5, -5, 100, 100), false, false, true, 0.3);

        AssertRegularIds(canvas, unselected: [], selected: []);
        AssertCadIds(canvas, unselected: [], selected: []);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().RegularDecimationStep);
        Assert.Equal(1, canvas.GetVisibleDrawPerfSnapshot().CadDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ReusingTheDrawCachePreservesExactRetainedIds()
    {
        var canvas = CreateCanvas(CreateRegularPads(), CreateCadPads(101), zoom: 0.1);
        canvas.SetSelection([999], [3]);
        canvas.HighlightedCadPadIds = [997];
        Build(canvas);
        AssertRegularIds(canvas, unselected: [1, 2, 6, 7, 11], selected: [3]);
        var expectedCadIds = Enumerable.Range(0, 101).Select(ordinal => 1000 - ordinal).Where(id => id != 999).ToArray();
        AssertCadIds(canvas, unselected: expectedCadIds, selected: [999]);

        Build(canvas);

        AssertRegularIds(canvas, unselected: [1, 2, 6, 7, 11], selected: [3]);
        AssertCadIds(canvas, unselected: expectedCadIds, selected: [999]);
        Assert.True(canvas.GetVisibleDrawPerfSnapshot().QueryCacheHit);
        Assert.True(canvas.GetVisibleDrawPerfSnapshot().DrawListCacheHit);
    }

    private static PadCanvas CreateCanvas(
        IReadOnlyList<RegularPad>? regularPads = null,
        IReadOnlyList<CadPad>? cadPads = null,
        double zoom = 0.1)
    {
        var canvas = new PadCanvas
        {
            RegularPads = regularPads ?? [],
            CadPads = cadPads ?? [],
        };
        canvas.Resources["CanvasLowDetailRegularDecimationMaxStep"] = 4.0;
        canvas.Resources["CanvasLowDetailCadMaxVisibleUnselected"] = 100.0;
        canvas.Resources["CanvasLowDetailCadMinScreenSize"] = 0.9;
        PadCanvasPrivateAccess.Zoom(canvas) = zoom;
        return canvas;
    }

    private static void Build(PadCanvas canvas, bool lowDetailMode = true)
    {
        PadCanvasPrivateAccess.BuildVisibleDrawLists(
            canvas, new Rect2(-5, -5, 100, 100), showRegular: true, showCad: true, lowDetailMode, lowDetailZoomThreshold: 0.3);
    }

    private static void AssertRegularIds(PadCanvas canvas, int[] unselected, int[] selected)
    {
        Assert.Equal(unselected, PadCanvasPrivateAccess.VisibleRegularUnselected(canvas).Select(pad => pad.Index).ToArray());
        Assert.Equal(selected, PadCanvasPrivateAccess.VisibleRegularSelected(canvas).Select(pad => pad.Index).ToArray());
    }

    private static void AssertCadIds(PadCanvas canvas, int[] unselected, int[] selected)
    {
        Assert.Equal(unselected, PadCanvasPrivateAccess.VisibleCadUnselected(canvas).Select(pad => pad.Id).ToArray());
        Assert.Equal(selected, PadCanvasPrivateAccess.VisibleCadSelected(canvas).Select(pad => pad.Id).ToArray());
    }

    private static RegularPad[] CreateRegularPads()
    {
        // The first two pads have a zero axis; the final four have negative row or column values.
        (int Row, int Col)[] cells =
        [
            (0, 1), (1, 0), (1, 1), (2, 1), (1, 2), (3, 1), (1, 3),
            (4, 1), (1, 4), (-2, -1), (-1, -3), (-1, -1), (-4, -1),
        ];
        return cells.Select((cell, ordinal) =>
            new RegularPad(cell.Row, cell.Col, ordinal + 1, CreateRectangle(20, 20))).ToArray();
    }

    private static CadPad[] CreateCadPads(int count)
    {
        return Enumerable.Range(0, count).Select(ordinal =>
            new CadPad(1000 - ordinal, "CAD", "L1", CreateRectangle(20, 20))).ToArray();
    }

    private static Polygon2 CreateRectangle(double width, double height)
    {
        return new Polygon2([new Point2(0, 0), new Point2(width, 0), new Point2(width, height), new Point2(0, height)]);
    }
}
