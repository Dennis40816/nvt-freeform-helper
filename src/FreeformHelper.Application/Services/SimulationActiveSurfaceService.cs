using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static class SimulationActiveSurfaceService
{
    public static IReadOnlySet<int> BuildActiveRegularPadIds(
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> cadToRegular,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlySet<int>? allowedRegularPadIds = null)
    {
        ArgumentNullException.ThrowIfNull(orderedCadPads);
        ArgumentNullException.ThrowIfNull(cadToRegular);
        ArgumentNullException.ThrowIfNull(regularPadById);
        ArgumentNullException.ThrowIfNull(cadIcIndexByCadId);

        var activeIds = new HashSet<int>();
        var seedByCadId = CadBestMatchSeedService.BuildSeedDetailsByCadId(
            orderedCadPads,
            cadToRegular,
            regularPadById,
            cadIcIndexByCadId,
            allowedRegularPadIds);

        foreach (var seed in seedByCadId.Values)
        {
            if (!seed.RegularPadIndex.HasValue)
            {
                continue;
            }

            var regularPadId = seed.RegularPadIndex.Value;
            if (allowedRegularPadIds is not null && !allowedRegularPadIds.Contains(regularPadId))
            {
                continue;
            }

            activeIds.Add(regularPadId);
        }

        return activeIds;
    }
}
