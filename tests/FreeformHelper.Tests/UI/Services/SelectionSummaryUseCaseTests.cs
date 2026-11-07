// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SelectionSummaryUseCaseTests
{
    [Fact]
    public void Compute_NoGridAndNoSelection_ReturnsEmptySummary()
    {
        var summary = SelectionSummaryUseCase.Compute(null, [], []);

        Assert.Equal(0, summary.CadCount);
        Assert.Equal(0, summary.RegularCount);
        Assert.Equal("No selection.", summary.SelectionSummaryText);
        Assert.Equal("No selection.", summary.SelectedGridRangeSummary);
        Assert.Equal("CAD: 0 pads.", summary.CadSelectionText);
        Assert.Equal("Regular: 0 pads.", summary.RegularSelectionText);
        Assert.Equal(string.Empty, summary.SelectionDetailText);
        Assert.Null(summary.RegularRange);
        Assert.False(summary.IsRectangular);
        Assert.Equal(string.Empty, summary.ManualRangeError);
        Assert.Equal(0m, summary.PendingWidth);
        Assert.Equal(0m, summary.PendingHeight);
    }

    [Fact]
    public void Compute_CadOnlySelection_DoesNotReadTheGrid()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2);

        var summary = SelectionSummaryUseCase.Compute(grid, [1, 2], []);

        Assert.Equal(2, summary.CadCount);
        Assert.Equal("Selected: CAD=2.", summary.SelectionSummaryText);
        Assert.Equal("Selected: 2 pads.", summary.SelectedGridRangeSummary);
        Assert.Equal("CAD: 2 pads.", summary.CadSelectionText);
        Assert.Equal("Regular: 0 pads.", summary.RegularSelectionText);
        Assert.Equal(string.Empty, summary.SelectionDetailText);
        Assert.Null(summary.RegularRange);
        Assert.False(summary.IsRectangular);
    }

    [Fact]
    public void Compute_RectangularRegularSelection_ReportsRangesInDisplayRows()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(3, 4, cellWidth: 10, cellHeight: 5);

        // Rows 1 and 2, columns 1 and 2. Display rows count from the top, so they read as 1 and 0.
        var summary = SelectionSummaryUseCase.Compute(grid, [7], [5, 6, 9, 10]);

        Assert.Equal(1, summary.CadCount);
        Assert.Equal(4, summary.RegularCount);
        Assert.Equal("Selected: CAD=1, Regular=4.", summary.SelectionSummaryText);
        Assert.Equal("Selected: 5 pads.", summary.SelectedGridRangeSummary);
        Assert.Equal("CAD: 1 pads.", summary.CadSelectionText);
        Assert.Equal("Regular: Rows 0-1, Cols 1-2 (4)", summary.RegularSelectionText);
        Assert.Equal("CAD: 1 pads · Regular: Rows 0-1, Cols 1-2 (4)", summary.SelectionDetailText);
        Assert.Equal((1, 2, 1, 2), summary.RegularRange);
        Assert.True(summary.IsRectangular);
        Assert.Equal("0-1", summary.ManualRowsRange);
        Assert.Equal("1-2", summary.ManualColsRange);
        Assert.Equal(string.Empty, summary.ManualRangeError);
        Assert.Equal(10m, summary.PendingWidth);
        Assert.Equal(5m, summary.PendingHeight);
        Assert.False(summary.WidthMixed);
        Assert.False(summary.HeightMixed);
    }

    [Fact]
    public void Compute_NonRectangularRegularSelection_ReportsErrorAndKeepsRange()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(3, 4);

        // Row 0 column 0 and row 1 column 1 do not fill the 2 by 2 box around them.
        var summary = SelectionSummaryUseCase.Compute(grid, [], [0, 5]);

        Assert.Equal("Selected: Regular=2.", summary.SelectionSummaryText);
        Assert.Equal("Regular: 2 pads (non-rectangular)", summary.RegularSelectionText);
        Assert.Equal("Regular: Rows 1-2, Cols 0-1 (2)", summary.SelectionDetailText);
        Assert.Equal((0, 1, 0, 1), summary.RegularRange);
        Assert.False(summary.IsRectangular);
        Assert.Equal(string.Empty, summary.ManualRowsRange);
        Assert.Equal(string.Empty, summary.ManualColsRange);
        Assert.Equal("Selection is not a full rectangle; rows/cols range not applied.", summary.ManualRangeError);
    }

    [Fact]
    public void Compute_PadsOfDifferentWidth_MarksWidthMixedAndKeepsHeight()
    {
        var grid = new RegularGrid(
            1,
            2,
            [0d, 10d, 30d],
            [0d, 5d],
            [
                TestGeometryFactory.CreateRegularPad(0, 0, 0, 0, 0, 10, 5),
                TestGeometryFactory.CreateRegularPad(0, 1, 1, 10, 0, 30, 5),
            ]);

        var summary = SelectionSummaryUseCase.Compute(grid, [], [0, 1]);

        Assert.True(summary.IsRectangular);
        Assert.True(summary.WidthMixed);
        Assert.Equal(0m, summary.PendingWidth);
        Assert.False(summary.HeightMixed);
        Assert.Equal(5m, summary.PendingHeight);
    }

    [Fact]
    public void Compute_IndexMissingFromGrid_KeepsCountButReportsNoRange()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2);

        var summary = SelectionSummaryUseCase.Compute(grid, [], [99]);

        Assert.Equal(1, summary.RegularCount);
        Assert.Equal("Selected: Regular=1.", summary.SelectionSummaryText);
        Assert.Equal("Regular: 1 pads.", summary.RegularSelectionText);
        Assert.Equal(string.Empty, summary.SelectionDetailText);
        Assert.Null(summary.RegularRange);
        Assert.False(summary.IsRectangular);
    }

    [Fact]
    public void Compute_PadIndexDiffersFromListPosition_ResolvesPadByIndex()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, regularPadIdSelector: (_, col) => 10 + col * 10);

        var summary = SelectionSummaryUseCase.Compute(grid, [], [10, 20]);

        Assert.True(summary.IsRectangular);
        Assert.Equal("Regular: Rows 0, Cols 0-1 (2)", summary.RegularSelectionText);
        Assert.Equal((0, 0, 0, 1), summary.RegularRange);
    }
}
