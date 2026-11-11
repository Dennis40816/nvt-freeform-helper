// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>Projects a grid's simulation membership through the accepted regular mask.</summary>
public static class RegularVisibilityMaskProjection
{
    /// <summary>Returns all effective grid IDs, intersected with the mask when it is active.</summary>
    public static HashSet<int> BuildActivePadIds(
        RegularGrid effectiveGrid,
        IReadOnlySet<int>? activeRegularVisibilityMaskPadIds)
    {
        ArgumentNullException.ThrowIfNull(effectiveGrid);

        var activeRegularPadIds = effectiveGrid.Pads
            .Select(static pad => pad.RegularPadId)
            .ToHashSet();
        if (activeRegularVisibilityMaskPadIds is null)
        {
            return activeRegularPadIds;
        }

        activeRegularPadIds.IntersectWith(activeRegularVisibilityMaskPadIds);
        return activeRegularPadIds;
    }
}
