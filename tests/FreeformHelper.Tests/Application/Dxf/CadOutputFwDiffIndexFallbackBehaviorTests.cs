// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadOutputFwDiffIndexFallbackBehaviorTests
{
    [Fact]
    public void Assign_WithoutGrid_InvalidOverrideFallsBackWhileUnseededOverridesRemainAssigned()
    {
        using var culture = TestCulture.UseEnUs();
        var pads = Enumerable.Range(1, 4)
            .Select(id => TestGeometryFactory.CreateCadPad(id, "Copper", id * 2, 0, id * 2 + 1, 1))
            .ToList();
        var seeds = new Dictionary<int, int?> { [1] = 8, [2] = null, [4] = 9 };
        var overrides = new Dictionary<int, int> { [1] = -1, [2] = 10, [3] = 11, [4] = 9 };

        var result = CadOutputFwDiffIndexAssignmentService.Assign(
            pads, null, new GridSettings(), CadOutputFwDiffAutoMode.StrictUnique, seeds, overrides);

        Assert.Equal(4, result.AssignedIndexByCadId.Count);
        Assert.Equal(8, result.AssignedIndexByCadId[1]);
        Assert.Equal(10, result.AssignedIndexByCadId[2]);
        Assert.Equal(11, result.AssignedIndexByCadId[3]);
        Assert.Equal(9, result.AssignedIndexByCadId[4]);
        Assert.Equal(1, result.InvalidOverrides);
        Assert.Equal(3, result.OverrideAssignedCount);
        Assert.Equal(1, result.AutoAssignedCount);
        Assert.Equal(0, result.OverrideMismatchCount);
        Assert.Equal(0, result.AutoConflictCount);
        Assert.Equal(0, result.AutoUnmatchedCount);
        Assert.Equal(4, result.CadIcIndexByCadId.Count);
        Assert.All(result.CadIcIndexByCadId.Values, icIndex => Assert.Equal(0, icIndex));
    }

    [Theory]
    [InlineData(CadOutputFwDiffAutoMode.StrictUnique, 1, 6)]
    [InlineData(CadOutputFwDiffAutoMode.BestMatchDirect, 7, 0)]
    public void Assign_WithoutGrid_CountsAllUnresolvedPadsAndLimitsDiagnosticSamples(
        CadOutputFwDiffAutoMode autoMode, int expectedAssignedCount, int expectedConflictCount)
    {
        using var culture = TestCulture.UseEnUs();
        var pads = Enumerable.Range(1, 14)
            .Select(id => TestGeometryFactory.CreateCadPad(id, "Copper", id * 2, 0, id * 2 + 1, 1))
            .ToList();
        var seeds = Enumerable.Range(1, 7).ToDictionary(id => id, _ => (int?)4);
        seeds[8] = null;

        var result = CadOutputFwDiffIndexAssignmentService.Assign(
            pads, null, new GridSettings(), autoMode, seeds);

        Assert.Equal(expectedAssignedCount, result.AutoAssignedCount);
        Assert.Equal(expectedConflictCount, result.AutoConflictCount);
        Assert.Equal(7, result.AutoUnmatchedCount);
        Assert.Equal(Enumerable.Range(1, expectedAssignedCount), result.AssignedIndexByCadId.Keys.Order());
        Assert.All(result.AssignedIndexByCadId.Values, diffIndex => Assert.Equal(4, diffIndex));
        Assert.Equal(
            Enumerable.Range(2, Math.Min(5, expectedConflictCount)).Select(id => $"IC1/R?: CAD {id} -> diff 4"),
            result.ConflictSamples);
        Assert.Equal(
            Enumerable.Range(8, 5).Select(id => $"IC1/R?: CAD {id}"),
            result.UnmatchedSamples);
    }
}
