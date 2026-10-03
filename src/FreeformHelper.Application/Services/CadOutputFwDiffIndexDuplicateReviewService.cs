using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static class CadOutputFwDiffIndexDuplicateReviewService
{
    public static IReadOnlyList<CadOutputFwDiffIndexDuplicateReviewGroup> BuildGroups(
        IReadOnlyList<CadPad> visibleCadPads,
        IReadOnlyDictionary<int, int> currentDiffByCadId,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlyDictionary<int, CadBestMatchSeed>? rawBestSeedByCadId = null,
        IReadOnlyDictionary<int, CadBestMatchSeed>? maskedBestSeedByCadId = null)
    {
        ArgumentNullException.ThrowIfNull(visibleCadPads);
        ArgumentNullException.ThrowIfNull(currentDiffByCadId);
        ArgumentNullException.ThrowIfNull(cadIcIndexByCadId);

        return visibleCadPads
            .Where(pad => currentDiffByCadId.ContainsKey(pad.Id) && cadIcIndexByCadId.ContainsKey(pad.Id))
            .GroupBy(pad => (IcIndex: cadIcIndexByCadId[pad.Id], DiffIndex: currentDiffByCadId[pad.Id]))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.IcIndex)
            .ThenBy(group => group.Key.DiffIndex)
            .Select(group => new CadOutputFwDiffIndexDuplicateReviewGroup(
                group.Key.IcIndex,
                group.Key.DiffIndex,
                group
                    .OrderBy(pad => pad.Id)
                    .Select(pad => BuildEntry(
                        pad,
                        group.Key.IcIndex,
                        group.Key.DiffIndex,
                        rawBestSeedByCadId,
                        maskedBestSeedByCadId))
                    .ToArray()))
            .ToArray();
    }

    private static CadOutputFwDiffIndexDuplicateReviewEntry BuildEntry(
        CadPad pad,
        int icIndex,
        int currentDiffIndex,
        IReadOnlyDictionary<int, CadBestMatchSeed>? rawBestSeedByCadId,
        IReadOnlyDictionary<int, CadBestMatchSeed>? maskedBestSeedByCadId)
    {
        CadBestMatchSeed? rawBestSeed = null;
        CadBestMatchSeed? maskedBestSeed = null;
        if (rawBestSeedByCadId is not null && rawBestSeedByCadId.TryGetValue(pad.Id, out var rawSeed))
        {
            rawBestSeed = rawSeed;
        }

        if (maskedBestSeedByCadId is not null && maskedBestSeedByCadId.TryGetValue(pad.Id, out var maskedSeed))
        {
            maskedBestSeed = maskedSeed;
        }

        return new CadOutputFwDiffIndexDuplicateReviewEntry(
            CadPadId: pad.Id,
            LayerName: pad.Layer,
            IcIndex: icIndex,
            CurrentDiffIndex: currentDiffIndex,
            RawBestDiffIndex: rawBestSeed?.BestMatchFwDiffIndex,
            RawBestRegularPadIndex: rawBestSeed?.RegularPadIndex,
            RawBestRegularCoverage: rawBestSeed?.RegularCoverage,
            MaskedBestDiffIndex: maskedBestSeed?.BestMatchFwDiffIndex,
            MaskedBestRegularPadIndex: maskedBestSeed?.RegularPadIndex,
            MaskedBestRegularCoverage: maskedBestSeed?.RegularCoverage);
    }
}

public sealed record CadOutputFwDiffIndexDuplicateReviewGroup(
    int IcIndex,
    int DiffIndex,
    IReadOnlyList<CadOutputFwDiffIndexDuplicateReviewEntry> Entries);

public sealed record CadOutputFwDiffIndexDuplicateReviewEntry(
    int CadPadId,
    string LayerName,
    int IcIndex,
    int CurrentDiffIndex,
    int? RawBestDiffIndex,
    int? RawBestRegularPadIndex,
    double? RawBestRegularCoverage,
    int? MaskedBestDiffIndex,
    int? MaskedBestRegularPadIndex,
    double? MaskedBestRegularCoverage);
