using FreeformHelper.Application.Settings;

namespace FreeformHelper.Application.Services;

public static class GridIcChannelAllocationService
{
    public static IReadOnlyList<int> ResolvePerIcColumns(GridSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ResolvePerIcColumns(settings, settings.XChannels);
    }

    public static IReadOnlyList<int> ResolvePerIcColumns(GridSettings settings, int totalColumns)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedTotalColumns = Math.Max(1, totalColumns);
        var cascade = Math.Clamp(settings.CascadeNum, 1, normalizedTotalColumns);
        if (settings.PerIcXChannels.Count == cascade &&
            settings.PerIcXChannels.Sum() == normalizedTotalColumns)
        {
            return settings.PerIcXChannels.ToArray();
        }

        var baseColumns = normalizedTotalColumns / cascade;
        var remainder = normalizedTotalColumns % cascade;
        var result = new int[cascade];
        for (var ic = 0; ic < cascade; ic++)
        {
            result[ic] = baseColumns + (ic < remainder ? 1 : 0);
        }

        return result;
    }

    public static int ResolveIcIndexForColumn(int column, IReadOnlyList<int> perIcColumns)
    {
        ArgumentNullException.ThrowIfNull(perIcColumns);

        var accumulated = 0;
        for (var ic = 0; ic < perIcColumns.Count; ic++)
        {
            accumulated += perIcColumns[ic];
            if (column < accumulated)
            {
                return ic;
            }
        }

        return Math.Max(0, perIcColumns.Count - 1);
    }
}
