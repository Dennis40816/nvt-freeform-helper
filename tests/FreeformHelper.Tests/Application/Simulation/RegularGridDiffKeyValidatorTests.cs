using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularGridDiffKeyValidatorTests
{
    private static readonly int[] DuplicateRegularPadIds = [100, 101];
    private static readonly int[] ActiveRegularPadIds = [100, 102];

    [Fact]
    public void FindDuplicateDiffGroups_WhenGridHasSharedIcDiff_ReturnsDuplicateGroup()
    {
        var grid = BuildGrid();

        var groups = RegularGridDiffKeyValidator.FindDuplicateDiffGroups(grid);

        var group = Assert.Single(groups);
        Assert.Equal(0, group.IcIndex);
        Assert.Equal(1541, group.DiffIndex);
        Assert.Equal(DuplicateRegularPadIds, group.RegularPadIds);
    }

    [Fact]
    public void FindDuplicateDiffGroups_IgnoresInactiveDuplicatesWhenActiveSurfaceProvided()
    {
        var grid = BuildGrid();

        var groups = RegularGridDiffKeyValidator.FindDuplicateDiffGroups(
            grid,
            activeRegularPadIds: new HashSet<int>(ActiveRegularPadIds));

        Assert.Empty(groups);
    }

    private static RegularGrid BuildGrid()
    {
        return TestGeometryFactory.CreateRegularGrid(
            rows: 1,
            cols: 3,
            diffIndexSelector: static (_, col) => col < 2 ? 1541 : 1542,
            regularPadIdSelector: static (_, col) => 100 + col);
    }
}
