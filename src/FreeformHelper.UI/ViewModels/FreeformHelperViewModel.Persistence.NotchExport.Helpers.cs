using System.Globalization;
using FreeformHelper.Application.Export;
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
                Serialize: (table, cad, grid) => NotchTableExporter.ExportAsCInitializer(
                    table,
                    cad,
                    grid,
                    _projectFile.Settings,
                    _projectFile.Settings.Notch.ExportProfile));
        }

        return (
            ExportKind: "CSV review",
            Extension: option.Extension,
            Serialize: (table, _, _) => NotchTableExporter.ExportAsCsv(table));
    }

    internal static string ResolveSuggestedNotchExportBaseName(string requestedBaseName, string extension, NotchTable table)
        => NotchExportText.ResolveSuggestedNotchExportBaseName(requestedBaseName, extension, table);

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
        _logger.Info(CultureInfo.InvariantCulture, "{0} export stage 3 switched file type to {1}.", exportKind, resolved.Display);
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
        _logger.Info(
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
        _logger.Info(message);
    }

    private Progress<NotchGenerationProgress> CreateNotchGenerationProgressReporter(string exportKind)
    {
        var finalProjectionRevision = _notchFinalProjection.Read();
        var sourceRevision = SimulationWorkspaceSourceRevision;
        var gate = new NotchProgressGate(
            TimeProvider.System,
            () => IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision),
            120);

        return new Progress<NotchGenerationProgress>(progress =>
        {
            if (!gate.ShouldPublish(progress))
            {
                return;
            }

            ReportExportGenerationProgress(exportKind, progress);
        });
    }

    private void ReportExportGenerationProgress(string exportKind, NotchGenerationProgress progress)
    {
        var status = CreateStatusScope("NotchExport");
        var phaseFraction = NotchExportText.ComputeGenerationPhaseFraction(progress);
        var stageStart = 1.0 / NotchExportStageCount;
        var stageSpan = 1.0 / NotchExportStageCount;
        NotchExportProgress = stageStart + stageSpan * Math.Clamp(phaseFraction, 0.0, 1.0);

        var (displayTotal, displayProcessed, phaseLabel) = NotchExportText.GetGenerationDisplayFacts(progress);
        var message = $"{exportKind} export [2/{NotchExportStageCount}]: {phaseLabel} {displayProcessed}/{displayTotal}, built rows={progress.GeneratedRowCount}";
        NotchExportSummary = message;
        status.ReportProgress(message);
        NotchExportProgressText = message;
    }

    private Progress<NotchGenerationProgress> CreateStep5GenerationProgressReporter(
        string operationLabel,
        long finalProjectionRevision,
        int sourceRevision,
        Action<string>? progressTextReporter = null)
    {
        var gate = new NotchProgressGate(
            TimeProvider.System,
            () => IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision),
            120);

        return new Progress<NotchGenerationProgress>(progress =>
        {
            if (!gate.ShouldPublish(progress))
            {
                return;
            }

            ReportStep5GenerationProgress(operationLabel, progress, progressTextReporter);
        });
    }

    private void ReportStep5GenerationProgress(
        string operationLabel,
        NotchGenerationProgress progress,
        Action<string>? progressTextReporter = null)
    {
        NotchExportProgress = Math.Clamp(NotchExportText.ComputeGenerationPhaseFraction(progress), 0.0, 1.0);
        var message = NotchExportText.BuildStep5GenerationProgressText(operationLabel, progress);
        NotchExportProgressText = message;
        progressTextReporter?.Invoke(message);
    }

    private static ProjectSettings CreateExportSettingsSnapshot(ProjectSettings source)
        => NotchComputationSettingsSnapshotFactory.Create(source);

}
