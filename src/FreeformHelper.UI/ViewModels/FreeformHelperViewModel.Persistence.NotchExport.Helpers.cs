using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private (string ExportKind, string Extension, Func<NotchTable, CadPadSet, RegularGrid, string> Serialize) ResolveDynamicNotchExportTarget()
        => _notchExportWorkflow.ResolveDynamicNotchExportTarget();

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
