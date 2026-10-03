using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed record FreeformStatistics(
    int TotalCount,
    int NoneCount,
    int XWayCount,
    int YWayCount,
    int XyWayCount)
{
    public int TaggedCount => XWayCount + YWayCount + XyWayCount;
}

public sealed class FreeformStatisticsBuilder
{
    public static FreeformStatistics Build(IReadOnlyCollection<RegularPad> pads)
    {
        if (pads is null || pads.Count == 0)
        {
            return new FreeformStatistics(0, 0, 0, 0, 0);
        }

        var noneCount = 0;
        var xWayCount = 0;
        var yWayCount = 0;
        var xyWayCount = 0;

        foreach (var pad in pads)
        {
            switch (pad.Freeform)
            {
                case FreeformType.XWay:
                    xWayCount++;
                    break;
                case FreeformType.YWay:
                    yWayCount++;
                    break;
                case FreeformType.XYWay:
                    xyWayCount++;
                    break;
                default:
                    noneCount++;
                    break;
            }
        }

        return new FreeformStatistics(
            pads.Count,
            noneCount,
            xWayCount,
            yWayCount,
            xyWayCount);
    }
}
