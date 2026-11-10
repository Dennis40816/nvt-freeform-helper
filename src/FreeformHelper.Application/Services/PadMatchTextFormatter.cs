// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>
/// Formats the short pad match texts shared by pad info and the inspector.
/// </summary>
public static class PadMatchTextFormatter
{
    /// <summary>
    /// Shows up to three values in their supplied order, followed by the remaining count.
    /// </summary>
    /// <param name="values">The values to display without sorting or removing duplicates.</param>
    /// <returns>The compact list, or a dash when empty.</returns>
    public static string CompactList(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return "-";
        }

        if (values.Count <= 3)
        {
            return string.Join(", ", values);
        }

        return $"{values[0]}, {values[1]}, {values[2]} (+{values.Count - 3})";
    }

    /// <summary>
    /// Formats the CAD identities and available DXF indices matched to a regular pad.
    /// </summary>
    /// <param name="cadIds">The matched CAD identities in display order.</param>
    /// <param name="dxfIndices">The distinct, sorted DXF indices prepared by the caller.</param>
    /// <returns>The regular pad's short match text.</returns>
    public static string RegularMatchText(IReadOnlyList<int> cadIds, IReadOnlyList<int> dxfIndices)
    {
        if (cadIds.Count == 0)
        {
            return "Unmatched";
        }

        var cadListText = CompactList(cadIds);
        if (dxfIndices.Count > 0)
        {
            var dxfListText = CompactList(dxfIndices);
            return $"CAD idx {dxfListText} (CAD id {cadListText})";
        }

        return $"CAD id {cadListText}";
    }

    /// <summary>
    /// Shows the best regular match followed by the remaining distinct match count.
    /// </summary>
    /// <param name="regularIds">The matched regular identities.</param>
    /// <param name="linkedRegularIds">The regular identities projected from links in their existing priority order.</param>
    /// <param name="getRegularPadIcDiff">The caller's lookup for a regular identity's IC and diff indices.</param>
    /// <returns>The CAD pad's short match text.</returns>
    public static string CadMatchText(
        IReadOnlyList<int> regularIds,
        IEnumerable<int> linkedRegularIds,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        if (regularIds.Count == 0)
        {
            return "Unmatched";
        }

        var ordered = linkedRegularIds
            .Concat(regularIds)
            .Distinct()
            .ToList();
        var bestRegularId = ordered[0];
        var bestText = BuildRegularMatchKey(bestRegularId, getRegularPadIcDiff);
        if (ordered.Count == 1)
        {
            return bestText;
        }

        return $"{bestText} (+{ordered.Count - 1})";
    }

    private static string BuildRegularMatchKey(
        int regularPadId,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        var match = getRegularPadIcDiff?.Invoke(regularPadId);
        return match.HasValue
            ? $"IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex}"
            : $"regId {regularPadId}";
    }
}
