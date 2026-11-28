// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfIcResolver
{
    public static int Resolve(RegularGrid? grid, GridSettings gridSettings, CadPad pad)
    {
        if (grid is null || grid.Cols <= 0 || grid.XEdges.Count < 2)
        {
            return 0;
        }

        var (_, col) = DxfIndexAssigner.ResolveGridCell(grid, pad.Centroid);
        var perIcCols = GridIcChannelAllocationService.ResolvePerIcColumns(gridSettings, grid.Cols);
        return GridIcChannelAllocationService.ResolveIcIndexForColumn(col, perIcCols);
    }
}
