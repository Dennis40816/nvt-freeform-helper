using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasHitTestTests
{
    [AvaloniaFact]
    public void TryHitRegular_FallsBackToLinearScan_WhenSpatialBucketMisses()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };

        // This point is inside row1-col0, but the spatial bucket (built from global edges)
        // may map to row1-col1 when per-row widths diverge.
        var hit = InvokeTryHitRegular(canvas, new Point2(15, 15));

        Assert.Equal("Regular", ReadHitKind(hit));
        Assert.Equal(2, ReadHitIdOrIndex(hit));
    }

    [AvaloniaFact]
    public void EnsureRegularSpatialIndex_DisablesIndex_ForNonSeparableGrid()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };

        var ensureMethod = typeof(PadCanvas).GetMethod("EnsureRegularSpatialIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(ensureMethod);
        ensureMethod!.Invoke(canvas, null);

        var field = typeof(PadCanvas).GetField("_regularIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Null(field!.GetValue(canvas));
    }

    [AvaloniaFact]
    public void SelectPadsInWorldRect_UsesLinearFallback_WhenIndexDisabled()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };

        var selectMethod = typeof(PadCanvas).GetMethod("SelectPadsInWorldRect", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(selectMethod);
        selectMethod!.Invoke(canvas, new object[] { new Rect2(11, 11, 19, 19), false, true });

        var selected = ReadSelectedRegularIndices(canvas);
        Assert.Contains(2, selected);
        Assert.DoesNotContain(3, selected);
    }

    [AvaloniaFact]
    public void SelectPadsInWorldRect_NonAdditive_ReplacesExistingSelection()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 20, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 30, maxX: 10, maxY: 40),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };
        canvas.SetSelection([], [0, 1]);

        var selectMethod = typeof(PadCanvas).GetMethod("SelectPadsInWorldRect", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(selectMethod);
        selectMethod!.Invoke(canvas, [new Rect2(1, 31, 9, 39), false, true]);

        Assert.Equal([2], ReadSelectedRegularIndices(canvas));
        Assert.Empty(ReadSelectedCadIds(canvas));
    }

    [AvaloniaFact]
    public void SelectPadsInWorldRect_Additive_PreservesExistingSelection()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 20, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 30, maxX: 10, maxY: 40),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };
        canvas.SetSelection([], [0]);

        var selectMethod = typeof(PadCanvas).GetMethod("SelectPadsInWorldRect", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(selectMethod);
        selectMethod!.Invoke(canvas, [new Rect2(1, 31, 9, 39), true, true]);

        Assert.Equal([0, 2], ReadSelectedRegularIndices(canvas));
        Assert.Empty(ReadSelectedCadIds(canvas));
    }

    [AvaloniaFact]
    public void EnsureRegularSpatialIndex_RebuildsAfterRegularPadsReplacement()
    {
        var separablePads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 20, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 10, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 10, minY: 10, maxX: 20, maxY: 20),
        };

        var nonSeparablePads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = separablePads,
        };

        var ensureMethod = typeof(PadCanvas).GetMethod("EnsureRegularSpatialIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        var field = typeof(PadCanvas).GetField("_regularIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(ensureMethod);
        Assert.NotNull(field);

        ensureMethod!.Invoke(canvas, null);
        Assert.NotNull(field!.GetValue(canvas));

        canvas.RegularPads = nonSeparablePads;
        ensureMethod.Invoke(canvas, null);
        Assert.Null(field.GetValue(canvas));
    }

    [AvaloniaFact]
    public void HitTestForHoverDebug_RegularOnly_IgnoresShowRegularVisibility()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 7, minX: 0, minY: 0, maxX: 10, maxY: 10),
        };
        pads[0].DiffIndex = 42;

        var canvas = new PadCanvas
        {
            Width = 100,
            Height = 100,
            ShowRegular = false,
            RegularPads = pads,
        };
        canvas.Measure(new Avalonia.Size(100, 100));
        canvas.Arrange(new Avalonia.Rect(0, 0, 100, 100));
        SetCanvasView(canvas, zoom: 5.0, panX: 25.0, panY: 75.0);
        var screenPoint = GetScreenPointForWorld(canvas, pads[0].Centroid);

        var hit = InvokeHitTestForHoverDebug(canvas, screenPoint, regularOnly: true);

        Assert.Equal("Regular", ReadHitKind(hit));
        Assert.Equal(7, ReadHitIdOrIndex(hit));
    }

    [AvaloniaFact]
    public void BuildHoverDebugSnapshot_RegularOnly_StillShowsDiff_WhenRegularIsHidden()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 7, minX: 0, minY: 0, maxX: 10, maxY: 10),
        };
        pads[0].DiffIndex = 42;

        var canvas = new PadCanvas
        {
            Width = 100,
            Height = 100,
            ShowRegular = false,
            RegularPads = pads,
        };
        canvas.Measure(new Avalonia.Size(100, 100));
        canvas.Arrange(new Avalonia.Rect(0, 0, 100, 100));
        SetCanvasView(canvas, zoom: 5.0, panX: 25.0, panY: 75.0);
        var screenPoint = GetScreenPointForWorld(canvas, pads[0].Centroid);

        var hit = InvokeHitTestForHoverDebug(canvas, screenPoint, regularOnly: true);
        SetPrivateField(canvas, "_hoverDebugHit", hit);
        SetPrivateField(canvas, "_hoverDebugVisible", true);
        SetPrivateField(canvas, "_hoverDebugRegularOnlyMode", true);

        var snapshot = InvokeBuildHoverDebugSnapshot(canvas);
        var text = ReadSnapshotText(snapshot);

        Assert.Contains("REG 7", text);
        Assert.Contains("Diff 42", text);
    }

    private static object InvokeTryHitRegular(PadCanvas canvas, Point2 world)
    {
        var method = typeof(PadCanvas).GetMethod("TryHitRegular", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var hit = method!.Invoke(canvas, new object[] { world });
        Assert.NotNull(hit);
        return hit!;
    }

    private static object InvokeHitTestForHoverDebug(PadCanvas canvas, Avalonia.Point screenPoint, bool regularOnly)
    {
        var method = typeof(PadCanvas).GetMethod("HitTestForHoverDebug", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var hit = method!.Invoke(canvas, new object[] { screenPoint, regularOnly });
        Assert.NotNull(hit);
        return hit!;
    }

    private static object InvokeBuildHoverDebugSnapshot(PadCanvas canvas)
    {
        var method = typeof(PadCanvas).GetMethod("BuildHoverDebugSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var snapshot = method!.Invoke(canvas, null);
        Assert.NotNull(snapshot);
        return snapshot!;
    }

    private static string ReadSnapshotText(object snapshot)
    {
        var property = snapshot.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<string>(property!.GetValue(snapshot));
    }

    private static void SetPrivateField(PadCanvas canvas, string fieldName, object value)
    {
        var field = typeof(PadCanvas).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(canvas, value);
    }

    private static void SetCanvasView(PadCanvas canvas, double zoom, double panX, double panY)
    {
        SetPrivateField(canvas, "_zoom", zoom);
        SetPrivateField(canvas, "_pan", new Vector(panX, panY));
        var invalidateMethod = typeof(PadCanvas).GetMethod("InvalidateViewFrameSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(invalidateMethod);
        invalidateMethod!.Invoke(canvas, null);
    }

    private static Avalonia.Point GetScreenPointForWorld(PadCanvas canvas, Point2 worldPoint)
    {
        var method = typeof(PadCanvas).GetMethod("WorldToScreen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Point2) }, null);
        Assert.NotNull(method);
        return Assert.IsType<Avalonia.Point>(method!.Invoke(canvas, new object[] { worldPoint }));
    }

    private static string ReadHitKind(object hit)
    {
        var type = hit.GetType();
        var prop = type.GetProperty("kind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   ?? type.GetProperty("Kind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(prop);
        return prop!.GetValue(hit)?.ToString() ?? string.Empty;
    }

    private static int ReadHitIdOrIndex(object hit)
    {
        var type = hit.GetType();
        var prop = type.GetProperty("idOrIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   ?? type.GetProperty("IdOrIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(prop);
        return Assert.IsType<int>(prop!.GetValue(hit));
    }

    private static List<int> ReadSelectedRegularIndices(PadCanvas canvas)
    {
        var field = typeof(PadCanvas).GetField("_selectedRegIdx", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = Assert.IsAssignableFrom<IEnumerable>(field!.GetValue(canvas));
        return value.Cast<object>().Select(Assert.IsType<int>).ToList();
    }

    private static List<int> ReadSelectedCadIds(PadCanvas canvas)
    {
        var field = typeof(PadCanvas).GetField("_selectedCadIds", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = Assert.IsAssignableFrom<IEnumerable>(field!.GetValue(canvas));
        return value.Cast<object>().Select(Assert.IsType<int>).ToList();
    }

    private static RegularPad CreateRectPad(int row, int col, int index, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(
            new[]
            {
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY),
            });
        return new RegularPad(row, col, index, polygon);
    }
}
