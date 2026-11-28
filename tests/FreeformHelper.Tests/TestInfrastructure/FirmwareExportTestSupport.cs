// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class FirmwareExportTestSupport
{
    // Projection/parity fixtures sample sparse diffs from a full UINT16 firmware matrix.
    // Bounds tests call the exporter directly with their actual small grid.
    internal static string ExportSparseFirmwareSample(
        NotchTable table, CadPadSet cad, RegularGrid grid, ProjectSettings settings,
        NotchExportProfile profile = NotchExportProfile.Release,
        IReadOnlySet<int>? activeRegularPadIds = null)
    {
        var capacityGrid = new RegularGrid(ushort.MaxValue + 1, grid.Cols, grid.XEdges, grid.YEdges, grid.Pads);
        return NotchTableExporter.ExportAsCInitializer(table, cad, capacityGrid, settings, profile, activeRegularPadIds);
    }
}
