using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class FreeformStatisticsBuilderTests
{
    [Fact]
    public void Build_ReturnsZeroStats_WhenPadsAreEmpty()
    {
        _ = new FreeformStatisticsBuilder();

        var stats = FreeformStatisticsBuilder.Build(Array.Empty<RegularPad>());

        Assert.Equal(0, stats.TotalCount);
        Assert.Equal(0, stats.NoneCount);
        Assert.Equal(0, stats.XWayCount);
        Assert.Equal(0, stats.YWayCount);
        Assert.Equal(0, stats.XyWayCount);
        Assert.Equal(0, stats.TaggedCount);
    }

    [Fact]
    public void Build_CountsEachFreeformType()
    {
        var grid = new RegularGridBuilder().BuildFromSettings(new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            BoundsPaddingRatio = 0,
        });

        grid.Pads[0].Freeform = FreeformType.None;
        grid.Pads[1].Freeform = FreeformType.XWay;
        grid.Pads[2].Freeform = FreeformType.YWay;
        grid.Pads[3].Freeform = FreeformType.XYWay;
        _ = new FreeformStatisticsBuilder();
        var stats = FreeformStatisticsBuilder.Build(grid.Pads);

        Assert.Equal(4, stats.TotalCount);
        Assert.Equal(1, stats.NoneCount);
        Assert.Equal(1, stats.XWayCount);
        Assert.Equal(1, stats.YWayCount);
        Assert.Equal(1, stats.XyWayCount);
        Assert.Equal(3, stats.TaggedCount);
    }
}
