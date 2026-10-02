using System.Globalization;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Services;

public static class SimulationSafetyTextProjector
{
    public static string FormatValue(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static string FormatEmsAfterCap(SimulationSafetyAuditResult? audit) =>
        FormatValue(audit?.AfterCap ?? SimulationSafetyAuditService.DefaultEmsAfterCap);

    internal static string DefaultEmsAfterCapText => FormatEmsAfterCap(audit: null);

    internal static string BuildNotchEmsSafetyPolicySummary(string capText) =>
        $"Simulation safety: After > {capText} is EMS risk. 400 is diagnostic input, not a correctness target.";

    internal static string BuildNotchEmsSafetyShortText(string capText) => $"EMS cap {capText}";

    internal static string BuildNotchTargetCoverageCapHelpText(
        decimal targetCoverageCapPercent,
        string emsCapText)
    {
        var targetCapText = targetCoverageCapPercent.ToString("0.#", CultureInfo.InvariantCulture);
        var mappedAfterText = (targetCoverageCapPercent * 4m).ToString("0.###", CultureInfo.InvariantCulture);
        return $"At uniform 400, target cap {targetCapText}% maps to After {mappedAfterText}; " +
               $"compare with EMS cap {emsCapText}.";
    }

    internal static string BuildNotchTargetCoverageGuardSummary(bool enabled, decimal targetCoverageCapPercent) =>
        enabled
            ? $"Target guard: cap CurrentGain target coverage at {targetCoverageCapPercent:0.#}%."
            : "Target guard: OFF; target coverage can exceed EMS diagnostic cap.";

    internal static string BuildNotchExportHandoffChecklistText(bool hasExportType) =>
        hasExportType
            ? "Checklist: Simulation audit, EMS cap, selected rows, and C/runtime parity before FW handoff."
            : "Checklist unavailable until an export type is selected.";

    internal static string BuildNotchExportSafetyPolicySummary(string capText) =>
        $"Export safety: run Simulation audit before FW handoff; any After > {capText} needs explicit review.";

    public static string FormatSignedValue(double value) => value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);

    public static string BuildStatusText(SimulationSafetyAuditResult? audit)
    {
        if (audit is not { HasCells: true })
        {
            return "Simulation not run";
        }

        return BuildReplayStatusText(
            isSupported: true,
            hasEmsViolations: audit.HasViolations,
            hasPhysicalAuditRisks: audit.HasPhysicalAuditRisks);
    }

    public static string BuildReplayStatusText(
        bool isSupported,
        bool hasEmsViolations,
        bool hasPhysicalAuditRisks)
    {
        if (!isSupported)
        {
            return "unsupported";
        }

        if (hasEmsViolations)
        {
            return "EMS risk";
        }

        return hasPhysicalAuditRisks ? "audit warning" : "EMS OK";
    }

    public static string FormatCountBadge(int count) =>
        count > 99 ? "99+" : Math.Max(0, count).ToString(CultureInfo.InvariantCulture);

