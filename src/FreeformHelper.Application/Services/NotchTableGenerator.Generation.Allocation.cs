using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{
    private List<CadAllocationProfile> BuildCadAllocationProfiles(
        IReadOnlyList<CadPad> cadPads,
        RegularGrid grid,
        IProgress<NotchGenerationProgress>? progress = null,
        string? phase = null,
        int phaseStep = 0,
        int phaseStepCount = 0)
    {
        ResetCadAllocationMemoIfGridChanged(grid);
        var profiles = new List<CadAllocationProfile>(cadPads.Count);
        var totalCount = cadPads.Count;
        ReportGenerationProgress(
            progress,
            0,
            totalCount,
            0,
            null,
            null,
            phase,
            phaseStep,
            phaseStepCount);
        for (var index = 0; index < cadPads.Count; index++)
        {
            var cadPad = cadPads[index];
            var allocations = GetOrBuildCadAllocations(cadPad, grid);
            if (allocations.Count == 0)
            {
                profiles.Add(new CadAllocationProfile(
                    CadPad: cadPad,
                    Anchor: null,
                    MaxRatio: 0.0,
                    IcIndices: Array.Empty<int>(),
                    HasAllocations: false,
                    Allocations: Array.Empty<NotchAllocation>()));
                if (ShouldReportGenerationProgress(index + 1, totalCount))
                {
                    ReportGenerationProgress(
                        progress,
                        index + 1,
                        totalCount,
                        0,
                        cadPad.Id,
                        null,
                        phase,
                        phaseStep,
                        phaseStepCount);
                }

                continue;
            }

            var anchor = SelectCadAllocationAnchor(cadPad.Id, allocations);
            var icIndices = allocations
                .Select(allocation => allocation.Pad.IcIndex)
                .Distinct()
                .OrderBy(ic => ic)
                .ToArray();
            profiles.Add(new CadAllocationProfile(
                CadPad: cadPad,
                Anchor: anchor,
                MaxRatio: allocations[0].Ratio,
                IcIndices: icIndices,
                HasAllocations: true,
                Allocations: allocations));

            if (ShouldReportGenerationProgress(index + 1, totalCount))
            {
                ReportGenerationProgress(
                    progress,
                    index + 1,
                    totalCount,
                    0,
                    cadPad.Id,
                    anchor?.RegularPadId,
                    phase,
                    phaseStep,
                    phaseStepCount);
            }
        }

        ReportGenerationProgress(
            progress,
            totalCount,
            totalCount,
            0,
            null,
            null,
            phase,
            phaseStep,
            phaseStepCount);
        return profiles;
    }

    private static Dictionary<int, IReadOnlyList<CadPad>> BuildCadPoolByIc(IReadOnlyList<CadAllocationProfile> profiles)
    {
        var builder = new Dictionary<int, Dictionary<int, CadPad>>();
        foreach (var profile in profiles)
        {
            if (profile.IcIndices.Count == 0)
            {
                continue;
            }

            foreach (var icIndex in profile.IcIndices)
            {
                if (!builder.TryGetValue(icIndex, out var byCadId))
                {
                    byCadId = new Dictionary<int, CadPad>();
                    builder[icIndex] = byCadId;
                }

                byCadId[profile.CadPad.Id] = profile.CadPad;
            }
        }

        return builder.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<CadPad>)item.Value
                .OrderBy(cad => cad.Key)
                .Select(cad => cad.Value)
                .ToArray());
    }

    private static RegularPad? SelectCadAllocationAnchor(
        int cadPadId,
        IReadOnlyList<NotchAllocation> allocations,
        int? icIndex = null)
    {
        if (allocations.Count == 0)
        {
            return null;
        }

        var scopedAllocations = icIndex.HasValue
            ? allocations
                .Where(allocation => allocation.Pad.IcIndex == icIndex.Value)
                .ToList()
            : allocations.ToList();
        if (scopedAllocations.Count == 0)
        {
            return null;
        }

        var candidates = scopedAllocations
            .Where(allocation => allocation.Pad.Freeform != FreeformType.None && allocation.Pad.MatchedCadPadId == cadPadId)
            .ToList();

        if (candidates.Count == 0)
        {
            // Fall back to same CAD match even when the candidate is not freeform.
            candidates = scopedAllocations
                .Where(allocation => allocation.Pad.MatchedCadPadId == cadPadId)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            candidates = scopedAllocations
                .Where(allocation => allocation.Pad.Freeform != FreeformType.None)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            candidates = scopedAllocations.ToList();
        }

        return candidates
            .OrderByDescending(allocation => allocation.Ratio)
            .ThenByDescending(allocation => allocation.Q7)
            .ThenBy(allocation => allocation.Pad.IcIndex)
            .ThenBy(allocation => allocation.Pad.DiffIndex)
            .Select(allocation => allocation.Pad)
            .First();
    }

    private static Dictionary<int, double> BuildCadMaxAllocation(Dictionary<int, CadPad> cadById, RegularGrid grid)
    {
        var result = new Dictionary<int, double>();

        foreach (var reg in grid.Pads)
        {
            if (reg.MatchedCadPadId is null || reg.MatchScore <= 0)
            {
                continue;
            }

            if (!cadById.TryGetValue(reg.MatchedCadPadId.Value, out var cad))
            {
                continue;
            }

            var current = result.TryGetValue(cad.Id, out var maxRatio) ? maxRatio : 0.0;
            var overlap = Polygon2.IntersectionAreaWithRect(cad.Polygon, reg.Bounds);
            var ratio = overlap / Math.Max(cad.Area, 1e-12);
            if (ratio > current)
            {
                result[cad.Id] = ratio;
            }
            else if (!result.ContainsKey(cad.Id))
            {
                result[cad.Id] = current;
            }
        }

        return result;
    }

    private sealed record CadAllocationProfile(
        CadPad CadPad,
        RegularPad? Anchor,
        double MaxRatio,
        IReadOnlyList<int> IcIndices,
        bool HasAllocations,
        IReadOnlyList<NotchAllocation> Allocations);
}
