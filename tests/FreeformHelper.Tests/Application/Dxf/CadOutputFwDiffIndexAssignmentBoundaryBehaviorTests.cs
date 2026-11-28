// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadOutputFwDiffIndexAssignmentBoundaryBehaviorTests
{
    [Fact]
    public void Assign_EmptyPadSelection_IgnoresStaleSeedsAndOverrides()
    {
        var result = CadOutputFwDiffIndexAssignmentService.Assign(
            [],
            null,
            new GridSettings(),
            CadOutputFwDiffAutoMode.StrictUnique,
            new Dictionary<int, int?> { [42] = 8 },
            new Dictionary<int, int> { [42] = -1 },
            new Dictionary<int, int> { [0] = 42 });

        Assert.Empty(result.AssignedIndexByCadId);
        Assert.Empty(result.CadIcIndexByCadId);
        Assert.Empty(result.SequenceAdjustedCadIds);
        Assert.Empty(result.ConflictSamples);
        Assert.Empty(result.UnmatchedSamples);
        Assert.Equal((0, 0, 0, 0, 0, 0),
            (result.OverrideAssignedCount, result.AutoAssignedCount, result.AutoConflictCount,
             result.AutoUnmatchedCount, result.DuplicateOverrides, result.InvalidOverrides));
        Assert.Equal((0, 0, 0, 0, 0, 0),
            (result.OverrideMismatchCount, result.SequenceAdjustedCount, result.AnchorAppliedCount,
             result.AnchorIgnoredCount, result.RowSequenceAppliedCount, result.RowSequenceFallbackCount));
    }

    [Fact]
    public void Assign_WithGrid_InvalidOverrideFallsBackAndMissingSeedsRemainUnassigned()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 1, cols: 6);
        var settings = new GridSettings { XChannels = 6, YChannels = 1 };
        var pads = grid.Pads.Select(pad => TestGeometryFactory.CreateCadPad(
            pad.RegularPadId + 1, "Copper", pad.Bounds.MinX, pad.Bounds.MinY, pad.Bounds.MaxX, pad.Bounds.MaxY)).ToList();
        var seeds = new Dictionary<int, int?> { [1] = 8, [2] = null, [4] = null, [5] = 9 };
        var overrides = new Dictionary<int, int> { [1] = -1, [2] = 10, [3] = 11, [5] = 9 };

        var result = CadOutputFwDiffIndexAssignmentService.Assign(
            pads, grid, settings, CadOutputFwDiffAutoMode.StrictUnique, seeds, overrides);

        Assert.Equal(4, result.AssignedIndexByCadId.Count);
        Assert.Equal(8, result.AssignedIndexByCadId[1]);
        Assert.Equal(10, result.AssignedIndexByCadId[2]);
        Assert.Equal(11, result.AssignedIndexByCadId[3]);
        Assert.Equal(9, result.AssignedIndexByCadId[5]);
        Assert.False(result.AssignedIndexByCadId.ContainsKey(4));
        Assert.False(result.AssignedIndexByCadId.ContainsKey(6));
        Assert.Equal(3, result.OverrideAssignedCount);
        Assert.Equal(1, result.AutoAssignedCount);
        Assert.Equal(2, result.AutoUnmatchedCount);
        Assert.Equal(1, result.InvalidOverrides);
        Assert.Equal(0, result.DuplicateOverrides);
        Assert.Equal(0, result.OverrideMismatchCount);
        Assert.Equal(0, result.AutoConflictCount);
        Assert.Empty(result.ConflictSamples);
        Assert.Collection(result.UnmatchedSamples,
            sample => Assert.Equal("IC1/R0: CAD 4", sample),
            sample => Assert.Equal("IC1/R0: CAD 6", sample));
        Assert.Equal(6, result.CadIcIndexByCadId.Count);
        Assert.All(pads, pad => Assert.Equal(0, result.CadIcIndexByCadId[pad.Id]));
    }
}
