using System.Globalization;
using Avalonia.Threading;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages all persistence-related
/// operations related to notch table export.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private const int NotchExportStageCount = 5;

    /// <summary>
    /// Asynchronously exports the generated notch table using the currently selected export type.
    /// </summary>
    private async Task ExportNotchAsync()
    {
        var target = ResolveDynamicNotchExportTarget();
        await ExportNotchTableAsync(
            exportKind: target.ExportKind,
            operationName: "Export notch table",
            baseName: "notch_table",
            extension: target.Extension,
            serialize: target.Serialize,
            allowSelectionExportTypeOverride: true);
    }

    private async Task ExportNotchTableAsync(
        string exportKind,
        string operationName,
        string baseName,
        string extension,
        Func<NotchTable, CadPadSet, RegularGrid, string> serialize,
        bool allowSelectionExportTypeOverride = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentNullException.ThrowIfNull(serialize);
        var status = CreateStatusScope(operationName);

        if (!TryGetOperationCadAndGrid(
                BuildFilteredCadPadSet,
                "Export notch: import DXF and build grid first.",
                out var cad,
                out var grid))
        {
            return;
        }

        // Ensure the file picker delegate is wired up by the View.
        if (PickSaveNotchPathAsync is null)
        {
            var blockedMessage = $"{exportKind} export blocked: save dialog handler not wired.";
            NotchExportSummary = blockedMessage;
            NotchExportProgressText = blockedMessage;
            status.ReportBlocked("Export notch: dialog handler not wired.");
            Logger.Warn(blockedMessage);
            return;
        }

        BeginNotchOperationBusyScope();
        try
        {
            await RunUiOperationAsync(
                operationName: operationName,
                showModalSpinner: false,
                operationAsync: async () =>
                {
                    // Let UI paint busy/progress state immediately before heavy work starts.
                    if (UiThread.TryGetRunningDispatcher(out var dispatcher))
                    {
                        await dispatcher!.InvokeAsync(static () => { }, DispatcherPriority.Background);
                    }

                    var table = await BuildNotchTableForExportAsync(cad, grid, exportKind, allowSelectionExportTypeOverride);
                    if (table is null)
                    {
                        return;
                    }

                    var effectiveExportKind = exportKind;
                    var effectiveExtension = extension;
                    var effectiveSerialize = serialize;
                    if (allowSelectionExportTypeOverride)
                    {
                        var selectedTarget = ResolveDynamicNotchExportTarget();
                        effectiveExportKind = selectedTarget.ExportKind;
                        effectiveExtension = selectedTarget.Extension;
                        effectiveSerialize = selectedTarget.Serialize;
                    }

                    ReportExportStage(effectiveExportKind, 4, "Waiting save path dialog...");
                    var suggestedBaseName = ResolveSuggestedNotchExportBaseName(baseName, effectiveExtension, table);
                    var path = await PickSaveNotchPathAsync(suggestedBaseName, effectiveExtension);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        var cancelMessage = $"{effectiveExportKind} export canceled at stage 4 (save path not selected).";
                        NotchExportSummary = cancelMessage;
                        NotchExportProgressText = cancelMessage;
                        status.ReportBlocked(cancelMessage);
                        Logger.Info(cancelMessage);
                        return;
                    }

                    ReportExportStage(effectiveExportKind, 5, $"Writing file to '{path}'");
                    var payload = await Task.Run(() => effectiveSerialize(table, cad, grid));
                    await Task.Run(() => File.WriteAllText(path, payload));
                    NotchExportProgress = 1.0;
                    var successMessage = $"{effectiveExportKind} exported: rows={table.Rows.Count}.";
                    NotchExportSummary = successMessage;
                    NotchExportProgressText = successMessage;
                    status.ReportSuccess($"{effectiveExportKind} notch exported. Rows={table.Rows.Count}.");
                    Logger.Info(CultureInfo.InvariantCulture, "{0} notch export completed: path={1}, rows={2}.", effectiveExportKind, path, table.Rows.Count);
                },
                onError: ex =>
                {
                    var message = NotchExportErrorFormatter.Format(exportKind, ex);
                    NotchExportSummary = message;
                    NotchExportProgressText = message;
                    status.ReportStatus(message);
                    Logger.Error(ex, "{0} export failed.", exportKind);
                },
                onStart: () =>
                {
                    NotchExportProgress = 0.0;
                    NotchExportProgressText = $"{exportKind} export starting...";
                });
        }
        finally
        {
            EndNotchOperationBusyScope();
        }
    }

    private async Task<NotchTable?> BuildNotchTableForExportAsync(
        CadPadSet cad,
        RegularGrid grid,
        string exportKind,
        bool allowExportTypeSelection = false)
    {
        var status = CreateStatusScope("BuildNotchTableForExport");
        ReportExportStage(exportKind, 1, "Checking workflow prerequisites");
        var gate = WorkflowStepGateService.Validate(WorkflowStepId.Step5Export, BuildWorkflowStateSnapshot());
        if (!gate.IsAllowed)
        {
            var message = string.IsNullOrWhiteSpace(gate.Message)
                ? "Step 5: export is blocked by workflow prerequisites."
                : gate.Message!;
            NotchExportSummary = $"{exportKind} export blocked at stage 1: {message}";
            NotchExportProgressText = NotchExportSummary;
            status.ReportBlocked(message);
            Logger.Warn(CultureInfo.InvariantCulture, "{0} export blocked at stage 1: {1}", exportKind, message);
            return null;
        }

        ReportExportStage(exportKind, 2, "Generating notch table");
        var generationProgress = CreateNotchGenerationProgressReporter(exportKind);
        var generation = await GenerateCurrentNotchTableAsync(
            cad,
            grid,
            $"{exportKind} export stage 2",
            generationProgress);
        if (generation is null)
        {
            return null;
        }

        var table = generation.Table;
        var crossIcMismatch = generation.CrossIcMismatch;
        if (table.Rows.Count == 0)
        {
            NotchExportSummary = $"{exportKind} export stopped at stage 2: no rows generated.";
            NotchExportProgressText = NotchExportSummary;
            status.ReportBlocked("Export notch: no rows generated.");
            Logger.Warn(CultureInfo.InvariantCulture, "{0} export stage 2 produced no rows.", exportKind);
            return null;
        }

        Logger.Info(CultureInfo.InvariantCulture, "{0} export stage 2 generated {1} row(s).", exportKind, table.Rows.Count);
        var stage2Summary = BuildStage2RowSummary(exportKind, table, crossIcMismatch);
        NotchExportSummary = stage2Summary;
        NotchExportProgressText = stage2Summary;
        status.ReportProgress(stage2Summary);
        Logger.Info(stage2Summary);

        var safetyAudit = GetCurrentSimulationSafetyAudit?.Invoke();
        if (OpenNotchExportSelectionAsync is null)
        {
            var directSelectionViewModel = new NotchExportSelectionViewModel(table);
            directSelectionViewModel.AttachSimulationSafetyAudit(safetyAudit);
            if (TryBlockNotchExportBySimulationSafety(exportKind, status, directSelectionViewModel))
            {
                return null;
            }

            Logger.Debug(CultureInfo.InvariantCulture, "{0} export stage 3 skipped row selection: dialog handler not wired.", exportKind);
            return table;
        }

        ReportExportStage(exportKind, 3, "Waiting row selection dialog");
        var selectionViewModel = allowExportTypeSelection
            ? new NotchExportSelectionViewModel(
                table,
                PreviewNotchExportRowSelection,
                NotchExportFileTypeOptions,
                SelectedNotchExportFileTypeOption)
            : new NotchExportSelectionViewModel(table, PreviewNotchExportRowSelection);
        selectionViewModel.AttachSimulationSafetyAudit(safetyAudit);
        var selectedTable = await OpenNotchExportSelectionAsync(selectionViewModel);
        if (selectedTable is null)
        {
            var cancelMessage = $"{exportKind} export canceled at stage 3 (row selection).";
            NotchExportSummary = cancelMessage;
            NotchExportProgressText = cancelMessage;
            status.ReportBlocked(cancelMessage);
            Logger.Info(cancelMessage);
            return null;
        }

        if (allowExportTypeSelection)
        {
            ApplyNotchExportTypeSelection(selectionViewModel.SelectedExportTypeOption, exportKind);
            ApplyNotchExportVersionSelection(selectionViewModel.SelectedVersionOption, exportKind);
        }

        if (selectedTable.Rows.Count == 0)
        {
            var cancelMessage = $"{exportKind} export canceled at stage 3: no rows selected.";
            NotchExportSummary = cancelMessage;
            NotchExportProgressText = cancelMessage;
            status.ReportBlocked(cancelMessage);
            Logger.Info(cancelMessage);
            return null;
        }

        if (TryBlockNotchExportBySimulationSafety(exportKind, status, selectionViewModel))
        {
            return null;
        }

        Logger.Info(CultureInfo.InvariantCulture, "{0} export stage 3 kept {1}/{2} row(s).",
            exportKind,
            selectedTable.Rows.Count,
            table.Rows.Count);

        return selectedTable;
    }

    private bool TryBlockNotchExportBySimulationSafety(
        string exportKind,
        UiOperationStatusReporter.Scope status,
        NotchExportSelectionViewModel selectionViewModel)
    {
        if (!selectionViewModel.TryGetExportBlockMessage(out var title, out var message))
        {
            return false;
        }

        var blockedMessage = $"{exportKind} export blocked at stage 3: {title}.";
        NotchExportSummary = blockedMessage;
        NotchExportProgressText = message;
        status.ReportBlocked(blockedMessage);
        Logger.Warn(CultureInfo.InvariantCulture, "{0} {1}", blockedMessage, message);
        return true;
    }

}

