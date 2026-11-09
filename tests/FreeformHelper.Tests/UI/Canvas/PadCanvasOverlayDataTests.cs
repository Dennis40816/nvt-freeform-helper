using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how a <see cref="PadCanvas"/> copies the overlay data of its styled properties into the collections it draws
/// from: the highlighted and diff index CAD ids, the Notch preview items with their To Full polygons, and the
/// simulation overlay cells with their value range.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasOverlayDataTests
{
    private static readonly Polygon2 _firstSeed = Square(0);
    private static readonly Polygon2 _firstCandidate = Square(10);
    private static readonly Polygon2 _disabledSeed = Square(20);
    private static readonly Polygon2 _disabledCandidate = Square(30);

    [AvaloniaFact]
    public void HighlightedCadPadIds_ReplaceTheHighlightedSet()
    {
        var canvas = new PadCanvas();

        canvas.HighlightedCadPadIds = [1, 2, 2];
        AssertSame(PadCanvasPrivateAccess.HighlightedCadIds(canvas), 1, 2);

        canvas.HighlightedCadPadIds = [3];
        AssertSame(PadCanvasPrivateAccess.HighlightedCadIds(canvas), 3);

        canvas.HighlightedCadPadIds = null;
        Assert.Empty(PadCanvasPrivateAccess.HighlightedCadIds(canvas));
    }

    [AvaloniaFact]
    public void DiffIndexOverrideCadPadIds_ReplaceTheOverrideSet()
    {
        var canvas = new PadCanvas();

        canvas.DiffIndexOverrideCadPadIds = [4, 5];
        AssertSame(PadCanvasPrivateAccess.DiffIndexOverrideCadIds(canvas), 4, 5);

        canvas.DiffIndexOverrideCadPadIds = [6];
        AssertSame(PadCanvasPrivateAccess.DiffIndexOverrideCadIds(canvas), 6);

        canvas.DiffIndexOverrideCadPadIds = null;
        Assert.Empty(PadCanvasPrivateAccess.DiffIndexOverrideCadIds(canvas));
        Assert.Empty(PadCanvasPrivateAccess.DiffIndexAnchorCadPadIds(canvas));
    }

    [AvaloniaFact]
    public void DiffIndexAnchorCadPadIds_ReplaceTheAnchorSet()
    {
        var canvas = new PadCanvas();

        canvas.DiffIndexAnchorCadPadIds = [7, 8];
        AssertSame(PadCanvasPrivateAccess.DiffIndexAnchorCadPadIds(canvas), 7, 8);

        canvas.DiffIndexAnchorCadPadIds = [9];
        AssertSame(PadCanvasPrivateAccess.DiffIndexAnchorCadPadIds(canvas), 9);

        canvas.DiffIndexAnchorCadPadIds = null;
        Assert.Empty(PadCanvasPrivateAccess.DiffIndexAnchorCadPadIds(canvas));
        Assert.Empty(PadCanvasPrivateAccess.DiffIndexOverrideCadIds(canvas));
    }

    [AvaloniaFact]
    public void NotchCanvasPreviewItems_IndexEveryItemAndCollectThePolygonsOfEnabledItemsOnly()
    {
        var canvas = new PadCanvas();
        var enabled = PreviewItem(11, isToFullEnabled: true, [_firstSeed], [_firstCandidate]);
        var disabled = PreviewItem(12, isToFullEnabled: false, [_disabledSeed], [_disabledCandidate]);

        canvas.NotchCanvasPreviewItems = [enabled, disabled];

        AssertSame(PadCanvasPrivateAccess.NotchPreviewByCadId(canvas).Keys, 11, 12);
        Assert.Same(enabled, PadCanvasPrivateAccess.NotchPreviewByCadId(canvas)[11]);
        Assert.Same(disabled, PadCanvasPrivateAccess.NotchPreviewByCadId(canvas)[12]);
        Assert.Equal([_firstSeed], PadCanvasPrivateAccess.NotchToFullSeedPolygons(canvas));
        Assert.Equal([_firstCandidate], PadCanvasPrivateAccess.NotchToFullCandidatePolygons(canvas));
    }

    [AvaloniaFact]
    public void NotchCanvasPreviewItems_ReplaceThePreviousItemsAndPolygons()
    {
        var canvas = new PadCanvas();
        canvas.NotchCanvasPreviewItems = [PreviewItem(11, isToFullEnabled: true, [_firstSeed], [_firstCandidate])];

        canvas.NotchCanvasPreviewItems = [PreviewItem(13, isToFullEnabled: true, [_disabledSeed], [])];

        AssertSame(PadCanvasPrivateAccess.NotchPreviewByCadId(canvas).Keys, 13);
        Assert.Equal([_disabledSeed], PadCanvasPrivateAccess.NotchToFullSeedPolygons(canvas));
        Assert.Empty(PadCanvasPrivateAccess.NotchToFullCandidatePolygons(canvas));

        canvas.NotchCanvasPreviewItems = null;
        Assert.Empty(PadCanvasPrivateAccess.NotchPreviewByCadId(canvas));
        Assert.Empty(PadCanvasPrivateAccess.NotchToFullSeedPolygons(canvas));
        Assert.Empty(PadCanvasPrivateAccess.NotchToFullCandidatePolygons(canvas));
    }

    [AvaloniaFact]
    public void SimulationRegularOverlayItems_IndexTheCellsAndSetTheValueRange()
    {
        var canvas = new PadCanvas();
        var cells = new[] { Cell(1, display: -2d, delta: -3d), Cell(2, display: 4d, delta: 1d), Cell(3, display: 10d, delta: 2d) };

        canvas.SimulationRegularOverlayItems = cells;

        AssertSame(PadCanvasPrivateAccess.SimulationOverlayByRegularPadId(canvas).Keys, 1, 2, 3);
        Assert.Same(cells[1], PadCanvasPrivateAccess.SimulationOverlayByRegularPadId(canvas)[2]);
        var range = SimulationColorScaleResolver.ComputeAutoScaleRange([-2d, 4d, 10d]);
        Assert.Equal(range.Minimum, PadCanvasPrivateAccess.SimulationOverlayMinValue(canvas));
        Assert.Equal(range.Maximum, PadCanvasPrivateAccess.SimulationOverlayMaxValue(canvas));
        Assert.Equal(range.NegativeClampAbs, PadCanvasPrivateAccess.SimulationOverlayAutoNegativeClampAbs(canvas));
        Assert.Equal(range.PositiveClamp, PadCanvasPrivateAccess.SimulationOverlayAutoPositiveClamp(canvas));
        Assert.Equal(3d, PadCanvasPrivateAccess.SimulationOverlayMaxAbsDelta(canvas));
    }

    [AvaloniaFact]
    public void SimulationRegularOverlayItems_ReplaceThePreviousCellsAndTheRange()
    {
        var canvas = new PadCanvas();
        canvas.SimulationRegularOverlayItems = [Cell(1, display: 5d, delta: 5d)];

        canvas.SimulationRegularOverlayItems = [Cell(2, display: 1d, delta: -0.5d)];

        AssertSame(PadCanvasPrivateAccess.SimulationOverlayByRegularPadId(canvas).Keys, 2);
        Assert.Equal(0.5d, PadCanvasPrivateAccess.SimulationOverlayMaxAbsDelta(canvas));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SimulationRegularOverlayItems_WithoutCells_ClearTheIndexAndZeroTheRange(bool useNull)
    {
        var canvas = new PadCanvas();
        canvas.SimulationRegularOverlayItems = [Cell(1, display: -2d, delta: -3d), Cell(2, display: 4d, delta: 1d)];

        canvas.SimulationRegularOverlayItems = useNull ? null : [];

        Assert.Empty(PadCanvasPrivateAccess.SimulationOverlayByRegularPadId(canvas));
        Assert.Equal(0d, PadCanvasPrivateAccess.SimulationOverlayMinValue(canvas));
        Assert.Equal(0d, PadCanvasPrivateAccess.SimulationOverlayMaxValue(canvas));
        Assert.Equal(0d, PadCanvasPrivateAccess.SimulationOverlayMaxAbsDelta(canvas));
        Assert.Equal(0d, PadCanvasPrivateAccess.SimulationOverlayAutoNegativeClampAbs(canvas));
        Assert.Equal(0d, PadCanvasPrivateAccess.SimulationOverlayAutoPositiveClamp(canvas));
    }

    private static void AssertSame<T>(IEnumerable<T> actual, params T[] expected)
        where T : notnull
    {
        Assert.Equal(expected.Order(), actual.Order());
    }

    private static Polygon2 Square(double minX) => new(new[]
    {
        new Point2(minX, 0), new Point2(minX + 5, 0), new Point2(minX + 5, 5), new Point2(minX, 5),
    });

    private static NotchCanvasPreviewItem PreviewItem(
        int cadPadId,
        bool isToFullEnabled,
        IReadOnlyList<Polygon2> seedPolygons,
        IReadOnlyList<Polygon2> candidatePolygons)
    {
        return new NotchCanvasPreviewItem(cadPadId, 0.5d, 0.75d, isToFullEnabled, seedPolygons, candidatePolygons, [], []);
    }

    private static NotchApplySimulationAaDisplayCell Cell(int regularPadId, double display, double delta)
    {
        return new NotchApplySimulationAaDisplayCell(
            RegularPadId: regularPadId,
            OriginalRow: 0,
            OriginalCol: regularPadId,
            DisplayRow: 0,
            DisplayCol: regularPadId,
            IcIndex: 0,
            DiffIndex: regularPadId,
            BeforeValue: 0d,
            AfterValue: display,
            DeltaValue: delta,
            DisplayValue: display,
            IsChanged: true,
            ValueText: display.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }
}
