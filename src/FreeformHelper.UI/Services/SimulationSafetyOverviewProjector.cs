using System.Globalization;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Services;

public sealed record SimulationSafetyOverviewProjection(
    bool HasAudit,
    bool IsStale,
    bool HasRisk,
    bool NeedsAttention,
    string StatusText,
    string EmsCapText,
    string MaxAfterText,
    string ViolationCountText,
    string SummaryText,
    string HighRiskDiffsText)
{
    /// <summary>
    /// The summary text while no Simulation audit exists and no build failure is known.
    /// </summary>
    public const string NotRunSummaryText = "Run Simulation to audit current notch output before FW handoff.";

    /// <summary>
    /// The high risk diff text while no diff is listed.
    /// </summary>
    public const string NoHighRiskDiffsText = "Top risk diffs: -";

    /// <summary>
    /// Gets the overview that the view model shows before the first Simulation result.
    /// </summary>
    public static SimulationSafetyOverviewProjection Initial { get; } = new(
        HasAudit: false,
        IsStale: false,
        HasRisk: false,
        NeedsAttention: false,
        StatusText: "Simulation not run",
        EmsCapText: SimulationSafetyTextProjector.DefaultEmsAfterCapText,
        MaxAfterText: "Max After -",
        ViolationCountText: "-",
        SummaryText: NotRunSummaryText,
        HighRiskDiffsText: NoHighRiskDiffsText);
}

public static class SimulationSafetyOverviewProjector
{
    public static SimulationSafetyOverviewProjection Project(
        SimulationSafetyAuditResult? audit,
        bool isStale,
        string? buildFailureText)
    {
        if (audit is not { HasCells: true })
        {
            return ProjectMissingAudit(audit, buildFailureText);
        }

        var status = SimulationSafetyTextProjector.BuildStatusText(audit);
        if (isStale)
        {
            status += " (stale)";
        }

        return new SimulationSafetyOverviewProjection(
            HasAudit: true,
            IsStale: isStale,
            HasRisk: audit.HasViolations,
            NeedsAttention: audit.HasViolations || audit.HasPhysicalAuditRisks || isStale,
            StatusText: status,
            EmsCapText: SimulationSafetyTextProjector.FormatValue(audit.AfterCap),
            MaxAfterText: SimulationSafetyTextProjector.FormatValue(audit.MaxAfterValue),
            ViolationCountText: audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture),
            SummaryText: BuildSummary(audit, isStale),
            HighRiskDiffsText: BuildHighRiskText(audit));
    }

    private static SimulationSafetyOverviewProjection ProjectMissingAudit(
        SimulationSafetyAuditResult? audit,
        string? buildFailureText)
    {
        var hasFailure = !string.IsNullOrWhiteSpace(buildFailureText);
        return new SimulationSafetyOverviewProjection(
            HasAudit: false,
            IsStale: false,
            HasRisk: false,
            NeedsAttention: hasFailure,
            StatusText: hasFailure
                ? "Simulation unavailable"
                : SimulationSafetyTextProjector.BuildStatusText(audit: null),
            EmsCapText: SimulationSafetyTextProjector.FormatEmsAfterCap(audit),
            MaxAfterText: "-",
            ViolationCountText: "-",
            SummaryText: hasFailure
                ? buildFailureText!.Trim()
                : SimulationSafetyOverviewProjection.NotRunSummaryText,
            HighRiskDiffsText: SimulationSafetyOverviewProjection.NoHighRiskDiffsText);
    }

    private static string BuildSummary(SimulationSafetyAuditResult audit, bool isStale)
    {
        if (isStale)
        {
            return "Workspace changed after this Simulation result. Refresh Simulation before export.";
        }

        var maxLocation = audit.MaxAfterRegularPadId.HasValue
            ? $" at REG {audit.MaxAfterRegularPadId.Value.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        var summary = audit.HasViolations
            ? $"{audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture)} pad(s) exceed EMS cap. Max After {SimulationSafetyTextProjector.FormatValue(audit.MaxAfterValue)}{maxLocation}."
            : $"Current Simulation is under EMS cap. Max After {SimulationSafetyTextProjector.FormatValue(audit.MaxAfterValue)}.";
        return audit.HasPhysicalAuditRisks
            ? $"{summary} {SimulationSafetyTextProjector.BuildPhysicalAuditSummaryText(audit)}"
            : summary;
    }

    private static string BuildHighRiskText(SimulationSafetyAuditResult audit)
    {
        if (!audit.HasHighRiskDiffs)
        {
            return SimulationSafetyOverviewProjection.NoHighRiskDiffsText;
        }

        var preview = audit.HighRiskDiffs
            .Take(2)
            .Select(item =>
            {
                var isViolation = SimulationSafetyAuditService.IsEmsAfterCapViolation(
                    item.AfterValue,
                    audit.AfterCap);
                var distance = isViolation ? -item.MarginToCap : Math.Max(0d, item.MarginToCap);
                var marginText = isViolation
                    ? $"+{SimulationSafetyTextProjector.FormatValue(distance)}"
                    : $"{SimulationSafetyTextProjector.FormatValue(distance)}m";
                return $"REG {item.RegularPadId}/D{item.DiffIndex} {SimulationSafetyTextProjector.FormatValue(item.AfterValue)} ({marginText})";
            });
        return (audit.HasViolations ? "Risk: " : "Closest: ") + string.Join("; ", preview);
    }
}
