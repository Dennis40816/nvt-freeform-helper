using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DiffIndexMapperTransitionTests
{
    private static readonly int[] UnequalIcIndices = { 0, 0, 1, 1, 1, 0, 0, 1, 1, 1 };

    [Theory]
    [InlineData(ScanOrder.LeftToRight_TopToBottom, new[] { 2, 3, 3, 4, 5, 0, 1, 0, 1, 2 })]
    [InlineData(ScanOrder.RightToLeft_TopToBottom, new[] { 3, 2, 5, 4, 3, 1, 0, 2, 1, 0 })]
    [InlineData(ScanOrder.LeftToRight_BottomToTop, new[] { 0, 1, 0, 1, 2, 2, 3, 3, 4, 5 })]
    [InlineData(ScanOrder.RightToLeft_BottomToTop, new[] { 1, 0, 2, 1, 0, 3, 2, 5, 4, 3 })]
    public void ApplyMapping_UnequalPerIcColumns_MapsEveryPadWithoutChangingOtherState(
        ScanOrder scanOrder,
        int[] expectedDiffIndices)
    {
        var grid = CreateSeededGrid();
        var otherState = grid.Pads.Select(pad => (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform)).ToArray();
        var settings = CreateSettings(scanOrder);

        DiffIndexMapper.ApplyMapping(grid, settings);

        AssertMapping(grid, UnequalIcIndices, expectedDiffIndices);
        Assert.Equal(otherState, grid.Pads.Select(pad => (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform)));
    }

    [Fact]
    public void ApplyMapping_Again_ReplacesCompleteMappingWithoutChangingOtherState()
    {
        var grid = CreateSeededGrid();
        var otherState = grid.Pads.Select(pad => (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform)).ToArray();
        var settings = CreateSettings(ScanOrder.LeftToRight_BottomToTop);
        DiffIndexMapper.ApplyMapping(grid, settings);
        var initialDiffIndices = new[] { 0, 1, 0, 1, 2, 2, 3, 3, 4, 5 };
        AssertMapping(grid, UnequalIcIndices, initialDiffIndices);

        settings.PerIcXChannels = new List<int> { 3, 2 };
        settings.ScanOrder = ScanOrder.RightToLeft_TopToBottom;
        DiffIndexMapper.ApplyMapping(grid, settings);

        var expectedIcIndices = new[] { 0, 0, 0, 1, 1, 0, 0, 0, 1, 1 };
        var expectedDiffIndices = new[] { 5, 4, 3, 3, 2, 2, 1, 0, 1, 0 };
        AssertMapping(grid, expectedIcIndices, expectedDiffIndices);
        Assert.Equal(otherState, grid.Pads.Select(pad => (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform)));
    }

    [Fact]
    public void ApplyMapping_InvalidSettings_ThrowsBeforeChangingAnyPad()
    {
        var grid = CreateSeededGrid();
        var state = grid.Pads.Select(pad =>
            (pad.IcIndex, pad.DiffIndex, pad.MatchedCadPadId, pad.MatchScore, pad.Freeform)).ToArray();
        var settings = CreateSettings(ScanOrder.LeftToRight_TopToBottom);
        settings.CascadeNum = 6;

        var exception = Assert.Throws<InvalidOperationException>(() => DiffIndexMapper.ApplyMapping(grid, settings));

        Assert.Equal("CascadeNum cannot exceed XChannels.", exception.Message);
        Assert.Equal(state, grid.Pads.Select(pad =>
            (pad.IcIndex, pad.DiffIndex, pad.MatchedCadPadId, pad.MatchScore, pad.Freeform)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 11)]
    [InlineData(-1, -2)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void AssignMapping_ReplacesPairWithoutRejectingValuesOrChangingOtherState(int icIndex, int diffIndex)
    {
        var pad = CreateSeededGrid().Pads[3];
        var otherState = (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform);

        pad.AssignMapping(icIndex, diffIndex);

        Assert.Equal((icIndex, diffIndex), (pad.IcIndex, pad.DiffIndex));
        Assert.Equal(diffIndex, pad.RegularFwDiffIndex);
        Assert.Equal(otherState, (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform));
    }

    [Fact]
    public void AssignMapping_LegacySettersAndAliasRemainWritable()
    {
        var pad = CreateSeededGrid().Pads[0];
        pad.AssignMapping(2, 7);

        pad.IcIndex = -4;
        pad.DiffIndex = -5;

        Assert.Equal((-4, -5), (pad.IcIndex, pad.RegularFwDiffIndex));

        pad.RegularFwDiffIndex = 13;

        Assert.Equal((-4, 13), (pad.IcIndex, pad.DiffIndex));
    }

    private static GridSettings CreateSettings(ScanOrder scanOrder)
    {
        return new GridSettings
        {
            XChannels = 5,
            YChannels = 2,
            CascadeNum = 2,
            PerIcXChannels = new List<int> { 2, 3 },
            ScanOrder = scanOrder,
        };
    }

    private static RegularGrid CreateSeededGrid()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(
            rows: 2,
            cols: 5,
            diffIndexSelector: (_, _) => -20,
            icIndexSelector: (_, _) => -10);
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = pad.Index % 2 == 0 ? null : 100 + pad.Index;
            pad.MatchScore = 0.25 + pad.Index;
            pad.Freeform = (FreeformType)(pad.Index % 4);
        }

        return grid;
    }

    private static void AssertMapping(RegularGrid grid, int[] expectedIcIndices, int[] expectedDiffIndices)
    {
        Assert.Equal(expectedIcIndices, grid.Pads.Select(pad => pad.IcIndex));
        Assert.Equal(expectedDiffIndices, grid.Pads.Select(pad => pad.DiffIndex));
        Assert.Equal(expectedDiffIndices, grid.Pads.Select(pad => pad.RegularFwDiffIndex));
    }
}
