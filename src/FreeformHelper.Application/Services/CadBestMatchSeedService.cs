using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static class CadBestMatchSeedService
{
    public static Dictionary<int, CadBestMatchSeed> BuildSeedDetailsByCadId(
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> cadToRegular,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlySet<int>? activeRegularPadIds = null)
    {
        ArgumentNullException.ThrowIfNull(orderedCadPads);
        ArgumentNullException.ThrowIfNull(cadToRegular);
        ArgumentNullException.ThrowIfNull(regularPadById);
        ArgumentNullException.ThrowIfNull(cadIcIndexByCadId);

        var seedByCadId = new Dictionary<int, CadBestMatchSeed>(orderedCadPads.Count);
        for (var i = 0; i < orderedCadPads.Count; i++)
        {
            var cadPad = orderedCadPads[i];
            seedByCadId[cadPad.Id] = ResolveBestMatchSeed(
                cadPad,
                cadToRegular,
                regularPadById,
                cadIcIndexByCadId,
                activeRegularPadIds);
        }

        return seedByCadId;
    }

    public static Dictionary<int, int?> BuildSeedByCadId(
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> cadToRegular,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlySet<int>? activeRegularPadIds = null)
    {
        return BuildSeedDetailsByCadId(
                orderedCadPads,
                cadToRegular,
                regularPadById,
                cadIcIndexByCadId,
                activeRegularPadIds)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value.BestMatchFwDiffIndex);
    }

    private static CadBestMatchSeed ResolveBestMatchSeed(
        CadPad cadPad,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> cadToRegular,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        if (!cadToRegular.TryGetValue(cadPad.Id, out var links) || links.Count == 0)
        {
            return CadBestMatchSeed.Empty;
        }

        cadIcIndexByCadId.TryGetValue(cadPad.Id, out var cadIcIndex);
        foreach (var link in links)
        {
            if (activeRegularPadIds is not null && !activeRegularPadIds.Contains(link.RegularPadId))
            {
                continue;
            }

            if (!regularPadById.TryGetValue(link.RegularPadId, out var regularPad))
            {
                continue;
            }

            if (cadIcIndexByCadId.ContainsKey(cadPad.Id) && regularPad.IcIndex != cadIcIndex)
            {
                continue;
            }

            return new CadBestMatchSeed(
                regularPad.RegularPadId,
                regularPad.IcIndex,
                regularPad.DiffIndex,
                link.RegularCoverage,
                link.CadCoverage);
        }

        return CadBestMatchSeed.Empty;
    }
}

public sealed record CadBestMatchSeed(
    int? RegularPadIndex,
    int? IcIndex,
    int? BestMatchFwDiffIndex,
    double? RegularCoverage,
    double? CadCoverage)
{
    public static readonly CadBestMatchSeed Empty = new(
        RegularPadIndex: null,
        IcIndex: null,
        BestMatchFwDiffIndex: null,
        RegularCoverage: null,
        CadCoverage: null);

    public bool HasMatch => RegularPadIndex.HasValue && BestMatchFwDiffIndex.HasValue;
}
