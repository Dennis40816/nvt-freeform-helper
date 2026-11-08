// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularGridCandidateQueryEmptyGridBehaviorTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    public void GridWithoutCellsHasNoCandidateRangeOrMatchingPads(int rows, int cols)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows, cols);
        var bounds = new Rect2(1, 1, 3, 3);

        var range = RegularGridCandidateQuery.GetCandidateRange(grid, bounds, padding: 1);
        var candidates = RegularGridCandidateQuery.QueryByBounds(grid, bounds, padding: 1).ToArray();

        Assert.Equal((0, -1, 0, -1), range);
        Assert.Empty(candidates);
    }
}
