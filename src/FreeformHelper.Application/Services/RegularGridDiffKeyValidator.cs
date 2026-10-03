using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static class RegularGridDiffKeyValidator
{
    public static IReadOnlyList<RegularGridDuplicateDiffGroup> FindDuplicateDiffGroups(
        RegularGrid grid,
        IReadOnlySet<int>? activeRegularPadIds = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        return grid.Pads
            .Where(pad => activeRegularPadIds is null || activeRegularPadIds.Contains(pad.RegularPadId))
            .GroupBy(static pad => (pad.IcIndex, pad.DiffIndex))
            .Where(static group => group.Count() > 1)
            .OrderBy(static group => group.Key.IcIndex)
            .ThenBy(static group => group.Key.DiffIndex)
            .Select(static group => new RegularGridDuplicateDiffGroup(
                group.Key.IcIndex,
                group.Key.DiffIndex,
                group.Select(static pad => pad.RegularPadId).OrderBy(static id => id).ToArray()))
            .ToArray();
    }
}

public sealed record RegularGridDuplicateDiffGroup(
    int IcIndex,
    int DiffIndex,
    IReadOnlyList<int> RegularPadIds);
