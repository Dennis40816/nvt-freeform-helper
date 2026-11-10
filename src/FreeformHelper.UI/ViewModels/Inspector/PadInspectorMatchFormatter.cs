using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Formats pad inspector match and assignment text.
/// </summary>
internal static class PadInspectorMatchFormatter
{
    private const int MaxInspectorMatchDetailLines = 8;

    internal static string BuildCadOutputFwDiffAssignmentModeText(
        bool hasCadOutputFwDiffOverride,
        bool isDxfIndexAnchor,
        CadOutputFwDiffAutoMode autoMode)
    {
        if (hasCadOutputFwDiffOverride)
        {
            return "Manual override";
        }

        if (isDxfIndexAnchor)
        {
            return "Anchor saved (inactive in current mode)";
        }

        return autoMode == CadOutputFwDiffAutoMode.BestMatchDirect
            ? "Auto (geometry seed)"
            : "Auto (geometry seed, unique per IC)";
    }

    internal static string BuildRegularDiffSource(
        int regularDiffIndex,
        List<PadInspectorMatchedCadSnapshot> matchedCadDetails)
    {
        if (matchedCadDetails.Count == 0)
        {
            return "Unmatched";
        }

        if (matchedCadDetails.Any(detail => detail.DxfIndex.HasValue && detail.DxfIndex.Value == regularDiffIndex))
        {
            return "Strict";
        }

        if (matchedCadDetails.Any(detail => detail.DxfIndex.HasValue))
        {
            return "Probable";
        }

        return "Auto";
    }

    internal static string BuildCadMatchText(
        List<int> regularIds,
        List<PadMatchLink> links,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        if (regularIds.Count == 0)
        {
            return PadMatchTextFormatter.CadMatchText(regularIds, Array.Empty<int>(), getRegularPadIcDiff);
        }

        return PadMatchTextFormatter.CadMatchText(
            regularIds, links.Select(link => link.RegularPadId), getRegularPadIcDiff);
    }

    internal static string BuildCadMatchDetailsText(
        List<int> regularIds,
        List<PadMatchLink> links,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        if (regularIds.Count == 0)
        {
            return "Unmatched";
        }

        if (links.Count == 0)
        {
            var texts = regularIds
                .Take(MaxInspectorMatchDetailLines)
                .Select(regularId => BuildRegularMatchDetail(regularId, getRegularPadIcDiff))
                .ToList();
            if (regularIds.Count > MaxInspectorMatchDetailLines)
            {
                texts.Add($"... (+{regularIds.Count - MaxInspectorMatchDetailLines} more)");
            }

            return $"Regular: {string.Join(", ", texts)}";
        }

        var lines = links
            .Take(MaxInspectorMatchDetailLines)
            .Select(link => $"{BuildRegularMatchDetail(link.RegularPadId, getRegularPadIcDiff)}: {(link.CadCoverage * 100):0.#}%")
            .ToList();
        if (links.Count > MaxInspectorMatchDetailLines)
        {
            lines.Add($"... (+{links.Count - MaxInspectorMatchDetailLines} more)");
        }

        return string.Join(Environment.NewLine, lines);
    }

    internal static string BuildRegularMatchDetail(
        int regularPadId,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        var match = getRegularPadIcDiff?.Invoke(regularPadId);
        return match.HasValue
            ? $"IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex} (regId {regularPadId})"
            : $"regId {regularPadId}";
    }

    internal static string BuildRegularMatchText(
        IReadOnlyList<PadInspectorMatchedCadSnapshot> matchedCadDetails,
        List<int> cadIds)
    {
        if (cadIds.Count == 0)
        {
            return PadMatchTextFormatter.RegularMatchText(cadIds, Array.Empty<int>());
        }

        var dxfIndices = matchedCadDetails
            .Select(detail => detail.DxfIndex)
            .Where(index => index.HasValue)
            .Select(index => index!.Value)
            .Distinct()
            .OrderBy(index => index)
            .ToList();

        return PadMatchTextFormatter.RegularMatchText(cadIds, dxfIndices);
    }

    internal static string BuildRegularMatchDetailsText(
        List<PadInspectorMatchedCadSnapshot> matchedCadDetails,
        List<PadMatchLink> links)
    {
        if (matchedCadDetails.Count == 0)
        {
            return "Unmatched";
        }

        if (links.Count == 0)
        {
            var ids = matchedCadDetails
                .Select(detail => detail.CadPadId.ToString(CultureInfo.InvariantCulture))
                .Take(MaxInspectorMatchDetailLines)
                .ToList();
            if (matchedCadDetails.Count > MaxInspectorMatchDetailLines)
            {
                ids.Add($"... (+{matchedCadDetails.Count - MaxInspectorMatchDetailLines} more)");
            }

            return $"CAD ids: {string.Join(", ", ids)}";
        }

        var lines = new List<string>(Math.Min(links.Count, MaxInspectorMatchDetailLines) + 1);
        foreach (var link in links.Take(MaxInspectorMatchDetailLines))
        {
            var detail = matchedCadDetails.FirstOrDefault(item => item.CadPadId == link.CadPadId);
            var indexText = detail?.DxfIndex is int dxfIndex
                ? $"idx {dxfIndex}"
                : $"id {link.CadPadId}";
            lines.Add($"CAD {indexText}: {(link.CadCoverage * 100):0.#}%");
        }

        if (links.Count > MaxInspectorMatchDetailLines)
        {
            lines.Add($"... (+{links.Count - MaxInspectorMatchDetailLines} more)");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
