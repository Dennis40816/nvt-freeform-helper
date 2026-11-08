// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularBoundaryClassifierBehaviorTests
{
    [Fact]
    public void BuildOccupiedGridCells_AbsentOrStaleActiveMappings_KeepCurrentGridOccupied()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(3, 3, regularPadIdSelector: (row, col) => 100 + row * 3 + col);
        IReadOnlySet<int>?[] activeMappings = [null, new HashSet<int>(), new HashSet<int> { 999 }];

        foreach (var activeIds in activeMappings)
        {
            var occupied = RegularBoundaryClassifier.BuildOccupiedGridCells(grid, activeIds);

            Assert.Equal(9, occupied.Count);
            Assert.All(grid.Pads, pad => Assert.Contains((pad.Row, pad.Col), occupied));
            Assert.False(RegularBoundaryClassifier.IsBoundaryPad(grid.GetPad(1, 1), grid.Rows, grid.Cols, occupied));
        }

        var emptyGrid = TestGeometryFactory.CreateRegularGrid(0, 0);
        Assert.Empty(RegularBoundaryClassifier.BuildOccupiedGridCells(emptyGrid, new HashSet<int> { 999 }));
    }

    [Fact]
    public void IsBoundaryPad_ActiveMask_RequiresCenterAndEveryCardinalNeighbor()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(3, 3, regularPadIdSelector: (row, col) => 100 + row * 3 + col);
        var center = grid.GetPad(1, 1);
        var occupied = RegularBoundaryClassifier.BuildOccupiedGridCells(grid);

        Assert.False(RegularBoundaryClassifier.IsBoundaryPad(center, 3, 3, occupied));
        Assert.All(grid.Pads.Where(pad => pad.RegularPadId != center.RegularPadId),
            pad => Assert.True(RegularBoundaryClassifier.IsBoundaryPad(pad, 3, 3, occupied)));

        (int Row, int Col)[] missingCells = [(0, 1), (2, 1), (1, 0), (1, 2), (1, 1)];
        foreach (var (row, col) in missingCells)
        {
            var missingId = grid.GetPad(row, col).RegularPadId;
            var activeIds = grid.Pads.Where(pad => pad.RegularPadId != missingId)
                .Select(pad => pad.RegularPadId).ToHashSet();
            var maskedCells = RegularBoundaryClassifier.BuildOccupiedGridCells(grid, activeIds);

            Assert.Equal(8, maskedCells.Count);
            Assert.DoesNotContain((row, col), maskedCells);
            Assert.True(RegularBoundaryClassifier.IsBoundaryPad(center, 3, 3, maskedCells));
        }

        var withoutDiagonal = grid.Pads.Where(pad => pad.RegularPadId != grid.GetPad(0, 0).RegularPadId)
            .Select(pad => pad.RegularPadId).ToHashSet();
        Assert.False(RegularBoundaryClassifier.IsBoundaryPad(center, 3, 3,
            RegularBoundaryClassifier.BuildOccupiedGridCells(grid, withoutDiagonal)));
        Assert.True(RegularBoundaryClassifier.IsBoundaryPad(center, 3, 3, new HashSet<(int Row, int Col)>()));
    }
}
