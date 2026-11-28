// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Pins selection order and notifications through the existing canvas operations, including on the old code.</summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasSelectionCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData("Row", 1, new[] { 40, 10 }, new[] { 10, 40 })]
    [InlineData("Column", 0, new[] { 30, 10 }, new[] { 10, 30 })]
    [InlineData("Ic", 2, new[] { 40, 20 }, new[] { 20, 40 })]
    public void AxisReplacementPreservesSourceOrderAndRaisesOneSortedEvent(
        string axis, int index, int[] expectedOrder, int[] expectedEvent)
    {
        var canvas = CreateCanvas();
        canvas.SetSelection([91, 17], [20, 10]);
        var changes = CollectChanges(canvas);
        var hit = new PadCanvas.AxisLabelHit(Enum.Parse<PadCanvas.AxisLabelKind>(axis), index, default);

        PadCanvasPrivateAccess.ApplyAxisLabelSelection(canvas, hit, additive: false);

        Assert.Empty(PadCanvasPrivateAccess.SelectedCadIds(canvas));
        Assert.Equal(expectedOrder, PadCanvasPrivateAccess.SelectedRegIdx(canvas).ToArray());
        var change = Assert.Single(changes);
        Assert.Empty(change.SelectedCadPadIds);
        Assert.Equal(expectedEvent, change.SelectedRegularPadIndices);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData("Row", 1, new[] { 20, 10, 40 }, new[] { 10, 20, 40 })]
    [InlineData("Column", 0, new[] { 20, 10, 30 }, new[] { 10, 20, 30 })]
    [InlineData("Ic", 2, new[] { 20, 10, 40 }, new[] { 10, 20, 40 })]
    public void AdditiveAxisKeepsBothSelectionsAndRaisesOneSortedEvent(
        string axis, int index, int[] expectedOrder, int[] expectedEvent)
    {
        var canvas = CreateCanvas();
        canvas.SetSelection([91, 17], [20, 10]);
        var changes = CollectChanges(canvas);
        var hit = new PadCanvas.AxisLabelHit(Enum.Parse<PadCanvas.AxisLabelKind>(axis), index, default);

        PadCanvasPrivateAccess.ApplyAxisLabelSelection(canvas, hit, additive: true);

        Assert.Equal([91, 17], PadCanvasPrivateAccess.SelectedCadIds(canvas).ToArray());
        Assert.Equal(expectedOrder, PadCanvasPrivateAccess.SelectedRegIdx(canvas).ToArray());
        var change = Assert.Single(changes);
        Assert.Equal([17, 91], change.SelectedCadPadIds);
        Assert.Equal(expectedEvent, change.SelectedRegularPadIndices);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyAxisInputKeepsTheCommitClearAndNotificationBehavior(bool additive)
    {
        var canvas = CreateCanvas();
        canvas.RegularPads = [];
        canvas.SetSelection([91], [20]);
        var changes = CollectChanges(canvas);
        var hit = new PadCanvas.AxisLabelHit(PadCanvas.AxisLabelKind.Row, 0, default);

        PadCanvasPrivateAccess.ApplyAxisLabelSelection(canvas, hit, additive);

        int[] expectedCad = additive ? [91] : [];
        int[] expectedRegular = additive ? [20] : [];
        Assert.Equal(expectedCad, PadCanvasPrivateAccess.SelectedCadIds(canvas).ToArray());
        Assert.Equal(expectedRegular, PadCanvasPrivateAccess.SelectedRegIdx(canvas).ToArray());
        var change = Assert.Single(changes);
        Assert.Equal(expectedCad, change.SelectedCadPadIds);
        Assert.Equal(expectedRegular, change.SelectedRegularPadIndices);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData("Cad", 91, false, false, new[] { 91 }, new int[0])]
    [InlineData("Regular", 20, false, false, new int[0], new[] { 20 })]
    [InlineData("Cad", 91, true, false, new[] { 91 }, new int[0])]
    [InlineData("Regular", 20, true, false, new int[0], new[] { 20 })]
    [InlineData("Cad", 91, true, true, new[] { 17 }, new[] { 20, 10 })]
    [InlineData("Regular", 20, true, true, new[] { 91, 17 }, new[] { 10 })]
    [InlineData("Cad", 52, true, true, new[] { 91, 17, 52 }, new[] { 20, 10 })]
    [InlineData("Regular", 40, true, true, new[] { 91, 17 }, new[] { 20, 10, 40 })]
    [InlineData("Cad", 91, false, true, new[] { 91, 17 }, new[] { 20, 10 })]
    [InlineData("Regular", 40, false, true, new[] { 91, 17 }, new[] { 20, 10, 40 })]
    [InlineData("None", 0, true, true, new[] { 91, 17 }, new[] { 20, 10 })]
    [InlineData("None", 0, true, false, new int[0], new int[0])]
    public void HitCommitClearsBeforeToggleAndPreservesOrderWithOneEvent(
        string kind, int id, bool toggle, bool additive, int[] expectedCad, int[] expectedRegular)
    {
        var canvas = CreateCanvas();
        canvas.SetSelection([91, 17], [20, 10]);
        var changes = CollectChanges(canvas);
        var hit = new PadCanvas.HitResult(Enum.Parse<PadCanvas.HitKind>(kind), id);

        PadCanvasPrivateAccess.ApplyHitSelection(canvas, hit, toggle, additive);

        Assert.Equal(expectedCad, PadCanvasPrivateAccess.SelectedCadIds(canvas).ToArray());
        Assert.Equal(expectedRegular, PadCanvasPrivateAccess.SelectedRegIdx(canvas).ToArray());
        var change = Assert.Single(changes);
        Assert.Equal(expectedCad.Order(), change.SelectedCadPadIds);
        Assert.Equal(expectedRegular.Order(), change.SelectedRegularPadIndices);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ToggleThenAddReusesTheRemovedSlotAndRaisesOneEventPerCommit()
    {
        var canvas = CreateCanvas();
        canvas.SetSelection([91, 17], [20, 10, 40]);
        var changes = CollectChanges(canvas);

        PadCanvasPrivateAccess.ApplyHitSelection(canvas, new(PadCanvas.HitKind.Regular, 10), toggle: true, additive: true);
        PadCanvasPrivateAccess.ApplyHitSelection(canvas, new(PadCanvas.HitKind.Regular, 30), toggle: true, additive: true);

        Assert.Equal([20, 30, 40], PadCanvasPrivateAccess.SelectedRegIdx(canvas).ToArray());
        Assert.Equal(2, changes.Count);
        Assert.Equal([20, 40], changes[0].SelectedRegularPadIndices);
        Assert.Equal([20, 30, 40], changes[1].SelectedRegularPadIndices);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(15, false, new[] { 17, 91 }, new[] { 10 })]
    [InlineData(35, false, new[] { 17 }, new[] { 10, 20 })]
    [InlineData(15, true, new[] { 17, 91 }, new[] { 10 })]
    public void ControlClickWithOptionalShiftTogglesWithoutReplacingAndRaisesOneEvent(
        double x, bool withShift, int[] expectedCad, int[] expectedRegular)
    {
        var canvas = CreateCanvas();
        canvas.SetSelection([91, 17], [20, 10]);
        var changes = CollectChanges(canvas);
        var window = OpenWindow(canvas);
        var modifiers = Avalonia.Input.RawInputModifiers.Control |
            (withShift ? Avalonia.Input.RawInputModifiers.Shift : Avalonia.Input.RawInputModifiers.None);
        try
        {
            Click(window, canvas, new Point2(x, 5), modifiers);

            var change = Assert.Single(changes);
            Assert.Equal(expectedCad, change.SelectedCadPadIds);
            Assert.Equal(expectedRegular, change.SelectedRegularPadIndices);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtendSelectionIncludesExactlyOneWorldUnitBeyondBothCenters(bool reverse)
    {
        var canvas = CreateCanvas(CreateBoundaryProbes(16));
        canvas.SetSelection([58], []);
        var changes = CollectChanges(canvas);
        var anchor = new PadCanvas.HitResult(PadCanvas.HitKind.Regular, reverse ? 40 : 30);
        PadCanvasPrivateAccess.LastSingleSelectedPad(canvas) = anchor;
        var window = OpenWindow(canvas);
        try
        {
            var current = reverse ? new Point2(5, 5) : new Point2(15, 15);
            Click(window, canvas, current, Avalonia.Input.RawInputModifiers.Shift);

            var change = Assert.Single(changes);
            Assert.Equal([51, 52, 53, 54], change.SelectedCadPadIds);
            Assert.Equal([10, 20, 30, 40], change.SelectedRegularPadIndices);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ExtendSelectionAtTheSameCenterStillUsesATwoByTwoWorldRectangle()
    {
        var canvas = CreateCanvas(CreateBoundaryProbes(6));
        var changes = CollectChanges(canvas);
        var hit = new PadCanvas.HitResult(PadCanvas.HitKind.Regular, 30);

        PadCanvasPrivateAccess.ApplyExtendSelection(canvas, hit, hit);

        var change = Assert.Single(changes);
        Assert.Equal([51, 52, 53, 54], change.SelectedCadPadIds);
        Assert.Equal([30], change.SelectedRegularPadIndices);
    }

    private static List<PadCanvasSelectionChangedEventArgs> CollectChanges(PadCanvas canvas)
    {
        var changes = new List<PadCanvasSelectionChangedEventArgs>();
        canvas.SelectionChanged += (_, e) => changes.Add(e);
        return changes;
    }

    private static Avalonia.Controls.Window OpenWindow(PadCanvas canvas)
    {
        canvas.Width = 300;
        canvas.Height = 180;
        var window = new Avalonia.Controls.Window { Content = canvas, Width = 300, Height = 180 };
        window.Show();
        canvas.Measure(new Avalonia.Size(300, 180));
        canvas.Arrange(new Avalonia.Rect(0, 0, 300, 180));
        PadCanvasPrivateAccess.Zoom(canvas) = 4;
        PadCanvasPrivateAccess.Pan(canvas) = new Avalonia.Vector(10, 160);
        PadCanvasPrivateAccess.InvalidateViewFrameSnapshot(canvas);
        return window;
    }

    private static void Click(
        Avalonia.Controls.Window window, PadCanvas canvas, Point2 world, Avalonia.Input.RawInputModifiers modifiers)
    {
        var position = Avalonia.VisualExtensions.TranslatePoint(canvas, canvas.WorldToScreen(world), window)!.Value;
        Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, position, Avalonia.Input.MouseButton.Left, modifiers);
        Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, position, Avalonia.Input.MouseButton.Left, modifiers);
    }

    private static PadCanvas CreateCanvas(IReadOnlyList<CadPad>? cadPads = null)
    {
        var canvas = new PadCanvas
        {
            Width = 100,
            Height = 100,
            ShowCad = true,
            ShowRegular = true,
            RegularPads =
            [
                new RegularPad(1, 1, 40, Rectangle(10, 10, 20, 20)) { IcIndex = 2 },
                new RegularPad(0, 0, 30, Rectangle(0, 0, 10, 10)) { IcIndex = 1 },
                new RegularPad(1, 0, 10, Rectangle(0, 10, 10, 20)) { IcIndex = 1 },
                new RegularPad(0, 1, 20, Rectangle(10, 0, 20, 10)) { IcIndex = 2 },
            ],
            CadPads = cadPads ??
            [
                new CadPad(91, "CAD-91", "L1", Rectangle(30, 0, 40, 10)),
                new CadPad(17, "CAD-17", "L1", Rectangle(40, 0, 50, 10)),
                new CadPad(52, "CAD-52", "L1", Rectangle(50, 0, 60, 10)),
            ],
        };
        canvas.Measure(new Avalonia.Size(100, 100));
        canvas.Arrange(new Avalonia.Rect(0, 0, 100, 100));
        PadCanvasPrivateAccess.Zoom(canvas) = 1.0;
        PadCanvasPrivateAccess.Pan(canvas) = new Avalonia.Vector(0, 0);
        PadCanvasPrivateAccess.InvalidateViewFrameSnapshot(canvas);
        return canvas;
    }

    private static CadPad[] CreateBoundaryProbes(double max)
    {
        return
        [
            new CadPad(51, "Left edge", "L1", Rectangle(3, 4.5, 4, 5.5)),
            new CadPad(52, "Right edge", "L1", Rectangle(max, 4.5, max + 1, 5.5)),
            new CadPad(53, "Bottom edge", "L1", Rectangle(4.5, 3, 5.5, 4)),
            new CadPad(54, "Top edge", "L1", Rectangle(4.5, max, 5.5, max + 1)),
            new CadPad(55, "Outside left", "L1", Rectangle(2.999, 4.5, 3.999, 5.5)),
            new CadPad(56, "Outside right", "L1", Rectangle(max + 0.001, 4.5, max + 1, 5.5)),
            new CadPad(57, "Outside bottom", "L1", Rectangle(4.5, 2.999, 5.5, 3.999)),
            new CadPad(58, "Outside top", "L1", Rectangle(4.5, max + 0.001, 5.5, max + 1)),
        ];
    }

    private static Polygon2 Rectangle(double minX, double minY, double maxX, double maxY) => new(
        [new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY)]);
}
