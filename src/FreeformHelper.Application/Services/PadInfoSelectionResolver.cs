// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Resolves the pads displayed in the pad info popover.
/// </summary>
public static class PadInfoSelectionResolver
{
    /// <summary>
    /// Returns the clicked CAD pad or the selected source pads in source order.
    /// </summary>
    public static List<CadPad> Resolve(
        IEnumerable<CadPad> cadPads,
        IReadOnlyCollection<int> selectedCadPadIds,
        CadPad clickedPad)
    {
        if (selectedCadPadIds.Count <= 1 || !selectedCadPadIds.Contains(clickedPad.Id))
        {
            return new List<CadPad> { clickedPad };
        }

        return cadPads.Where(pad => selectedCadPadIds.Contains(pad.Id)).ToList();
    }

    /// <summary>
    /// Returns the clicked regular pad or the selected source pads in source order.
    /// </summary>
    public static List<RegularPad> Resolve(
        IEnumerable<RegularPad> regularPads,
        IReadOnlyCollection<int> selectedRegularPadIndices,
        RegularPad clickedPad)
    {
        if (selectedRegularPadIndices.Count <= 1 || !selectedRegularPadIndices.Contains(clickedPad.Index))
        {
            return new List<RegularPad> { clickedPad };
        }

        return regularPads.Where(pad => selectedRegularPadIndices.Contains(pad.Index)).ToList();
    }
}
