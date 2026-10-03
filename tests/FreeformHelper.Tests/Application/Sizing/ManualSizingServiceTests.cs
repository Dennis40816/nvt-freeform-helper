using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualSizingServiceTests
{
    private readonly ManualSizingService _service = new();

    [Fact]
    public void TryParseRowRange_ConvertsDisplayToActualRows()
    {
        var ok = _service.TryParseRowRange("0-1,3", totalRows: 5, out var rows, out var error);

        Assert.True(ok);
        Assert.Empty(error);
        Assert.Equal(new List<int> { 1, 3, 4 }, rows);
    }

    [Fact]
    public void ToDisplayRow_ConvertsWithTopOrigin()
    {
        var display = ManualSizingService.ToDisplayRow(actualRow: 0, totalRows: 5);
        var actual = ManualSizingService.ToActualRow(display, totalRows: 5);

        Assert.Equal(4, display);
        Assert.Equal(0, actual);
    }

    [Fact]
    public void TryParseIndexRange_ReordersReverseRangeAndDeduplicates()
    {
        var ok = ManualSizingService.TryParseIndexRange("5-3,4,3", min: 0, max: 10, out var indices, out var error);

        Assert.True(ok);
        Assert.Empty(error);
        Assert.Equal(new List<int> { 3, 4, 5 }, indices);
    }

    [Fact]
    public void TryParseIndexRange_ReturnsError_WhenOutOfBounds()
    {
        var ok = ManualSizingService.TryParseIndexRange("1,9", min: 0, max: 4, out var indices, out var error);

        Assert.False(ok);
        Assert.Contains(1, indices);
        Assert.Contains("out of bounds", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseIndexRange_ReturnsError_WhenFormatInvalid()
    {
        var ok = ManualSizingService.TryParseIndexRange("2-a", min: 0, max: 10, out var indices, out var error);

        Assert.False(ok);
        Assert.Empty(indices);
        Assert.Contains("Invalid numbers in range", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseRowRange_ReturnsGridNotBuilt_WhenTotalRowsIsZero()
    {
        var ok = _service.TryParseRowRange("0-1", totalRows: 0, out var rows, out var error);

        Assert.False(ok);
        Assert.Empty(rows);
        Assert.Equal("Grid not built.", error);
    }

    [Fact]
    public void TryParseColRange_ReturnsFalseWithoutError_WhenInputEmpty()
    {
        var ok = _service.TryParseColRange("   ", totalCols: 4, out var cols, out var error);

        Assert.False(ok);
        Assert.Empty(cols);
        Assert.Empty(error);
    }

    [Fact]
    public void ApplySizing_SetsOverridesAndDistributes()
    {
        var grid = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 50,
            ColumnWidths = new List<double> { 40, 60 },
            ColumnOverrides = new List<bool> { false, false },
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
        };

        var result = _service.ApplySizing(
            grid,
            GridAlignmentMode.FromPanelAa,
            cad: null,
            rows: new List<int> { 0, 1 },
            cols: new List<int> { 0 },
            width: 10,
            height: null);

        Assert.True(result.Success);
        Assert.Equal(new List<double> { 10, 90 }, grid.ColumnWidths, new DoubleListComparer(1e-6));
        Assert.Equal(new List<bool> { true, false }, grid.ColumnOverrides);
    }

    [Fact]
    public void ResetSizing_ClearsOverridesForSelected()
    {
        var grid = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 10,
            ColumnWidths = new List<double> { 40, 60 },
            ColumnOverrides = new List<bool> { true, true },
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
        };

        var result = _service.ResetSizing(
            grid,
            GridAlignmentMode.FromPanelAa,
            cad: null,
            rows: new List<int>(),
            cols: new List<int> { 0 });

        Assert.True(result.Success);
        Assert.Equal(new List<double> { 40, 60 }, grid.ColumnWidths);
        Assert.Equal(new List<bool> { false, true }, grid.ColumnOverrides);
    }

    [Fact]
    public void SetPadDimensions_FailsWhenNoAutoCells()
    {
        var grid = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 80,
            ActiveAreaHeight = 10,
            ColumnWidths = new List<double> { 20, 30 },
            ColumnOverrides = new List<bool> { true, true },
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
        };

        var result = _service.SetPadDimensions(
            grid,
            GridAlignmentMode.FromPanelAa,
            cad: null,
            row: 0,
            col: 0,
            width: 70,
            height: null);

        Assert.False(result.Success);
        Assert.Equal("No auto cells available to keep total size constant.", result.Error);
        Assert.Equal(new List<double> { 20, 30 }, grid.ColumnWidths);
        Assert.Equal(new List<bool> { true, true }, grid.ColumnOverrides);
    }

    [Fact]
    public void SetPadDimensions_ScalesRemainingProportionally()
    {
        var grid = new GridSettings
        {
            XChannels = 3,
            YChannels = 1,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 10,
            ColumnWidths = new List<double> { 20, 40, 40 },
            ColumnOverrides = new List<bool> { false, false, false },
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
        };

        var result = _service.SetPadDimensions(
            grid,
            GridAlignmentMode.FromPanelAa,
            cad: null,
            row: 0,
            col: 0,
            width: 50,
            height: null);

        Assert.True(result.Success);
        Assert.Equal(new List<double> { 50, 25, 25 }, grid.ColumnWidths, new DoubleListComparer(1e-6));
        Assert.Equal(new List<bool> { true, false, false }, grid.ColumnOverrides);
    }

    [Fact]
    public void ApplySizing_RowLocal_OnlyTouchesSelectedRow()
    {
        var grid = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 50,
            WidthScope = WidthAdjustmentScope.RowLocal,
            ColumnWidths = new List<double> { 50, 50 },
        };

        var result = _service.ApplySizing(
            grid,
            GridAlignmentMode.FromPanelAa,
            cad: null,
            rows: new List<int> { 1 },
            cols: new List<int> { 0 },
            width: 70,
            height: null);

        Assert.True(result.Success);
        Assert.Equal(70, grid.RowWidthOverrides[1][0], 6);
        Assert.Equal(30, grid.RowWidthOverrides[1][1], 6);
    }

    [Fact]
    public void ApplySizing_ColumnLocal_OnlyTouchesSelectedColumn()
    {
        var grid = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 100,
            HeightScope = HeightAdjustmentScope.ColumnLocal,
            RowHeights = new List<double> { 50, 50 },
        };

        var result = _service.ApplySizing(
            grid,
            GridAlignmentMode.FromPanelAa,
            cad: null,
            rows: new List<int> { 1 },
            cols: new List<int> { 0 },
            width: null,
            height: 70);

        Assert.True(result.Success);
        Assert.Equal(70, grid.ColumnHeightOverrides[0][1], 6);
        Assert.Equal(30, grid.ColumnHeightOverrides[0][0], 6);
    }

    [Fact]
    public void ReplaceSizingCollections_ClonesInputCollections()
    {
        var columnWidths = new List<double> { 10, 20 };
        var rowHeights = new List<double> { 5, 5 };
        var columnOverrides = new List<bool> { true, false };
        var rowOverrides = new List<bool> { false, true };
        var rowWidthOverrides = new List<List<double>> { new() { 10, 20 } };
        var rowWidthOverrideFlags = new List<List<bool>> { new() { true, false } };
        var columnHeightOverrides = new List<List<double>> { new() { 6, 4 } };
        var columnHeightOverrideFlags = new List<List<bool>> { new() { false, true } };

        var grid = new GridSettings();
        grid.ReplaceSizingCollections(
            columnWidths,
            rowHeights,
            columnOverrides,
            rowOverrides,
            rowWidthOverrides,
            rowWidthOverrideFlags,
            columnHeightOverrides,
            columnHeightOverrideFlags);

        columnWidths.Add(99);
        rowWidthOverrides[0][0] = 777;
        columnHeightOverrideFlags[0][0] = true;

        Assert.Equal(2, grid.ColumnWidths.Count);
        Assert.Equal(10, grid.RowWidthOverrides[0][0], 6);
        Assert.False(grid.ColumnHeightOverrideFlags[0][0]);
    }

    private sealed class DoubleListComparer : IEqualityComparer<List<double>>
    {
        private readonly double _tolerance;

        public DoubleListComparer(double tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(List<double>? x, List<double>? y)
        {
            if (x is null || y is null) return x == y;
            if (x.Count != y.Count) return false;
            for (var i = 0; i < x.Count; i++)
            {
                if (Math.Abs(x[i] - y[i]) > _tolerance) return false;
            }
            return true;
        }

        public int GetHashCode(List<double> obj) => obj.Count.GetHashCode();
    }
}
