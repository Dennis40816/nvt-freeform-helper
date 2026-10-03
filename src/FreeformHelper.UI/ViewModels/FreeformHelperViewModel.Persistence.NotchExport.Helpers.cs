using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private (string ExportKind, string Extension, Func<NotchTable, CadPadSet, RegularGrid, string> Serialize) ResolveDynamicNotchExportTarget()
    {
        var option = SelectedNotchExportFileTypeOption;
        if (NotchExportFileTypeMetadata.IsCExportType(option.Value))
        {
            return (
                ExportKind: NotchExportFileTypeMetadata.GetExportKindLabel(option),
                Extension: option.Extension,
                Serialize: (table, cad, grid) => _notchExportService.ExportCInitializer(
                    table,
                    cad,
                    grid,
                    _projectFile.Settings,
                    _projectFile.Settings.Notch.ExportProfile,
                    GetActiveRegularPadIdsForNotchComputation()));
        }

        return (
            ExportKind: "CSV review",
            Extension: option.Extension,
            Serialize: (table, _, _) => _notchExportService.ExportCsv(table));
    }

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

    private void ApplyNotchExportTypeSelection(NotchExportFileTypeOption selectedOption, string exportKind)
    {
        if (string.IsNullOrWhiteSpace(selectedOption.Display))
        {
            return;
        }

        var resolved = NotchExportFileTypeOptions.FirstOrDefault(option => option.Value == selectedOption.Value);
        if (string.IsNullOrWhiteSpace(resolved.Display))
        {
            return;
        }

        if (resolved.Equals(SelectedNotchExportFileTypeOption))
        {
            return;
        }

        SelectedNotchExportFileTypeOption = resolved;
        Logger.Info(CultureInfo.InvariantCulture, "{0} export stage 3 switched file type to {1}.", exportKind, resolved.Display);
    }

    private void ApplyNotchExportVersionSelection(NotchExportVersionOption selectedOption, string exportKind)
    {
        // "All versions" means keep current project settings as-is.
        if (!selectedOption.Value.HasValue)
        {
            return;
        }

        var version = selectedOption.Value.Value;
        var nextEnableV21 = version == NotchAlgorithmVersion.V21;
        var nextEnableV22 = version == NotchAlgorithmVersion.V22;
        if (!nextEnableV21 && !nextEnableV22)
        {
            return;
        }

        if (EnableV21 == nextEnableV21 &&
            EnableV22 == nextEnableV22)
        {
            return;
        }

        EnableV21 = nextEnableV21;
        EnableV22 = nextEnableV22;
        Logger.Info(
            CultureInfo.InvariantCulture,
            "{0} export stage 3 switched notch version scope to {1} (persist-on-save).",
            exportKind,
            selectedOption.Display);
    }

    private void PreviewNotchExportRowSelection(NotchTableRow row)
    {
        var cadIds = row.CadPadId.HasValue
            ? new[] { row.CadPadId.Value }
            : Array.Empty<int>();
        var regularIndices = row.RegularPadIndex >= 0
            ? new[] { row.RegularPadIndex }
            : Array.Empty<int>();

        TryLocateSelection(
            cadIds,
            regularIndices,
            focusSelection: true,
            notVisibleStatus: "Notch row preview target is not visible.",
            successStatusBuilder: (cadCount, regularCount) =>
                $"Notch row focused: CAD={cadCount}, REG={regularCount}.",
            minZoomOverride: NotchExportPreviewFocusMinZoom);
    }

    private void ReportExportStage(string exportKind, int stage, string detail)
    {
        var status = CreateStatusScope("NotchExport");
        var clampedStage = Math.Clamp(stage, 0, NotchExportStageCount);
        NotchExportProgress = clampedStage / (double)NotchExportStageCount;
        var message = $"{exportKind} export [{clampedStage}/{NotchExportStageCount}]: {detail}";
        NotchExportSummary = message;
        NotchExportProgressText = message;
        status.ReportProgress(message);
        Logger.Info(message);
    }

    private Progress<NotchGenerationProgress> CreateNotchGenerationProgressReporter(string exportKind)
    {
        const int minUpdateIntervalMs = 120;
        var finalProjectionRevision = Volatile.Read(ref _notchFinalProjectionRevision);
        var sourceRevision = SimulationWorkspaceSourceRevision;
        var lastTick = 0L;

        return new Progress<NotchGenerationProgress>(progress =>
        {
            if (!IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision))
            {
                return;
            }

            var total = Math.Max(progress.TotalCount, 1);
            var processed = Math.Clamp(progress.ProcessedCount, 0, total);
            var isFinal = processed >= total;
            var now = Environment.TickCount64;
            if (!isFinal && now - lastTick < minUpdateIntervalMs)
            {
                return;
            }

            lastTick = now;
            ReportExportGenerationProgress(exportKind, progress);
        });
    }

    private void ReportExportGenerationProgress(string exportKind, NotchGenerationProgress progress)
    {
        var status = CreateStatusScope("NotchExport");
        var phaseFraction = ComputeGenerationPhaseFraction(progress);
        var stageStart = 1.0 / NotchExportStageCount;
        var stageSpan = 1.0 / NotchExportStageCount;
        NotchExportProgress = stageStart + stageSpan * Math.Clamp(phaseFraction, 0.0, 1.0);

        var displayTotal = Math.Max(progress.TotalCount, 0);
        var displayProcessed = displayTotal <= 0
            ? 0
            : Math.Clamp(progress.ProcessedCount, 0, displayTotal);
        var phaseLabel = string.IsNullOrWhiteSpace(progress.Phase)
            ? "Scan CAD"
            : progress.Phase;
        var message = $"{exportKind} export [2/{NotchExportStageCount}]: {phaseLabel} {displayProcessed}/{displayTotal}, built rows={progress.GeneratedRowCount}";
        NotchExportSummary = message;
        status.ReportProgress(message);
        NotchExportProgressText = message;
    }

    private Progress<NotchGenerationProgress> CreateStep5GenerationProgressReporter(
        string operationLabel,
        int finalProjectionRevision,
        int sourceRevision,
        Action<string>? progressTextReporter = null)
    {
        const int minUpdateIntervalMs = 120;
        var lastTick = 0L;

        return new Progress<NotchGenerationProgress>(progress =>
        {
            if (!IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision))
            {
                return;
            }

            var total = Math.Max(progress.TotalCount, 1);
            var processed = Math.Clamp(progress.ProcessedCount, 0, total);
            var isFinal = processed >= total;
            var now = Environment.TickCount64;
            if (!isFinal && now - lastTick < minUpdateIntervalMs)
            {
                return;
            }

            lastTick = now;
            ReportStep5GenerationProgress(operationLabel, progress, progressTextReporter);
        });
    }

    private void ReportStep5GenerationProgress(
        string operationLabel,
        NotchGenerationProgress progress,
        Action<string>? progressTextReporter = null)
    {
        NotchExportProgress = Math.Clamp(ComputeGenerationPhaseFraction(progress), 0.0, 1.0);
        var message = BuildStep5GenerationProgressText(operationLabel, progress);
        NotchExportProgressText = message;
        progressTextReporter?.Invoke(message);
    }

    private static double ComputeGenerationPhaseFraction(NotchGenerationProgress progress)
    {
        var total = Math.Max(progress.TotalCount, 1);
        var processed = Math.Clamp(progress.ProcessedCount, 0, total);
        var phaseStepCount = Math.Max(progress.PhaseStepCount, 1);
        var phaseStep = Math.Clamp(progress.PhaseStep <= 0 ? 1 : progress.PhaseStep, 1, phaseStepCount);
        return ((phaseStep - 1) + (processed / (double)total)) / phaseStepCount;
    }

    private static string BuildStep5GenerationProgressText(string operationLabel, NotchGenerationProgress progress)
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

    private static ProjectSettings CreateExportSettingsSnapshot(ProjectSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = source.Notch.ComputationMode,
                CompensationModel = source.Notch.CompensationModel,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>(source.Notch.EnabledVersions),
                LenScale = source.Notch.LenScale,
                NullValue = source.Notch.NullValue,
                ThresholdQ7 = source.Notch.ThresholdQ7,
                ThresholdPercentV22 = source.Notch.ThresholdPercentV22,
                LinkVersionThresholds = source.Notch.LinkVersionThresholds,
                EnableToRegular = source.Notch.EnableToRegular,
                EnableToFull = source.Notch.EnableToFull,
                MultiOwnerStrictOverlapPercent = source.Notch.MultiOwnerStrictOverlapPercent,
                EnableToFullRuleEngine = source.Notch.EnableToFullRuleEngine,
                EnableToFullRuleTrace = source.Notch.EnableToFullRuleTrace,
                EnableBoundaryVirtualAreaCap = source.Notch.EnableBoundaryVirtualAreaCap,
                BoundaryVirtualAreaCapRatio = source.Notch.BoundaryVirtualAreaCapRatio,
                EnableTargetCoverageGuard = source.Notch.EnableTargetCoverageGuard,
                TargetCoverageCapPercent = source.Notch.TargetCoverageCapPercent,
                ExportProfile = source.Notch.ExportProfile
            }
        };
    }

    private static string BuildStage2RowSummary(
        string exportKind,
        NotchTable table,
        CrossIcMismatchSnapshot crossIcMismatch)
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
        return $"{exportKind} export [2/{NotchExportStageCount}]: rows={table.Rows.Count} | cad={cadCount} | reg={regularCount} | {versionText} | {toFullCoverageText}{crossIcText}";
    }

    private static string BuildVersionBreakdownText(NotchTable table)
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

    private static string BuildToFullCoverageStatusText(NotchToFullCoverageAudit audit)
    {
        if (!audit.HasExpectations)
        {
            return "toFullCoverage=n/a";
        }

        return audit.HasMissingCoverage
            ? $"toFullCoverage=miss {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount}"
            : $"toFullCoverage=ok {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount}";
    }

    private static int ComputeNotchExportSettingsFingerprint(ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        unchecked
        {
            var notch = settings.Notch;
            var hash = 17;
            hash = (hash * 31) + (int)notch.ComputationMode;
            hash = (hash * 31) + (int)notch.CompensationModel;
            hash = (hash * 31) + notch.LenScale;
            hash = (hash * 31) + notch.NullValue;
            hash = (hash * 31) + notch.ThresholdQ7;
            hash = (hash * 31) + notch.ThresholdPercentV22.GetHashCode();
            hash = (hash * 31) + (notch.LinkVersionThresholds ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToRegular ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToFull ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToFullRuleEngine ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToFullRuleTrace ? 1 : 0);
            hash = (hash * 31) + (notch.EnableBoundaryVirtualAreaCap ? 1 : 0);
            hash = (hash * 31) + notch.BoundaryVirtualAreaCapRatio.GetHashCode();
            hash = (hash * 31) + (notch.EnableTargetCoverageGuard ? 1 : 0);
            hash = (hash * 31) + notch.TargetCoverageCapPercent;
            hash = (hash * 31) + notch.EnabledVersions.Count;
            hash = (hash * 31) + notch.MultiOwnerStrictOverlapPercent.GetHashCode();
            foreach (var version in notch.EnabledVersions.OrderBy(static version => (int)version))
            {
                hash = (hash * 31) + (int)version;
            }

            return hash;
        }
    }

    private static int CombineNotchExportSettingsFingerprint(
        int settingsFingerprint,
        int activeRegularHash,
        int cadOutputFwDiffHash)
    {
        unchecked
        {
            var hash = (settingsFingerprint * 31) + activeRegularHash;
            return (hash * 31) + cadOutputFwDiffHash;
        }
    }

    private static int ComputeCadOutputFwDiffHash(IReadOnlyDictionary<int, int> cadOutputFwDiffIndexByCadId)
    {
        ArgumentNullException.ThrowIfNull(cadOutputFwDiffIndexByCadId);

        unchecked
        {
            var hash = 17;
            foreach (var pair in cadOutputFwDiffIndexByCadId.OrderBy(static pair => pair.Key))
            {
                hash = (hash * 31) + pair.Key;
                hash = (hash * 31) + pair.Value;
            }

            return hash;
        }
    }

    private static CrossIcMismatchSnapshot BuildCrossIcMismatchSnapshot(RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var crossIcGroups = grid.Pads
            .Where(pad => pad.MatchedCadPadId.HasValue)
            .GroupBy(pad => pad.MatchedCadPadId!.Value)
            .Select(group => new
            {
                CadId = group.Key,
                RegularCount = group.Count(),
                IcIndices = group
                    .Select(pad => pad.IcIndex)
                    .Distinct()
                    .OrderBy(ic => ic)
                    .ToList(),
            })
            .Where(item => item.IcIndices.Count > 1)
            .OrderBy(item => item.CadId)
            .ToList();

        if (crossIcGroups.Count == 0)
        {
            return new CrossIcMismatchSnapshot(
                CadCount: 0,
                RegularCount: 0,
                SampleCadText: string.Empty);
        }

        var sample = crossIcGroups
            .Take(6)
            .Select(item => $"CAD{item.CadId}(IC:{string.Join("/", item.IcIndices.Select(ic => ic + 1))})");
        var suffix = crossIcGroups.Count > 6 ? $"+{crossIcGroups.Count - 6}" : string.Empty;
        return new CrossIcMismatchSnapshot(
            CadCount: crossIcGroups.Count,
            RegularCount: crossIcGroups.Sum(item => item.RegularCount),
            SampleCadText: string.Join(", ", sample) + suffix);
    }

    private readonly record struct CrossIcMismatchSnapshot(
        int CadCount,
        int RegularCount,
        string SampleCadText);
}
