// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Builds a Notch Detail the way the open command does: the detail first, then its explanation.</summary>
internal static class NotchDetailTestBuilder
{
    internal static NotchDetailViewModel Build(CadPad selectedCad, int? dxfIndex, CadPadSet cad, RegularGrid grid,
        ProjectSettings settings, IReadOnlySet<int>? activeRegularPadIds,
        IReadOnlyDictionary<int, int> cadOutputFwDiffByCadId, bool firmwareOutput = true)
    {
        var detail = NotchDetailUseCase.Build(selectedCad, dxfIndex, grid, settings, cad.Pads);
        detail.ShowExplanation(NotchDetailUseCase.BuildExplanation(selectedCad, cad, grid, settings,
            activeRegularPadIds, cadOutputFwDiffByCadId, firmwareOutput));
        return detail;
    }
}
