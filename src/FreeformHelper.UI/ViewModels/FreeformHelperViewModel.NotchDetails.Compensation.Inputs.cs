using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private IReadOnlyList<CadPad> ResolveCadPadsForComputationSource()
    {
        return CadPads.Count > 0 ? CadPads : (_cad?.Pads ?? CadPads);
    }

    private List<CadPad> GetCadPadsForNotchComputation(int? targetCadPadId = null)
    {
        var sourceCadPads = ResolveCadPadsForComputationSource();
        var visibleCadPads = sourceCadPads
            .Where(pad => !IsCadPadEffectivelyHidden(pad.Id))
            .OrderBy(static pad => pad.Id)
            .ToList();
        if (visibleCadPads.Count == 0)
        {
            return visibleCadPads;
        }

        if (_grid is null ||
            !targetCadPadId.HasValue ||
            !TryGetCadIcIndex(targetCadPadId.Value, out var targetIc))
        {
            return visibleCadPads;
        }

        var sameIcCadPads = visibleCadPads
            .Where(pad => NotchAllocationService.HasQ7PositiveAllocationInIc(pad, _grid, targetIc))
            .ToList();
        if (sameIcCadPads.Count == 0)
        {
            return visibleCadPads;
        }

        if (!sameIcCadPads.Any(pad => pad.Id == targetCadPadId.Value))
        {
            var targetCad = visibleCadPads.FirstOrDefault(pad => pad.Id == targetCadPadId.Value);
            if (targetCad is not null)
            {
                sameIcCadPads.Add(targetCad);
                sameIcCadPads.Sort(static (left, right) => left.Id.CompareTo(right.Id));
            }
        }

        return sameIcCadPads;
    }

    private static ulong ComputeCadPoolFingerprint(IReadOnlyList<CadPad> cadPads)
    {
        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)cadPads.Count;
            foreach (var cadPad in cadPads)
            {
                hash = (hash * 1099511628211UL) ^ (uint)cadPad.Id;
            }

            return hash;
        }
    }

    private HashSet<int>? GetActiveRegularPadIdsForNotchComputation()
    {
        if (_latestPadMatchResult.RegularToCad.Count == 0)
        {
            return null;
        }

        return _latestPadMatchResult.RegularToCad.Keys.ToHashSet();
    }

    private bool TryResolveNotchComputationInputs(
        int cadPadId,
        out CadPad cad,
        out IReadOnlyList<CadPad> allCadPads,
        out IReadOnlySet<int>? activeRegularPadIds)
    {
        cad = null!;
        allCadPads = Array.Empty<CadPad>();
        activeRegularPadIds = null;

        if (_grid is null)
        {
            return false;
        }

        allCadPads = GetCadPadsForNotchComputation(cadPadId);
        var sourceCadPads = ResolveCadPadsForComputationSource();
        var targetCad = allCadPads.FirstOrDefault(p => p.Id == cadPadId)
            ?? sourceCadPads.FirstOrDefault(p => p.Id == cadPadId);
        if (targetCad is null)
        {
            return false;
        }

        cad = targetCad;
        activeRegularPadIds = GetActiveRegularPadIdsForNotchComputation();
        return true;
    }
}
