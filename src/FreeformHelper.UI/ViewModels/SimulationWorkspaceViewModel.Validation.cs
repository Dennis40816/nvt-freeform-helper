// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    private SimulationSelectionOwner CreateSelectionOwner() => new(
        _session.FwDiffGrid.Pads.ToDictionary(pad => pad.RegularPadId),
        BuildCadOutputFwDiffByRegularPadId(_session), BuildRegularPadIdsByIcDiff(_session),
        _session.ActiveRegularPadIds, AreaOptions, () => _snapshot?.SimulationResult,
        () => _canvas.CellsByRegularPadId, () => SelectedAreaOption, value => SelectedAreaOption = value,
        () => SelectedInputSourceOption.Value, () => HasCsvData, () => HasSelectedOverride,
        () => _manualOverridesByRegularPadId, () => GlobalValue, DefaultGlobalValue,
        RefreshCommandState, value => StatusText = value,
        static (cells, actions, area) => SimulationDeltaViolationService.Detect(cells, actions, area)
            .Select(static item => item.RegularPadId),
        ProjectSimulationRiskDiff, SimulationSafetyTextProjector.BuildStatusText,
        SimulationSafetyTextProjector.BuildWorkspaceEmsCapText, SimulationSafetyTextProjector.BuildWorkspaceSummaryText,
        SimulationSafetyTextProjector.BuildPhysicalAuditSummaryText,
        static audit => SimulationSafetyTextProjector.BuildHighRiskDiffListText(audit, "No simulation cells."),
        SimulationSafetyTextProjector.BuildViolationListText, SimulationSafetyTextProjector.FormatValue,
        SimulationSafetyTextProjector.FormatSignedValue, SimulationSafetyTextProjector.FormatCountBadge,
        SimulationSafetyTextProjector.FormatCellEmsStatus,
        name => OnPropertyChanging(name), name => OnPropertyChanged(name));

    private static SimulationSafetyRiskDiffViewModel ProjectSimulationRiskDiff(SimulationSafetyHighRiskDiff item, double afterCap)
    {
        var isViolation = SimulationSafetyAuditService.IsEmsAfterCapViolation(item.AfterValue, afterCap);
        return new SimulationSafetyRiskDiffViewModel(
            item.Rank,
            item.RegularPadId,
            item.IcIndex,
            item.DiffIndex,
            $"REG {item.RegularPadId.ToString(CultureInfo.InvariantCulture)} · IC {item.IcIndex + 1} · FW Diff {item.DiffIndex.ToString(CultureInfo.InvariantCulture)}",
            $"After {SimulationSafetyTextProjector.FormatValue(item.AfterValue)}",
            $"Δ {SimulationSafetyTextProjector.FormatSignedValue(item.DeltaValue)}",
            SimulationSafetyTextProjector.FormatRiskMarginText(item, afterCap),
            SimulationSafetyTextProjector.BuildHighRiskStatusText(isViolation),
            isViolation);
    }

    public int DiffViolationPadCount => _selection.DiffViolationPadCount;
    public bool HasDiffViolationPads => _selection.HasDiffViolationPads;
    public string DiffViolationSummaryText => _selection.DiffViolationSummaryText;
    public string DiffViolationPadListText => _selection.DiffViolationPadListText;
    public double SimulationEmsAfterCap => _selection.SimulationEmsAfterCap;
    public int SimulationSafetyViolationCount => _selection.SimulationSafetyViolationCount;
    public bool HasSimulationSafetyViolations => _selection.HasSimulationSafetyViolations;
    public bool HasSimulationHighRiskDiffs => _selection.HasSimulationHighRiskDiffs;
    public int SimulationNetFlowResidualCount => _selection.SimulationNetFlowResidualCount;
    public int SimulationTargetCoverageRiskCount => _selection.SimulationTargetCoverageRiskCount;
    public string SimulationNetFlowResidualCountText => _selection.SimulationNetFlowResidualCountText;
    public string SimulationTargetCoverageRiskCountText => _selection.SimulationTargetCoverageRiskCountText;
    public string SimulationSafetyStatusText => _selection.SimulationSafetyStatusText;
    public string SimulationEmsCapText => _selection.SimulationEmsCapText;
    public string SimulationMaxAfterText => _selection.SimulationMaxAfterText;
    public string SimulationSafetySummaryText => _selection.SimulationSafetySummaryText;
    public string SimulationSafetyViolationCountText => _selection.SimulationSafetyViolationCountText;
    public string SimulationSafetyViolationBadgeText => _selection.SimulationSafetyViolationBadgeText;
    public string SimulationPhysicalAuditSummaryText => _selection.SimulationPhysicalAuditSummaryText;
    public string SimulationHighRiskDiffListText => _selection.SimulationHighRiskDiffListText;
    public string SimulationSafetyViolationListText => _selection.SimulationSafetyViolationListText;
    public string SelectedEmsStatusText => _selection.SelectedEmsStatusText;
    public string SelectedNetFlowText => _selection.SelectedNetFlowText;
    public IReadOnlyList<SimulationSelectedFlowLegViewModel> SelectedFlowLegs => _selection.SelectedFlowLegs;
    public string SelectedFlowLegSummaryText => _selection.SelectedFlowLegSummaryText;
    public bool HasSelectedFlowLegs => _selection.HasSelectedFlowLegs;

    internal SimulationSafetyAuditResult GetSimulationSafetyAuditSnapshot() => _selection.GetSimulationSafetyAuditSnapshot();

    internal static string FormatCountBadge(int count) => SimulationSafetyTextProjector.FormatCountBadge(count);
}
