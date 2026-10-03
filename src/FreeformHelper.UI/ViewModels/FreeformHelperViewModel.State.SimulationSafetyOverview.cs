using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    [ObservableProperty]
    private bool _hasSimulationSafetyOverviewAudit;

    [ObservableProperty]
    private bool _hasSimulationSafetyOverviewRisk;

    [ObservableProperty]
    private bool _hasSimulationSafetyOverviewAttention;

    [ObservableProperty]
    private string _simulationSafetyOverviewStatusText = "Simulation not run";

    [ObservableProperty]
    private string _simulationSafetyOverviewEmsCapText = "480";

    [ObservableProperty]
    private string _simulationSafetyOverviewMaxAfterText = "Max After -";

    [ObservableProperty]
    private string _simulationSafetyOverviewViolationCountText = "-";

    [ObservableProperty]
    private string _simulationSafetyOverviewSummaryText = "Run Simulation to audit current notch output before FW handoff.";

    [ObservableProperty]
    private string _simulationSafetyOverviewHighRiskDiffsText = "Top risk diffs: -";

    public void ApplySimulationSafetyOverview(
        SimulationSafetyAuditResult? audit,
        bool isStale,
        string? buildFailureText)
    {
        ApplySimulationSafetyOverview(SimulationSafetyOverviewProjector.Project(audit, isStale, buildFailureText));
    }

    private void ApplySimulationSafetyOverview(SimulationSafetyOverviewProjection projection)
    {
        HasSimulationSafetyOverviewAudit = projection.HasAudit;
        HasSimulationSafetyOverviewRisk = projection.HasRisk;
        HasSimulationSafetyOverviewAttention = projection.NeedsAttention;
        SimulationSafetyOverviewStatusText = projection.StatusText;
        SimulationSafetyOverviewEmsCapText = projection.EmsCapText;
        SimulationSafetyOverviewMaxAfterText = projection.MaxAfterText;
        SimulationSafetyOverviewViolationCountText = projection.ViolationCountText;
        SimulationSafetyOverviewSummaryText = projection.SummaryText;
        SimulationSafetyOverviewHighRiskDiffsText = projection.HighRiskDiffsText;
    }

    partial void OnSimulationSafetyOverviewEmsCapTextChanged(string value)
    {
        OnPropertyChanged(nameof(NotchEmsSafetyPolicySummary));
        OnPropertyChanged(nameof(NotchEmsSafetyShortText));
        OnPropertyChanged(nameof(NotchTargetCoverageCapHelpText));
        OnPropertyChanged(nameof(NotchExportSafetyPolicySummary));
    }
}
