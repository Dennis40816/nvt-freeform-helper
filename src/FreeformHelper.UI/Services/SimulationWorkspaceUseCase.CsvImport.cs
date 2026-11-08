// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

public sealed partial class SimulationWorkspaceUseCase
{
    public bool TryImportCsvSources(
        IReadOnlyList<string> sourcePaths,
        RegularGrid grid,
        out NotchApplySimulationImportedDataset dataset,
        out string diagnostic)
    {
        try
        {
            dataset = ImportCsvSources(sourcePaths, grid);
            diagnostic = string.Empty;
            return true;
        }
        catch (InvalidDataException ex)
        {
            dataset = NotchApplySimulationImportedDataset.Empty;
            diagnostic = ex.Message;
            return false;
        }
    }
}
