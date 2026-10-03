using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Detects exact same-layer duplicate CAD pads while preserving original pad IDs.
/// The lowest-ID pad in each same-layer duplicate group is treated as the canonical pad.
/// </summary>
public static class CadPadExactDuplicateSanitizer
{
    public static CadPadExactDuplicateSanitizationResult Sanitize(CadPadSet cad)
    {
        ArgumentNullException.ThrowIfNull(cad);

        if (cad.Pads.Count == 0)
        {
            return new CadPadExactDuplicateSanitizationResult(
                Cad: cad,
                DuplicateSameLayerGroupCount: 0,
                ExcludedPadIds: Array.Empty<int>(),
                DuplicateToCanonicalPadId: new Dictionary<int, int>());
        }

        var signatureMap = new Dictionary<string, List<CadPad>>(StringComparer.Ordinal);
        foreach (var pad in cad.Pads)
        {
            var signature = CadPadGeometrySignature.Build(pad.Polygon);
            if (!signatureMap.TryGetValue(signature, out var list))
            {
                list = new List<CadPad>();
                signatureMap[signature] = list;
            }

            list.Add(pad);
        }

        var excludedPadIds = new HashSet<int>();
        var duplicateToCanonicalPadId = new Dictionary<int, int>();
        var duplicateGroupCount = 0;

        foreach (var signatureGroup in signatureMap.Values)
        {
            foreach (var layerGroup in signatureGroup.GroupBy(
                         static pad => pad.Layer ?? string.Empty,
                         StringComparer.OrdinalIgnoreCase))
            {
                var ordered = layerGroup.OrderBy(static pad => pad.Id).ToList();
                if (ordered.Count < 2)
                {
                    continue;
                }

                duplicateGroupCount++;
                var canonicalPadId = ordered[0].Id;
                foreach (var duplicate in ordered.Skip(1))
                {
                    excludedPadIds.Add(duplicate.Id);
                    duplicateToCanonicalPadId[duplicate.Id] = canonicalPadId;
                }
            }
        }

        return new CadPadExactDuplicateSanitizationResult(
            Cad: cad,
            DuplicateSameLayerGroupCount: duplicateGroupCount,
            ExcludedPadIds: excludedPadIds.OrderBy(static id => id).ToArray(),
            DuplicateToCanonicalPadId: duplicateToCanonicalPadId);
    }
}

public sealed record CadPadExactDuplicateSanitizationResult(
    CadPadSet Cad,
    int DuplicateSameLayerGroupCount,
    IReadOnlyList<int> ExcludedPadIds,
    IReadOnlyDictionary<int, int> DuplicateToCanonicalPadId)
{
    public bool HasExcludedDuplicates => ExcludedPadIds.Count > 0;
}
