// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.ViewModels;

internal static class NotchExportText
{
    internal static string ResolveSuggestedNotchExportBaseName(string requestedBaseName, string extension, NotchTable table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentNullException.ThrowIfNull(table);

        var normalizedExtension = extension.TrimStart('.').Trim();
        if (!string.Equals(normalizedExtension, "c", StringComparison.OrdinalIgnoreCase))
        {
            return requestedBaseName;
        }

        var versions = table.Rows
            .Select(static row => row.Version)
            .Distinct()
            .ToList();
        if (versions.Count != 1)
        {
            return requestedBaseName;
        }

        return $"notch_{versions[0].ToDisplayLabel()}";
    }

    internal static double ComputeGenerationPhaseFraction(NotchGenerationProgress progress)
    {
        var total = Math.Max(progress.TotalCount, 1);
        var processed = Math.Clamp(progress.ProcessedCount, 0, total);
        var phaseStepCount = Math.Max(progress.PhaseStepCount, 1);
        var phaseStep = Math.Clamp(progress.PhaseStep <= 0 ? 1 : progress.PhaseStep, 1, phaseStepCount);
        return ((phaseStep - 1) + (processed / (double)total)) / phaseStepCount;
    }

    internal static string BuildStep5GenerationProgressText(string operationLabel, NotchGenerationProgress progress)
    {
        var displayTotal = Math.Max(progress.TotalCount, 0);
        var displayProcessed = displayTotal <= 0
            ? 0
            : Math.Clamp(progress.ProcessedCount, 0, displayTotal);
        var phaseLabel = string.IsNullOrWhiteSpace(progress.Phase)
            ? "Scan CAD"
            : progress.Phase;
        return $"{operationLabel}: {phaseLabel} {displayProcessed}/{displayTotal}, rows={progress.GeneratedRowCount}";
    }

    internal static string BuildStage2RowSummary(
        string exportKind,
        NotchTable table,
        CrossIcMismatchSnapshot crossIcMismatch,
        int stageCount)
    {
        var versionText = BuildVersionBreakdownText(table);
        var cadCount = table.Rows
            .Where(row => row.CadPadId.HasValue)
            .Select(row => row.CadPadId!.Value)
            .Distinct()
            .Count();
        var regularCount = table.Rows
            .Select(row => row.RegularPadIndex)
            .Distinct()
            .Count();
        var crossIcText = crossIcMismatch.CadCount > 0
            ? $" | crossIC={crossIcMismatch.CadCount} cad/{crossIcMismatch.RegularCount} reg"
            : string.Empty;
        var toFullCoverageText = BuildToFullCoverageStatusText(table.ToFullCoverageAudit);
        return $"{exportKind} export [2/{stageCount}]: rows={table.Rows.Count} | cad={cadCount} | reg={regularCount} | {versionText} | {toFullCoverageText}{crossIcText}";
    }

    internal static string BuildVersionBreakdownText(NotchTable table)
    {
        var items = table.Rows
            .GroupBy(row => row.Version)
            .OrderBy(group => (int)group.Key)
            .Select(group => $"{group.Key.ToDisplayLabel()}={group.Count()}")
            .ToList();
        return items.Count == 0
            ? "versions=-"
            : $"versions:{string.Join(", ", items)}";
    }

    internal static string BuildToFullCoverageStatusText(NotchToFullCoverageAudit audit)
    {
        if (!audit.HasExpectations)
        {
            return "toFullCoverage=n/a";
        }

        return audit.HasMissingCoverage
            ? $"toFullCoverage=miss {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount}"
            : $"toFullCoverage=ok {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount}";
    }
}
