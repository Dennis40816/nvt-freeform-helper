// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;
using DrawInputs = (
    FreeformHelper.UI.Controls.PadCanvasQueryState Query,
    FreeformHelper.UI.Controls.PadCanvasVisibleDrawState State,
    FreeformHelper.UI.Controls.PadCanvasVisibleDrawBuffers Buffers);

namespace FreeformHelper.Tests;

/// <summary>Pins visible pad membership using supplied state and buffers, with control cache integration coverage.</summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasDrawDensityCharacterizationTests
{
    [Theory]
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
        var inputs = CreateInputs(regularPads: CreateRegularPads(), zoom: zoom);

        var metrics = Build(inputs);

        AssertRegularIds(inputs.Buffers, unselected: expectedIds, selected: []);
        Assert.Equal(expectedStep, metrics.RegularDecimationStep);
    }

    [Theory]
    [InlineData(0.299999, new int[] { 1, 2, 4, 5, 8, 9, 10, 13 })]
    [InlineData(0.149999, new int[] { 1, 2, 6, 7, 11 })]
    [InlineData(0.099999, new int[] { 1, 2, 8, 9, 13 })]
    public void SelectedRegularPadsSurviveEveryDecimationStep(double zoom, int[] expectedUnselectedIds)
    {
        var inputs = CreateInputs(regularPads: CreateRegularPads(), zoom: zoom);
        inputs.State = inputs.State with { SelectedRegularIndices = new HashSet<int> { 3, 12 } };

        Build(inputs);

        AssertRegularIds(inputs.Buffers, unselected: expectedUnselectedIds, selected: [3, 12]);
    }

    [Theory]
    [InlineData(100, 1, 100)]
    [InlineData(101, 2, 51)]
    [InlineData(401, 5, 81)]
    public void CadBudgetRetainsExactSourceOrdinals(int candidateCount, int expectedStep, int expectedRetainedCount)
    {
        var inputs = CreateInputs(cadPads: CreateCadPads(candidateCount));
        var expectedIds = Enumerable.Range(0, expectedRetainedCount)
            .Select(ordinal => 1000 - (ordinal * expectedStep)).ToArray();

        var metrics = Build(inputs);

        AssertCadIds(inputs.Buffers, unselected: expectedIds, selected: []);
        Assert.Equal(expectedStep, metrics.CadDecimationStep);
    }

    [Fact]
    public void SelectedAndHighlightedCadPadsDoNotConsumeTheBudget()
    {
        var inputs = CreateInputs(cadPads: CreateCadPads(102));
        inputs.State = inputs.State with { SelectedCadIds = new HashSet<int> { 999 } };
        inputs.State = inputs.State with { HighlightedCadIds = new HashSet<int> { 997 } };
        var expectedIds = Enumerable.Range(0, 102).Select(ordinal => 1000 - ordinal).Where(id => id != 999).ToArray();

        var metrics = Build(inputs);

        AssertCadIds(inputs.Buffers, unselected: expectedIds, selected: [999]);
        Assert.Equal(1, metrics.CadDecimationStep);
    }

    [Fact]
    public void SelectedAndHighlightedCadPadsDoNotAdvanceTheSamplingOrdinal()
    {
        var inputs = CreateInputs(cadPads: CreateCadPads(103));
        inputs.State = inputs.State with { SelectedCadIds = new HashSet<int> { 999 } };
        inputs.State = inputs.State with { HighlightedCadIds = new HashSet<int> { 997 } };
        int[] expectedIds = [1000, 997, .. Enumerable.Range(0, 50).Select(ordinal => 996 - (ordinal * 2))];

        var metrics = Build(inputs);

        AssertCadIds(inputs.Buffers, unselected: expectedIds, selected: [999]);
        Assert.Equal(2, metrics.CadDecimationStep);
    }

    [Fact]
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
        var inputs = CreateInputs(cadPads: pads);
        inputs.State = inputs.State with { SelectedCadIds = new HashSet<int> { 44 } };
        inputs.State = inputs.State with { HighlightedCadIds = new HashSet<int> { 45 } };

        var metrics = Build(inputs);

        AssertCadIds(inputs.Buffers, unselected: [42, 43, 45], selected: [44]);
        Assert.Equal(1, metrics.CadDecimatedCount);
    }

    [Fact]
    public void ZeroCandidatesRetainsEmptyListsAndUnitSteps()
    {
        var inputs = CreateInputs(zoom: 0.3);

        var metrics = Build(inputs);

        AssertRegularIds(inputs.Buffers, unselected: [], selected: []);
        AssertCadIds(inputs.Buffers, unselected: [], selected: []);
        Assert.Equal(1, metrics.RegularDecimationStep);
        Assert.Equal(1, metrics.CadDecimationStep);
    }

    [Fact]
    public void LowDetailOffRetainsEveryPadEvenBelowTheScreenSizeThreshold()
    {
        var inputs = CreateInputs(CreateRegularPads(), CreateCadPads(101), zoom: 0.01);

        var metrics = Build(inputs, lowDetailMode: false);

        AssertRegularIds(inputs.Buffers, unselected: Enumerable.Range(1, 13).ToArray(), selected: []);
        AssertCadIds(inputs.Buffers, unselected: Enumerable.Range(0, 101).Select(ordinal => 1000 - ordinal).ToArray(), selected: []);
        Assert.Equal(1, metrics.RegularDecimationStep);
        Assert.Equal(1, metrics.CadDecimationStep);
    }

    [Fact]
    public void HiddenLayersRetainEmptyListsAndUnitSteps()
    {
        var inputs = CreateInputs(CreateRegularPads(), CreateCadPads(101), zoom: 0.01);

        var metrics = Build(inputs, showRegular: false, showCad: false);

        AssertRegularIds(inputs.Buffers, unselected: [], selected: []);
        AssertCadIds(inputs.Buffers, unselected: [], selected: []);
        Assert.Equal(1, metrics.RegularDecimationStep);
        Assert.Equal(1, metrics.CadDecimationStep);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ReusingTheDrawCachePreservesExactRetainedIds()
    {
        var canvas = CreateCachedCanvas(CreateRegularPads(), CreateCadPads(101), zoom: 0.1);
        canvas.SetSelection([999], [3]);
        canvas.HighlightedCadPadIds = [997];
        Build(canvas);
        AssertRegularIds(canvas.VisibleDrawBuffers, unselected: [1, 2, 6, 7, 11], selected: [3]);
        var expectedCadIds = Enumerable.Range(0, 101).Select(ordinal => 1000 - ordinal).Where(id => id != 999).ToArray();
        AssertCadIds(canvas.VisibleDrawBuffers, unselected: expectedCadIds, selected: [999]);
        var buffers = canvas.VisibleDrawBuffers;
        using var regularUnselected = buffers.RegularUnselected.GetEnumerator();
        using var regularSelected = buffers.RegularSelected.GetEnumerator();
        using var cadUnselected = buffers.CadUnselected.GetEnumerator();
        using var cadSelected = buffers.CadSelected.GetEnumerator();
        var firstRevision = canvas.GetVisibleDrawPerfSnapshot().Revision;

        Build(canvas);

        AssertRegularIds(canvas.VisibleDrawBuffers, unselected: [1, 2, 6, 7, 11], selected: [3]);
        AssertCadIds(canvas.VisibleDrawBuffers, unselected: expectedCadIds, selected: [999]);
        Assert.True(canvas.GetVisibleDrawPerfSnapshot().QueryCacheHit);
        Assert.True(canvas.GetVisibleDrawPerfSnapshot().DrawListCacheHit);
        Assert.Same(buffers.RegularUnselected, canvas.VisibleDrawBuffers.RegularUnselected);
        Assert.Same(buffers.RegularSelected, canvas.VisibleDrawBuffers.RegularSelected);
        Assert.Same(buffers.CadUnselected, canvas.VisibleDrawBuffers.CadUnselected);
        Assert.Same(buffers.CadSelected, canvas.VisibleDrawBuffers.CadSelected);
        Assert.True(regularUnselected.MoveNext());
        Assert.True(regularSelected.MoveNext());
        Assert.True(cadUnselected.MoveNext());
        Assert.True(cadSelected.MoveNext());
        Assert.Equal(firstRevision + 1, canvas.GetVisibleDrawPerfSnapshot().Revision);
    }

    private static PadCanvas CreateCachedCanvas(
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

    private static DrawInputs CreateInputs(
        IReadOnlyList<RegularPad>? regularPads = null,
        IReadOnlyList<CadPad>? cadPads = null,
        double zoom = 0.1) =>
        (new PadCanvasQueryState(cadPads ?? [], regularPads ?? [], null, null, true, true),
         new PadCanvasVisibleDrawState(true, true, true, zoom, 0.3, 4.0, 100.0, 0.9,
             new HashSet<int>(), new HashSet<int>(), new HashSet<int>()),
         new PadCanvasVisibleDrawBuffers([], [], [], []));

    private static PadCanvas.PadCanvasVisibleDrawListMetrics Build(
        DrawInputs inputs, bool lowDetailMode = true, bool showRegular = true, bool showCad = true)
    {
        var query = inputs.Query with { ShowRegular = showRegular, ShowCad = showCad };
        var candidates = PadCanvasVisibleDrawListBuilder.Query(new Rect2(-5, -5, 100, 100), query);
        var state = inputs.State with { ShowRegular = showRegular, ShowCad = showCad, LowDetailMode = lowDetailMode };
        return PadCanvasVisibleDrawListBuilder.Build(candidates.Regular, candidates.Cad, state, inputs.Buffers);
    }

    private static void AssertRegularIds(PadCanvasVisibleDrawBuffers buffers, int[] unselected, int[] selected)
    {
        Assert.Equal(unselected, buffers.RegularUnselected.Select(pad => pad.Index).ToArray());
        Assert.Equal(selected, buffers.RegularSelected.Select(pad => pad.Index).ToArray());
    }

    private static void AssertCadIds(PadCanvasVisibleDrawBuffers buffers, int[] unselected, int[] selected)
    {
        Assert.Equal(unselected, buffers.CadUnselected.Select(pad => pad.Id).ToArray());
        Assert.Equal(selected, buffers.CadSelected.Select(pad => pad.Id).ToArray());
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
