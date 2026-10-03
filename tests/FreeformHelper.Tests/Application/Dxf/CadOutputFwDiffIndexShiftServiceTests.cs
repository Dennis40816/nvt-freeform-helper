using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadOutputFwDiffIndexShiftServiceTests
{
    private static readonly int[] SelectedCad23 = [2, 3];
    private static readonly int[] SelectedCad2 = [2];
    private static readonly int[] SelectedCad1 = [1];

    [Fact]
    public void BuildShiftPlan_ShiftsSelectedCadIdsBySharedOffset()
    {
        var result = CadOutputFwDiffIndexShiftService.BuildShiftPlan(
            selectedCadIds: SelectedCad23,
            currentCadOutputFwDiffByCadId: new Dictionary<int, int>
            {
                [1] = 10,
                [2] = 11,
                [3] = 12,
            },
            cadIcIndexByCadId: new Dictionary<int, int>
            {
                [1] = 0,
                [2] = 0,
                [3] = 0,
            },
            delta: 1);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.ShiftedDiffByCadId.Count);
        Assert.Equal(12, result.ShiftedDiffByCadId[2]);
        Assert.Equal(13, result.ShiftedDiffByCadId[3]);
        Assert.Equal("Shifted 2 CAD pad(s) by 1. Range 11-12 -> 12-13.", result.Message);
    }

    [Fact]
    public void BuildShiftPlan_BlocksWhenShiftCreatesDuplicateDiffInsideSameIc()
    {
        var result = CadOutputFwDiffIndexShiftService.BuildShiftPlan(
            selectedCadIds: SelectedCad2,
            currentCadOutputFwDiffByCadId: new Dictionary<int, int>
            {
                [1] = 10,
                [2] = 11,
            },
            cadIcIndexByCadId: new Dictionary<int, int>
            {
                [1] = 0,
                [2] = 0,
            },
            delta: -1);

        Assert.False(result.IsSuccessful);
        Assert.Contains("duplicate diff after shift", result.Message, StringComparison.Ordinal);
        Assert.Empty(result.ShiftedDiffByCadId);
    }

    [Fact]
    public void BuildShiftPlan_BlocksWhenShiftMovesBelowZero()
    {
        var result = CadOutputFwDiffIndexShiftService.BuildShiftPlan(
            selectedCadIds: SelectedCad1,
            currentCadOutputFwDiffByCadId: new Dictionary<int, int>
            {
                [1] = 0,
            },
            cadIcIndexByCadId: new Dictionary<int, int>
            {
                [1] = 0,
            },
            delta: -1);

        Assert.False(result.IsSuccessful);
        Assert.Contains("outside 0..1000000", result.Message, StringComparison.Ordinal);
    }
}