    public static string BuildWorkspaceSummaryText(SimulationSafetyAuditResult audit)
    {
        if (!audit.HasCells)
        {
            return "EMS safety: no simulation cells.";
        }

        var maxText = $"Max After {FormatValue(audit.MaxAfterValue)}";
        var capText = $"cap {FormatValue(audit.AfterCap)}";
        if (!audit.HasViolations)
        {
            return $"EMS OK: {maxText} <= {capText}.";
        }

        var location = audit.MaxAfterRegularPadId.HasValue
            ? $" at REG {audit.MaxAfterRegularPadId.Value.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"EMS risk: {audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture)} pad(s) exceed {capText}. {maxText}{location}.";
    }

    public static string BuildPhysicalAuditSummaryText(SimulationSafetyAuditResult audit)
    {
        if (!audit.HasCells)
        {
            return "Physical audit: no simulation cells.";
        }

        var globalText = audit.GlobalFlowAudit.IsBalanced
            ? "global flow residual OK"
            : $"global flow residual {FormatSignedValue(audit.GlobalFlowAudit.ResidualValue)}";
        var netFlowText = audit.TotalNetFlowResidualCount == 0
            ? "net-flow OK"
            : $"net-flow residuals {audit.TotalNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)}";
        var coverageText = audit.TotalTargetCoverageRiskCount == 0
            ? $"target coverage <= {FormatValue(audit.TargetCoveragePercentCap)}%"
            : $"target coverage risks {audit.TotalTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)}";
        return $"Physical audit: {globalText}; {netFlowText}; {coverageText}.";
    }

    public static string BuildHighRiskDiffListText(SimulationSafetyAuditResult audit, string emptyText)
    {
        return audit.HasHighRiskDiffs
            ? string.Join(
                Environment.NewLine,
                audit.HighRiskDiffs.Select(item => FormatHighRiskDiffLine(item, audit.AfterCap)))
            : emptyText;
    }

    public static string BuildViolationListText(SimulationSafetyAuditResult audit)
    {
        if (!audit.HasViolations)
        {
            return "REG -";
        }

        var preview = string.Join(
            ", ",
            audit.Violations.Select(static violation =>
                $"REG {violation.RegularPadId} After {FormatValue(violation.AfterValue)}"));
        var remaining = audit.TotalViolationCount - audit.Violations.Count;
        return remaining > 0
            ? $"{preview}, ... (+{remaining.ToString(CultureInfo.InvariantCulture)})"
            : preview;
    }

    public static string BuildExportBadgeText(SimulationSafetyAuditResult? audit)
    {
        if (audit is not { HasCells: true })
        {
            return "Simulation audit: not run";
        }

        if (audit.HasViolations)
        {
            return $"EMS risk {audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture)}";
        }

        return $"{BuildStatusText(audit)} · Max After {FormatValue(audit.MaxAfterValue)}";
    }

    public static string BuildExportSummaryText(SimulationSafetyAuditResult? audit)
    {
        var capText = FormatEmsAfterCap(audit);
        if (audit is not { HasCells: true })
        {
            return "Simulation audit is not available in the current workspace. " +
                   $"Open Simulation to review After <= {capText} before FW handoff.";
        }

        var maxLocation = audit.MaxAfterRegularPadId.HasValue
            ? $" at REG {audit.MaxAfterRegularPadId.Value.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        var maxText = $"Max After {FormatValue(audit.MaxAfterValue)}{maxLocation}";
        var summary = audit.HasViolations
            ? $"Blocked: {audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture)} pad(s) exceed EMS cap {capText}. {maxText}."
            : $"Safe for EMS cap {capText}: {maxText}.";
        return audit is { HasViolations: false, HasPhysicalAuditRisks: true }
            ? $"{summary} {BuildPhysicalAuditSummaryText(audit)}"
            : summary;
    }

    public static string BuildExportHighRiskText(SimulationSafetyAuditResult? audit)
    {
        return audit is { HasHighRiskDiffs: true }
            ? string.Join(
                Environment.NewLine,
                audit.HighRiskDiffs.Select(item => FormatHighRiskDiffLine(item, audit.AfterCap)))
            : "High-risk diffs: -";
    }

    public static string FormatCellEmsStatus(NotchApplySimulationDiffCell cell, double afterCap)
    {
        var isViolation = SimulationSafetyAuditService.IsEmsAfterCapViolation(cell.AfterValue, afterCap);
        var distance = isViolation
            ? cell.AfterValue - afterCap
            : Math.Max(0d, afterCap - cell.AfterValue);
        return isViolation
            ? $"EMS risk: +{FormatValue(distance)} over cap"
            : $"EMS OK: {FormatValue(distance)} margin";
    }

    public static string FormatRiskMarginText(SimulationSafetyHighRiskDiff item, double afterCap)
    {
        var isViolation = SimulationSafetyAuditService.IsEmsAfterCapViolation(item.AfterValue, afterCap);
        var distance = isViolation ? -item.MarginToCap : Math.Max(0d, item.MarginToCap);
        return isViolation
            ? $"+{FormatValue(distance)} over"
            : $"{FormatValue(distance)} margin";
    }

    private static string FormatHighRiskDiffLine(SimulationSafetyHighRiskDiff item, double afterCap)
    {
        var isViolation = SimulationSafetyAuditService.IsEmsAfterCapViolation(item.AfterValue, afterCap);
        var distance = isViolation ? -item.MarginToCap : Math.Max(0d, item.MarginToCap);
        var marginText = isViolation
            ? $"+{FormatValue(distance)} over cap"
            : $"margin {FormatValue(distance)}";
        return $"#{item.Rank} REG {item.RegularPadId} · IC {item.IcIndex + 1} · FW Diff {item.DiffIndex} · After {FormatValue(item.AfterValue)} · {marginText}";
    }
}
