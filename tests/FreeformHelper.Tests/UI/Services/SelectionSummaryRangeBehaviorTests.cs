// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SelectionSummaryRangeBehaviorTests
{
    [Fact]
    public void Compute_DiscontinuousRows_FormatsSeparateRunsWithoutIncludingUnselectedRows()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(6, 1, cellWidth: 10, cellHeight: 5);

        var summary = SelectionSummaryUseCase.Compute(grid, [], [0, 2, 3, 5]);

        Assert.True(summary.IsRectangular);
        Assert.Equal("0, 2-3, 5", summary.ManualRowsRange);
        Assert.Equal("0", summary.ManualColsRange);
        Assert.Equal("Regular: Rows 0, 2-3, 5, Cols 0 (4)", summary.RegularSelectionText);
        Assert.Equal("Regular: Rows 0, 2-3, 5, Cols 0 (4)", summary.SelectionDetailText);
        Assert.Equal((0, 5, 0, 0), summary.RegularRange);
        Assert.Empty(summary.ManualRangeError);
    }

    [Fact]
    public void Compute_SelectedIdWithinListBoundsButAtAnotherPosition_UsesPadIdentity()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(
            2, 2, cellWidth: 10, cellHeight: 5,
            regularPadIdSelector: static (row, col) => (row * 2 + col + 1) % 4);

        var summary = SelectionSummaryUseCase.Compute(grid, [], [0]);

        Assert.Equal(1, summary.RegularCount);
        Assert.True(summary.IsRectangular);
        Assert.Equal((1, 1, 1, 1), summary.RegularRange);
        Assert.Equal("0", summary.ManualRowsRange);
        Assert.Equal("1", summary.ManualColsRange);
        Assert.Equal("Regular: Rows 0, Cols 1 (1)", summary.RegularSelectionText);
        Assert.Equal(10m, summary.PendingWidth);
        Assert.Equal(5m, summary.PendingHeight);
    }
}
