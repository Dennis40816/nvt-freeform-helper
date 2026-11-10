// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal delegate bool TryGetCadIcIndexDelegate(int cadPadId, out int icIndex);

internal static class NotchComputationInputs
{
    internal static List<CadPad> SelectCadPadsForComputation(
        IReadOnlyList<CadPad> sourceCadPads,
        Func<int, bool> isEffectivelyHidden,
        RegularGrid? grid,
        int? targetCadPadId,
        TryGetCadIcIndexDelegate tryGetCadIcIndex)
    {
        var visibleCadPads = sourceCadPads
            .Where(pad => !isEffectivelyHidden(pad.Id))
            .OrderBy(static pad => pad.Id)
            .ToList();
        if (visibleCadPads.Count == 0)
        {
            return visibleCadPads;
        }

        if (grid is null ||
            !targetCadPadId.HasValue ||
            !tryGetCadIcIndex(targetCadPadId.Value, out var targetIc))
        {
            return visibleCadPads;
        }

        var sameIcCadPads = visibleCadPads
            .Where(pad => NotchAllocationService.HasQ7PositiveAllocationInIc(pad, grid, targetIc))
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
}
