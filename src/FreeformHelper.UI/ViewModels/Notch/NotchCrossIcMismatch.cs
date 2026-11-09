// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal static class NotchCrossIcMismatch
{
    internal static CrossIcMismatchSnapshot BuildCrossIcMismatchSnapshot(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var crossIcGroups = grid.Pads
            .Where(pad => pad.MatchedCadPadId.HasValue)
            .GroupBy(pad => pad.MatchedCadPadId!.Value)
            .Select(group => new
            {
                CadId = group.Key,
                RegularCount = group.Count(),
                IcIndices = group
                    .Select(pad => pad.IcIndex)
                    .Distinct()
                    .OrderBy(ic => ic)
                    .ToList(),
            })
            .Where(item => item.IcIndices.Count > 1)
            .OrderBy(item => item.CadId)
            .ToList();

        if (crossIcGroups.Count == 0)
        {
            return new CrossIcMismatchSnapshot(
                CadCount: 0,
                RegularCount: 0,
                SampleCadText: string.Empty);
        }

        var sample = crossIcGroups
            .Take(6)
            .Select(item => $"CAD{item.CadId}(IC:{string.Join("/", item.IcIndices.Select(ic => ic + 1))})");
        var suffix = crossIcGroups.Count > 6 ? $"+{crossIcGroups.Count - 6}" : string.Empty;
        return new CrossIcMismatchSnapshot(
            CadCount: crossIcGroups.Count,
            RegularCount: crossIcGroups.Sum(item => item.RegularCount),
            SampleCadText: string.Join(", ", sample) + suffix);
    }
}

internal readonly record struct CrossIcMismatchSnapshot(
    int CadCount,
    int RegularCount,
    string SampleCadText);
