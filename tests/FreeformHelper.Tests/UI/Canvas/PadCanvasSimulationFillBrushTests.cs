using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the fill color of a regular pad in the simulation overlay for each view mode and color mode.
/// The expected colors come from the default theme fallbacks of the canvas.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasSimulationFillBrushTests
{
    private const int PadId = 3;

    [AvaloniaFact]
    public void OverlayHidden_GivesNoFill()
    {
        var canvas = BuildCanvas(NotchApplySimulationCanvasViewMode.Before, NotchApplySimulationCanvasColorMode.Auto, 5d, 3d, isChanged: true);
        canvas.ShowSimulationRegularOverlay = false;

        Assert.Null(FillOf(canvas, PadId));
    }

    [AvaloniaFact]
    public void PadWithoutOverlayCell_GivesNoFill()
    {
        var canvas = BuildCanvas(NotchApplySimulationCanvasViewMode.Before, NotchApplySimulationCanvasColorMode.Auto, 5d, 3d, isChanged: true);

        Assert.Null(FillOf(canvas, 99));
    }

    [AvaloniaFact]
    public void ChangedOnlyView_DimsUnchangedPads()
    {
        var canvas = BuildCanvas(NotchApplySimulationCanvasViewMode.ChangedOnly, NotchApplySimulationCanvasColorMode.Auto, 5d, 3d, isChanged: false);

        Assert.Equal(Color.FromArgb(18, 128, 128, 128), FillOf(canvas, PadId));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Scenarios))]
    public void FillColor_FollowsTheModeAndTheValue(
        NotchApplySimulationCanvasViewMode view,
        NotchApplySimulationCanvasColorMode colorMode,
        double threshold,
        double value,
        bool includeNegativeCell,
        string expected)
    {
        var canvas = BuildCanvas(view, colorMode, threshold, value, isChanged: true, includeNegativeCell);

        var color = FillOf(canvas, PadId);

        Assert.Equal(expected, color is { } c ? $"{c.A},{c.R},{c.G},{c.B}" : "null");
    }

    public static TheoryData<NotchApplySimulationCanvasViewMode, NotchApplySimulationCanvasColorMode, double, double, bool, string> Scenarios()
    {
        const NotchApplySimulationCanvasViewMode before = NotchApplySimulationCanvasViewMode.Before;
        const NotchApplySimulationCanvasViewMode after = NotchApplySimulationCanvasViewMode.After;
        const NotchApplySimulationCanvasViewMode delta = NotchApplySimulationCanvasViewMode.Delta;
        const NotchApplySimulationCanvasViewMode changedOnly = NotchApplySimulationCanvasViewMode.ChangedOnly;
        const NotchApplySimulationCanvasColorMode auto = NotchApplySimulationCanvasColorMode.Auto;
        const NotchApplySimulationCanvasColorMode threshold = NotchApplySimulationCanvasColorMode.Threshold;

        return new TheoryData<NotchApplySimulationCanvasViewMode, NotchApplySimulationCanvasColorMode, double, double, bool, string>
        {
            { before, auto, 5d, -4d, true, "224,0,191,255" },
            { before, auto, 5d, 3d, true, "156,172,211,20" },
            { after, auto, 5d, 10d, true, "224,205,92,92" },
            { after, auto, 5d, 0d, true, "107,50,205,50" },
            { before, threshold, 5d, -4d, true, "203,6,193,231" },
            { after, threshold, 5d, 3d, true, "180,255,166,0" },
            { after, threshold, 5d, 3d, false, "189,251,158,8" },
            { after, threshold, 0d, 3d, false, "224,50,205,50" },
            { after, threshold, 0d, 0d, false, "117,50,205,50" },
            { delta, auto, 5d, 0d, true, "91,50,205,50" },
            { delta, auto, 5d, 3d, true, "155,255,201,0" },
            { delta, auto, 5d, -4d, true, "166,20,197,174" },
            { delta, threshold, 5d, 3d, true, "187,255,166,0" },
            { delta, threshold, 5d, -4d, true, "206,6,193,231" },
            { delta, threshold, 0d, 3d, true, "224,50,205,50" },
            { changedOnly, auto, 5d, 3d, true, "155,255,201,0" },
            { changedOnly, threshold, 5d, 3d, true, "187,255,166,0" },
        };
    }

    private static PadCanvas BuildCanvas(
        NotchApplySimulationCanvasViewMode view,
        NotchApplySimulationCanvasColorMode colorMode,
        double threshold,
        double value,
        bool isChanged,
        bool includeNegativeCell = true)
    {
        var cells = new List<NotchApplySimulationAaDisplayCell>
        {
            Cell(PadId, value, isChanged),
            Cell(4, 10d, true),
            Cell(5, 6d, true),
        };
        if (includeNegativeCell)
        {
            cells.Add(Cell(6, -4d, true));
        }

        return new PadCanvas
        {
            ShowSimulationRegularOverlay = true,
            SimulationRegularOverlayViewMode = view,
            SimulationRegularOverlayColorMode = colorMode,
            SimulationRegularOverlayThresholdValue = threshold,
            SimulationRegularOverlayItems = cells,
        };
    }

    private static NotchApplySimulationAaDisplayCell Cell(int regularPadId, double value, bool isChanged)
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
            AfterValue: value,
            DeltaValue: value,
            DisplayValue: value,
            IsChanged: isChanged,
            ValueText: value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static Color? FillOf(PadCanvas canvas, int regularPadId)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(1, 0),
            new Point2(1, 1),
            new Point2(0, 1),
        });
        var pad = new RegularPad(row: 0, col: regularPadId, index: regularPadId, polygon: polygon);

        return PadCanvasPrivateAccess.TryBuildSimulationRegularFillBrush(canvas, pad) is ISolidColorBrush brush
            ? brush.Color
            : null;
    }
}
