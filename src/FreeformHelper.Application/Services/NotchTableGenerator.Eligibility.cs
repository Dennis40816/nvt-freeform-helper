using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{
    private NotchCadRowEligibility EvaluateCadAllocationEligibility(
        CadPad cadPad,
        RegularGrid grid,
        ProjectSettings settings,
        IReadOnlyList<NotchAlgorithmVersion> enabled)
    {
        var allocations = GetOrBuildCadAllocations(cadPad, grid);
        if (allocations.Count == 0)
        {
            return new NotchCadRowEligibility(
                CadPadId: cadPad.Id,
                EligibleVersions: Array.Empty<NotchAlgorithmVersion>(),
                EstimatedRowCount: 0,
                AnchorRegularPadId: null,
                Reason: "No regular overlap.");
        }

        var anchor = SelectCadAllocationAnchor(cadPad.Id, allocations);
        var maxRatio = allocations[0].Ratio;
        if (anchor is null)
        {
            return new NotchCadRowEligibility(
                CadPadId: cadPad.Id,
                EligibleVersions: Array.Empty<NotchAlgorithmVersion>(),
                EstimatedRowCount: 0,
                AnchorRegularPadId: null,
                Reason: "No Step2 freeform anchor.");
        }

        var eligible = new List<NotchAlgorithmVersion>();
        var blocked = new List<string>();
        foreach (var version in enabled)
        {
            if (!PassesThresholdForVersion(version, maxRatio, settings.Notch))
            {
                blocked.Add($"{version.ToDisplayLabel()}: below gate TH");
                continue;
            }

            if (!CanBuildLegacyRow(version, anchor, cadPad))
            {
                blocked.Add($"{version.ToDisplayLabel()}: freeform not compatible ({anchor.Freeform})");
                continue;
            }

            eligible.Add(version);
        }

        var reason = eligible.Count > 0
            ? $"Anchor REG{anchor.RegularPadId}, freeform={anchor.Freeform}."
            : blocked.Count > 0
                ? string.Join("; ", blocked)
                : "No eligible strategy.";

        return new NotchCadRowEligibility(
            CadPadId: cadPad.Id,
            EligibleVersions: eligible,
            EstimatedRowCount: eligible.Count,
            AnchorRegularPadId: anchor.RegularPadId,
            Reason: reason);
    }

    private static NotchCadRowEligibility EvaluateLegacyEligibility(
        CadPad cadPad,
        RegularGrid grid,
        ProjectSettings settings,
        IReadOnlyList<NotchAlgorithmVersion> enabled)
    {
        var candidates = grid.Pads
            .Where(reg => reg.Freeform != FreeformType.None && reg.MatchedCadPadId == cadPad.Id)
            .ToList();
        if (candidates.Count == 0)
        {
            return new NotchCadRowEligibility(
                CadPadId: cadPad.Id,
                EligibleVersions: Array.Empty<NotchAlgorithmVersion>(),
                EstimatedRowCount: 0,
                AnchorRegularPadId: null,
                Reason: "No matched freeform regular.");
        }

        var cadArea = Math.Max(cadPad.Area, 1e-12);
        var maxRatio = candidates
            .Select(reg => Polygon2.IntersectionAreaWithRect(cadPad.Polygon, reg.Bounds) / cadArea)
            .DefaultIfEmpty(0.0)
            .Max();
        var bestAnchor = candidates
            .OrderByDescending(reg => Polygon2.IntersectionAreaWithRect(cadPad.Polygon, reg.Bounds))
            .ThenBy(reg => reg.RegularPadId)
            .First();

        var eligibleVersions = new HashSet<NotchAlgorithmVersion>();
        var rowCount = 0;
        foreach (var version in enabled)
        {
            if (!PassesThresholdForVersion(version, maxRatio, settings.Notch))
            {
                continue;
            }

            var countForVersion = candidates.Count(reg => CanBuildLegacyRow(version, reg, cadPad));
            if (countForVersion <= 0)
            {
                continue;
            }

            eligibleVersions.Add(version);
            rowCount += countForVersion;
        }

        var reason = rowCount > 0
            ? $"Legacy mode with {candidates.Count} matched freeform regular(s)."
            : "No eligible strategy after gate/can-handle checks.";

        return new NotchCadRowEligibility(
            CadPadId: cadPad.Id,
            EligibleVersions: eligibleVersions.OrderBy(v => (int)v).ToList(),
            EstimatedRowCount: rowCount,
            AnchorRegularPadId: bestAnchor.RegularPadId,
            Reason: reason);
    }
}
