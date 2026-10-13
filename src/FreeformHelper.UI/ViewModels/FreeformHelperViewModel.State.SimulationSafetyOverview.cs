using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Gets or sets the latest Simulation safety overview. The view model replaces the whole
    /// value. The properties below forward to it and raise their change notification when their
    /// own value changed.
    /// </summary>
    [ObservableProperty]
    private SimulationSafetyOverviewProjection _simulationSafetyOverview = SimulationSafetyOverviewProjection.Initial;

    public bool HasSimulationSafetyOverviewAudit => SimulationSafetyOverview.HasAudit;

    public bool HasSimulationSafetyOverviewRisk => SimulationSafetyOverview.HasRisk;

    public bool HasSimulationSafetyOverviewAttention => SimulationSafetyOverview.NeedsAttention;

    public string SimulationSafetyOverviewStatusText => SimulationSafetyOverview.StatusText;

    public string SimulationSafetyOverviewEmsCapText => SimulationSafetyOverview.EmsCapText;

    public string SimulationSafetyOverviewMaxAfterText => SimulationSafetyOverview.MaxAfterText;

    public string SimulationSafetyOverviewViolationCountText => SimulationSafetyOverview.ViolationCountText;

    public string SimulationSafetyOverviewSummaryText => SimulationSafetyOverview.SummaryText;

    public string SimulationSafetyOverviewHighRiskDiffsText => SimulationSafetyOverview.HighRiskDiffsText;

    public void ApplySimulationSafetyOverview(
        SimulationSafetyAuditResult? audit,
        bool isStale,
        string? buildFailureText)
    {
        ApplySimulationSafetyOverview(SimulationSafetyOverviewProjector.Project(audit, isStale, buildFailureText));
    }

    private void ApplySimulationSafetyOverview(SimulationSafetyOverviewProjection projection)
    {
        SimulationSafetyOverview = projection;
    }

    partial void OnSimulationSafetyOverviewChanged(
        SimulationSafetyOverviewProjection? oldValue,
        SimulationSafetyOverviewProjection newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.HasAudit, newValue.HasAudit, nameof(HasSimulationSafetyOverviewAudit));
        RaiseWhenChanged(oldValue.HasRisk, newValue.HasRisk, nameof(HasSimulationSafetyOverviewRisk));
        RaiseWhenChanged(oldValue.NeedsAttention, newValue.NeedsAttention, nameof(HasSimulationSafetyOverviewAttention));
        RaiseWhenChanged(oldValue.StatusText, newValue.StatusText, nameof(SimulationSafetyOverviewStatusText));
        if (!string.Equals(oldValue.EmsCapText, newValue.EmsCapText, StringComparison.Ordinal))
        {
            // The cap guidance texts depend on the cap. They are raised before the cap text, as the
            // stored property's changed hook did.
            OnPropertyChanged(nameof(NotchEmsSafetyPolicySummary));
            OnPropertyChanged(nameof(NotchEmsSafetyShortText));
            OnPropertyChanged(nameof(NotchTargetCoverageCapHelpText));
            OnPropertyChanged(nameof(NotchExportSafetyPolicySummary));
            OnPropertyChanged(nameof(SimulationSafetyOverviewEmsCapText));
        }

        RaiseWhenChanged(oldValue.MaxAfterText, newValue.MaxAfterText, nameof(SimulationSafetyOverviewMaxAfterText));
        RaiseWhenChanged(oldValue.ViolationCountText, newValue.ViolationCountText, nameof(SimulationSafetyOverviewViolationCountText));
        RaiseWhenChanged(oldValue.SummaryText, newValue.SummaryText, nameof(SimulationSafetyOverviewSummaryText));
        RaiseWhenChanged(oldValue.HighRiskDiffsText, newValue.HighRiskDiffsText, nameof(SimulationSafetyOverviewHighRiskDiffsText));
    }
}
