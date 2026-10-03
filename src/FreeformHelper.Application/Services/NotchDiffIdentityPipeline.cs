using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static class NotchDiffIdentityPipeline
{
    private const double NearZeroEpsilon = 1e-9;

    public static NotchDiffProjectionContract BuildProjectionContract(
        IReadOnlyList<NotchTableRow> rows,
        IReadOnlyDictionary<int, int> cadOutputFwDiffByCadId)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(cadOutputFwDiffByCadId);

        if (rows.Count == 0 || cadOutputFwDiffByCadId.Count == 0)
        {
            return new NotchDiffProjectionContract(
                ProjectedCadOutputFwDiffByRawDiff: new Dictionary<NotchDiffKey, int>(),
                ConflictedRawDiffKeys: Array.Empty<NotchDiffKey>(),
                CadOutputFwDiffByCadId: cadOutputFwDiffByCadId);
        }

        var projectedCadOutputFwDiffByRawDiff = new Dictionary<NotchDiffKey, int>();
        var conflictedRawDiffKeys = new HashSet<NotchDiffKey>();
        foreach (var row in rows)
        {
            if (row.CadPadId is not int cadPadId ||
                !cadOutputFwDiffByCadId.TryGetValue(cadPadId, out var cadOutputFwDiff))
            {
                continue;
            }

            var diffKey = new NotchDiffKey(row.IcIndex, row.DiffIndex);
            if (!projectedCadOutputFwDiffByRawDiff.TryAdd(diffKey, cadOutputFwDiff) &&
                projectedCadOutputFwDiffByRawDiff[diffKey] != cadOutputFwDiff)
            {
                conflictedRawDiffKeys.Add(diffKey);
            }
        }

        if (conflictedRawDiffKeys.Count > 0)
        {
            foreach (var diffKey in conflictedRawDiffKeys)
            {
                projectedCadOutputFwDiffByRawDiff.Remove(diffKey);
            }
        }

        var orderedConflictedRawDiffKeys = conflictedRawDiffKeys
            .OrderBy(static key => key.IcIndex)
            .ThenBy(static key => key.DiffIndex)
            .ToArray();
        return new NotchDiffProjectionContract(
            ProjectedCadOutputFwDiffByRawDiff: projectedCadOutputFwDiffByRawDiff,
            ConflictedRawDiffKeys: orderedConflictedRawDiffKeys,
            CadOutputFwDiffByCadId: cadOutputFwDiffByCadId);
    }

    public static NotchActiveDiffBaseline BuildActiveDiffBaseline(
        IReadOnlyList<RegularPad> regularPads,
        IReadOnlySet<int> activeRegularPadIds,
        IReadOnlyDictionary<int, double> valueByRegularPadId)
    {
        ArgumentNullException.ThrowIfNull(regularPads);
        ArgumentNullException.ThrowIfNull(activeRegularPadIds);
        ArgumentNullException.ThrowIfNull(valueByRegularPadId);

        var valueByDiffKey = new Dictionary<NotchDiffKey, double>(regularPads.Count);
        var contributingRegularPadIdsByDiffKey = new Dictionary<NotchDiffKey, List<int>>();

        foreach (var regularPad in regularPads)
        {
            var diffKey = new NotchDiffKey(regularPad.IcIndex, regularPad.DiffIndex);
            var isActivePad = activeRegularPadIds.Contains(regularPad.RegularPadId);
            var beforeValue = isActivePad
                ? valueByRegularPadId.GetValueOrDefault(regularPad.RegularPadId)
                : 0d;

            if (!valueByDiffKey.TryGetValue(diffKey, out var existingValue))
            {
                valueByDiffKey[diffKey] = beforeValue;
                if (!isActivePad || Math.Abs(beforeValue) <= NearZeroEpsilon)
                {
                    continue;
                }

                contributingRegularPadIdsByDiffKey[diffKey] = new List<int> { regularPad.RegularPadId };
                continue;
            }

            if (!isActivePad || Math.Abs(beforeValue) <= NearZeroEpsilon)
            {
                continue;
            }

            valueByDiffKey[diffKey] = existingValue + beforeValue;

            if (!contributingRegularPadIdsByDiffKey.TryGetValue(diffKey, out var contributingRegularPadIds))
            {
                contributingRegularPadIds = new List<int>();
                contributingRegularPadIdsByDiffKey[diffKey] = contributingRegularPadIds;
            }

            if (!contributingRegularPadIds.Contains(regularPad.RegularPadId))
            {
                contributingRegularPadIds.Add(regularPad.RegularPadId);
            }
        }

        var duplicateResolutions = contributingRegularPadIdsByDiffKey
            .Where(static entry => entry.Value.Count > 1)
            .OrderBy(static entry => entry.Key.IcIndex)
            .ThenBy(static entry => entry.Key.DiffIndex)
            .Select(static entry =>
            {
                var contributorRegularPadIds = entry.Value;
                var primaryRegularPadId = contributorRegularPadIds[0];
                var suppressedRegularPadIds = contributorRegularPadIds.Skip(1).ToArray();
                return new NotchDiffDuplicateResolution(
                    entry.Key,
                    primaryRegularPadId,
                    suppressedRegularPadIds);
            })
            .ToArray();
        return new NotchActiveDiffBaseline(valueByDiffKey, duplicateResolutions);
    }
}

public readonly record struct NotchDiffKey(int IcIndex, int DiffIndex);

public sealed record NotchDiffProjectionContract(
    IReadOnlyDictionary<NotchDiffKey, int> ProjectedCadOutputFwDiffByRawDiff,
    IReadOnlyList<NotchDiffKey> ConflictedRawDiffKeys,
    IReadOnlyDictionary<int, int> CadOutputFwDiffByCadId)
{
    public int ConflictedRawDiffKeyCount => ConflictedRawDiffKeys.Count;

    public int ResolveAnchorDiff(NotchTableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.CadPadId is int cadPadId &&
            CadOutputFwDiffByCadId.TryGetValue(cadPadId, out var projectedAnchorDiff)
            ? projectedAnchorDiff
            : row.DiffIndex;
    }

    public int ResolveTargetDiff(int icIndex, int rawTargetDiff)
    {
        return ProjectedCadOutputFwDiffByRawDiff.TryGetValue(new NotchDiffKey(icIndex, rawTargetDiff), out var projectedTargetDiff)
            ? projectedTargetDiff
            : rawTargetDiff;
    }
}

public sealed record NotchDiffDuplicateResolution(
    NotchDiffKey DiffKey,
    int PrimaryRegularPadId,
    IReadOnlyList<int> SuppressedRegularPadIds);

public sealed record NotchActiveDiffBaseline(
    IReadOnlyDictionary<NotchDiffKey, double> ValueByDiffKey,
    IReadOnlyList<NotchDiffDuplicateResolution> DuplicateResolutions);
