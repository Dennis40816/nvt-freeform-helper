using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    private int[] _diffViolationRegularPadIds = Array.Empty<int>();
    private SimulationSafetyAuditResult _simulationSafetyAudit = SimulationSafetyAuditResult.Empty(
        SimulationSafetyAuditService.DefaultEmsAfterCap);

    public int DiffViolationPadCount => _diffViolationRegularPadIds.Length;
    public bool HasDiffViolationPads => DiffViolationPadCount > 0;
    public string DiffViolationSummaryText => HasDiffViolationPads
        ? $"Potential mismatch pads: {DiffViolationPadCount.ToString(CultureInfo.InvariantCulture)}."
        : "No mismatch pads detected between notch expected delta and simulation delta.";
    public string DiffViolationPadListText
    {
        get
        {
            if (!HasDiffViolationPads)
            {
                return "REG -";
            }

            const int maxDisplayPadCount = 24;
            var ids = _diffViolationRegularPadIds;
            var preview = string.Join(
                ", ",
                ids.Take(maxDisplayPadCount)
                    .Select(static regularPadId => $"REG {regularPadId}"));
            if (ids.Length > maxDisplayPadCount)
            {
                return $"{preview}, ... (+{(ids.Length - maxDisplayPadCount).ToString(CultureInfo.InvariantCulture)})";
            }

            return preview;
        }
    }
    public double SimulationEmsAfterCap => _simulationSafetyAudit.AfterCap;
    public int SimulationSafetyViolationCount => _simulationSafetyAudit.TotalViolationCount;
    public bool HasSimulationSafetyViolations => _simulationSafetyAudit.HasViolations;
    public bool HasSimulationHighRiskDiffs => _simulationSafetyAudit.HasHighRiskDiffs;
    public bool HasSimulationPhysicalAuditRisks => _simulationSafetyAudit.HasPhysicalAuditRisks;
    public int SimulationNetFlowResidualCount => _simulationSafetyAudit.TotalNetFlowResidualCount;
    public int SimulationTargetCoverageRiskCount => _simulationSafetyAudit.TotalTargetCoverageRiskCount;
    public string SimulationNetFlowResidualCountText =>
        $"Net-flow {SimulationNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)}";
    public string SimulationTargetCoverageRiskCountText =>
        $"Coverage {SimulationTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)}";
    public string SimulationSafetyStatusText =>
        SimulationSafetyTextProjector.BuildStatusText(_simulationSafetyAudit);
    public string SimulationEmsCapText => SimulationSafetyTextProjector.BuildWorkspaceEmsCapText(SimulationEmsAfterCap);
    public string SimulationMaxAfterText => _simulationSafetyAudit.HasCells
        ? $"Max After {FormatSafetyValue(_simulationSafetyAudit.MaxAfterValue)}"
        : "Max After -";
    public string SimulationSafetySummaryText =>
        SimulationSafetyTextProjector.BuildWorkspaceSummaryText(_simulationSafetyAudit);

    public string SimulationSafetyViolationCountText =>
        $"Violations {SimulationSafetyViolationCount.ToString(CultureInfo.InvariantCulture)}";
    public string SimulationSafetyViolationBadgeText => FormatCountBadge(SimulationSafetyViolationCount);

    public string SimulationPhysicalAuditSummaryText =>
        SimulationSafetyTextProjector.BuildPhysicalAuditSummaryText(_simulationSafetyAudit);

    public string SimulationHighRiskDiffListText =>
        SimulationSafetyTextProjector.BuildHighRiskDiffListText(_simulationSafetyAudit, "No simulation cells.");

    public string SimulationSafetyViolationListText =>
        SimulationSafetyTextProjector.BuildViolationListText(_simulationSafetyAudit);

    public string SelectedEmsStatusText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return "-";
            }

            return FormatCellEmsStatus(cell);
        }
    }

    public string SelectedNetFlowText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return "-";
            }

            var flow = BuildSelectedDiffFlow(cell);
            return $"NetFlow {FormatSignedSafetyValue(flow.NetFlow)} · source {FormatSignedSafetyValue(flow.SourceDelta)} · target {FormatSignedSafetyValue(flow.TargetDelta)}";
        }
    }

    public IReadOnlyList<SimulationSelectedFlowLegViewModel> SelectedFlowLegs
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return Array.Empty<SimulationSelectedFlowLegViewModel>();
            }

            return BuildSelectedDiffFlow(cell).Legs;
        }
    }

    public string SelectedFlowLegSummaryText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return "Select a regular pad to inspect source/target legs.";
            }

            var flow = BuildSelectedDiffFlow(cell);
            if (flow.Lines.Count == 0)
            {
                return "No notch source/target leg touches this selected FW diff.";
            }

            return $"{flow.Lines.Count.ToString(CultureInfo.InvariantCulture)} source/target leg(s) touch this selected FW diff.";
        }
    }

    public bool HasSelectedFlowLegs
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return false;
            }

            return BuildSelectedDiffFlow(cell).Lines.Count > 0;
        }
    }

    internal SimulationSafetyAuditResult GetSimulationSafetyAuditSnapshot()
    {
        return _simulationSafetyAudit;
    }

    private void RefreshDiffViolationState()
    {
        if (_snapshot is null)
        {
            _diffViolationRegularPadIds = Array.Empty<int>();
            NotifyDiffViolationChanged();
            return;
        }

        var violations = SimulationDeltaViolationService.Detect(
            _snapshot.Result.Cells,
            _snapshot.Result.Actions,
            SelectedAreaOption.IcIndex);
        _diffViolationRegularPadIds = violations
            .Select(static item => item.RegularPadId)
            .Distinct()
            .OrderBy(static regularPadId => regularPadId)
            .ToArray();
        NotifyDiffViolationChanged();
    }

    private void RefreshSimulationSafetyState()
    {
        _simulationSafetyAudit = _snapshot is null
            ? SimulationSafetyAuditResult.Empty(SimulationSafetyAuditService.DefaultEmsAfterCap)
            : _snapshot.SafetyAudit;
        RebuildSimulationHighRiskDiffs();
        NotifySimulationSafetyChanged();
    }

    private void RebuildSimulationHighRiskDiffs()
    {
        _simulationHighRiskDiffs.Clear();
        foreach (var item in _simulationSafetyAudit.HighRiskDiffs)
        {
            var isViolation = SimulationSafetyAuditService.IsEmsAfterCapViolation(
                item.AfterValue,
                _simulationSafetyAudit.AfterCap);
            _simulationHighRiskDiffs.Add(new SimulationSafetyRiskDiffViewModel(
                item.Rank,
                item.RegularPadId,
                item.IcIndex,
                item.DiffIndex,
                $"REG {item.RegularPadId.ToString(CultureInfo.InvariantCulture)} · IC {item.IcIndex + 1} · FW Diff {item.DiffIndex.ToString(CultureInfo.InvariantCulture)}",
                $"After {FormatSafetyValue(item.AfterValue)}",
                $"Δ {FormatSignedSafetyValue(item.DeltaValue)}",
                SimulationSafetyTextProjector.FormatRiskMarginText(item, _simulationSafetyAudit.AfterCap),
                SimulationSafetyTextProjector.BuildHighRiskStatusText(isViolation),
                isViolation));
        }
    }

    private void NotifyDiffViolationChanged()
    {
        OnPropertyChanged(nameof(DiffViolationPadCount));
        OnPropertyChanged(nameof(HasDiffViolationPads));
        OnPropertyChanged(nameof(DiffViolationSummaryText));
        OnPropertyChanged(nameof(DiffViolationPadListText));
    }

    private void NotifySimulationSafetyChanged()
    {
        OnPropertyChanged(nameof(SimulationEmsAfterCap));
        OnPropertyChanged(nameof(SimulationSafetyViolationCount));
        OnPropertyChanged(nameof(HasSimulationSafetyViolations));
        OnPropertyChanged(nameof(HasSimulationHighRiskDiffs));
        OnPropertyChanged(nameof(HasSimulationPhysicalAuditRisks));
        OnPropertyChanged(nameof(SimulationNetFlowResidualCount));
        OnPropertyChanged(nameof(SimulationTargetCoverageRiskCount));
        OnPropertyChanged(nameof(SimulationNetFlowResidualCountText));
        OnPropertyChanged(nameof(SimulationTargetCoverageRiskCountText));
        OnPropertyChanged(nameof(SimulationSafetyStatusText));
        OnPropertyChanged(nameof(SimulationEmsCapText));
        OnPropertyChanged(nameof(SimulationMaxAfterText));
        OnPropertyChanged(nameof(SimulationSafetySummaryText));
        OnPropertyChanged(nameof(SimulationSafetyViolationCountText));
        OnPropertyChanged(nameof(SimulationSafetyViolationBadgeText));
        OnPropertyChanged(nameof(SimulationPhysicalAuditSummaryText));
        OnPropertyChanged(nameof(SimulationHighRiskDiffListText));
        OnPropertyChanged(nameof(SimulationSafetyViolationListText));
        OnPropertyChanged(nameof(SimulationHighRiskDiffs));
        OnPropertyChanged(nameof(SelectedEmsStatusText));
        OnPropertyChanged(nameof(SelectedNetFlowText));
        OnPropertyChanged(nameof(SelectedFlowLegSummaryText));
        OnPropertyChanged(nameof(SelectedFlowLegs));
        OnPropertyChanged(nameof(HasSelectedFlowLegs));
    }

    private static string FormatSafetyValue(double value)
    {
        return SimulationSafetyTextProjector.FormatValue(value);
    }

    internal static string FormatCountBadge(int count) => SimulationSafetyTextProjector.FormatCountBadge(count);

    private string FormatCellEmsStatus(NotchApplySimulationDiffCell cell)
    {
        return SimulationSafetyTextProjector.FormatCellEmsStatus(cell, SimulationEmsAfterCap);
    }

    private SelectedDiffFlow BuildSelectedDiffFlow(NotchApplySimulationDiffCell cell)
    {
        if (_snapshot is null)
        {
            return SelectedDiffFlow.Empty;
        }

        var sourceDelta = 0d;
        var targetDelta = 0d;
        var lines = new List<string>();
        var legs = new List<SimulationSelectedFlowLegViewModel>();
        foreach (var action in _snapshot.Result.Actions)
        {
            if (action.IcIndex != cell.IcIndex)
            {
                continue;
            }

            var sourceRowsText = BuildSourceRowsText(action.SourceRowNumbers);
            if (action.AnchorDiffIndex == cell.DiffIndex)
            {
                var delta = action.SourceAfterValue - action.SourceBeforeValue;
                sourceDelta += delta;
                lines.Add(
                    $"Source leg: anchor FW Diff {action.AnchorDiffIndex} retain {action.SourceRetainedPercent}% => {FormatSignedSafetyValue(delta)} · {sourceRowsText}");
                legs.Add(new SimulationSelectedFlowLegViewModel(
                    "Source retain",
                    $"FW Diff {action.AnchorDiffIndex} keeps {action.SourceRetainedPercent}%",
                    $"Combine {action.CombinePercent}% · CAD {FormatCadPadId(action.CadPadId)}",
                    FormatSignedSafetyValue(delta),
                    sourceRowsText));
            }

            foreach (var leg in action.Legs)
            {
                if (leg.TargetDiffIndex != cell.DiffIndex)
                {
                    continue;
                }

                targetDelta += leg.DeltaValue;
                lines.Add(
                    $"Target leg: from FW Diff {action.AnchorDiffIndex} ratio {leg.RatioPercent}% => {FormatSignedSafetyValue(leg.DeltaValue)} · {sourceRowsText}");
                legs.Add(new SimulationSelectedFlowLegViewModel(
                    "Target receive",
                    $"From FW Diff {action.AnchorDiffIndex} at {leg.RatioPercent}%",
                    $"Anchor REG {action.RegularPadId} · CAD {FormatCadPadId(action.CadPadId)}",
                    FormatSignedSafetyValue(leg.DeltaValue),
                    sourceRowsText));
            }
        }

        return new SelectedDiffFlow(sourceDelta, targetDelta, sourceDelta + targetDelta, lines, legs);
    }

    private static string FormatSignedSafetyValue(double value)
    {
        return SimulationSafetyTextProjector.FormatSignedValue(value);
    }

    private void SelectSimulationRiskDiff(SimulationSafetyRiskDiffViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targetArea = AreaOptions.FirstOrDefault(option => option.IcIndex == item.IcIndex);
        if (!targetArea.Equals(default(NotchApplySimulationAreaOption))
            && !targetArea.Equals(SelectedAreaOption))
        {
            SelectedAreaOption = targetArea;
        }

        SelectRegularPad(item.RegularPadId);
        StatusText = $"Focused {item.LocationText} from Simulation safety risk list.";
    }

    private static string FormatCadPadId(int? cadPadId)
    {
        return cadPadId.HasValue
            ? cadPadId.Value.ToString(CultureInfo.InvariantCulture)
            : "-";
    }

    private sealed record SelectedDiffFlow(
        double SourceDelta,
        double TargetDelta,
        double NetFlow,
        IReadOnlyList<string> Lines,
        IReadOnlyList<SimulationSelectedFlowLegViewModel> Legs)
    {
        public static SelectedDiffFlow Empty { get; } = new(
            0d,
            0d,
            0d,
            Array.Empty<string>(),
            Array.Empty<SimulationSelectedFlowLegViewModel>());
    }
}
