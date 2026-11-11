// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    private SimulationCanvasOwner<SimulationOverlayProjection> CreateCanvasOwner() => new(
        _sharedCanvasViewOptions, _sharedCanvasColorOptions, BuildAreaOptions(_session.FwDiffGrid.Pads),
        new SimulationOverlayProjection(Array.Empty<NotchApplySimulationAaDisplayCell>(),
            new Dictionary<int, NotchApplySimulationDiffCell>(), default),
        SimulationOverlayProjectionBuilder.Build,
        static projection => projection.CellsByRegularPadId,
        static projection => projection.OverlayItems,
        static projection => (projection.LegendRange.Minimum, projection.LegendRange.Maximum,
            projection.LegendRange.MaximumAbsDelta, projection.LegendRange.AutoNegativeClampAbs,
            projection.LegendRange.AutoPositiveClamp),
        () => _snapshot?.SimulationResult, () => _selection.EnsureSelectedRegularWithinArea(),
        () => _selection.RefreshDiffViolationState(), RefreshSelectionPresentation, RefreshCommandState,
        name => OnPropertyChanging(name), name => OnPropertyChanged(name));
}
