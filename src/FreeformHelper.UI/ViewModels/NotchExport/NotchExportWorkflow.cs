// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Sequences Notch export through live host ports without storing operation or document state.</summary>
internal sealed class NotchExportWorkflow(NotchExportPorts ports, NotchExportOperationPorts operations)
{
    private const int NotchExportStageCount = 5;
    private readonly NotchExportPorts _ports = ports;
    private readonly NotchExportOperationPorts _operations = operations;

    public async Task ExportNotchTableAsync(
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
        var status = _operations.CreateStatus(operationName);

        if (!_ports.TryGetInputs(
                "Export notch: import DXF and build grid first.",
                out var cad,
                out var grid))
        {
            return;
        }

        // Ensure the file picker delegate is wired up by the View.
        if (_ports.GetSavePicker() is null)
        {
            ReportNotchExportSavePickerMissing(exportKind, status);
            return;
        }

        _operations.BeginBusy();
        try
        {
            await _operations.RunAsync(
                operationName: operationName,
                showModalSpinner: false,
                operationAsync: async () =>
                {
                    // Let UI paint busy/progress state immediately before heavy work starts.
                    await _operations.YieldUiAsync();

                    var table = await _ports.BuildTableAsync(cad, grid, exportKind, allowSelectionExportTypeOverride);
                    if (table is null)
                    {
                        return;
                    }

                    var (effectiveExportKind, effectiveExtension, effectiveSerialize) = allowSelectionExportTypeOverride
                        ? ResolveDynamicNotchExportTarget()
                        : (exportKind, extension, serialize);

                    ReportExportStage(effectiveExportKind, 4, "Waiting save path dialog...");
                    var suggestedBaseName = NotchExportText.ResolveSuggestedNotchExportBaseName(baseName, effectiveExtension, table);
                    var path = await _ports.GetSavePicker()!(suggestedBaseName, effectiveExtension);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        ReportNotchExportCanceled(status, $"{effectiveExportKind} export canceled at stage 4 (save path not selected).");
                        return;
                    }

                    ReportExportStage(effectiveExportKind, 5, $"Writing file to '{path}'");
                    var payload = await Task.Run(() => effectiveSerialize(table, cad, grid));
                    await _ports.WriteAsync(path, payload);
                    ReportNotchExportSucceeded(status, effectiveExportKind, path, table);
                },
                onError: ex => ReportNotchExportFailed(status, exportKind, ex),
                onStart: () => ReportNotchExportStarting(exportKind));
        }
        finally
        {
            _operations.EndBusy();
        }
    }

    private void ReportNotchExportSavePickerMissing(string exportKind, NotchExportStatus status)
    {
        var blockedMessage = $"{exportKind} export blocked: save dialog handler not wired.";
        _operations.SetSummary(blockedMessage);
        _operations.SetProgressText(blockedMessage);
        status.ReportBlocked("Export notch: dialog handler not wired.");
        _operations.Warn(blockedMessage);
    }

    private void ReportNotchExportStarting(string exportKind)
    {
        _operations.SetProgress(0.0);
        _operations.SetProgressText($"{exportKind} export starting...");
    }

    public void ReportNotchExportCanceled(NotchExportStatus status, string cancelMessage)
    {
        _operations.SetSummary(cancelMessage);
        _operations.SetProgressText(cancelMessage);
        status.ReportBlocked(cancelMessage);
        _operations.Info(cancelMessage);
    }

    private void ReportNotchExportSucceeded(
        NotchExportStatus status,
        string effectiveExportKind,
        string path,
        NotchTable table)
    {
        _operations.SetProgress(1.0);
        var successMessage = $"{effectiveExportKind} exported: rows={table.Rows.Count}.";
        _operations.SetSummary(successMessage);
        _operations.SetProgressText(successMessage);
        status.ReportSuccess($"{effectiveExportKind} notch exported. Rows={table.Rows.Count}.");
        _operations.InfoFormat("{0} notch export completed: path={1}, rows={2}.", effectiveExportKind, path, table.Rows.Count);
    }

    private void ReportNotchExportFailed(NotchExportStatus status, string exportKind, Exception ex)
    {
        var message = _operations.FormatError(exportKind, ex);
        _operations.SetSummary(message);
        _operations.SetProgressText(message);
        status.ReportStatus(message);
        _operations.Error(ex, "{0} export failed.", exportKind);
    }

    public bool TryPassNotchExportWorkflowGate(string exportKind, NotchExportStatus status)
    {
        ReportExportStage(exportKind, 1, "Checking workflow prerequisites");
        var gate = _ports.ValidateWorkflowGate();
        if (!gate.IsAllowed)
        {
            var message = string.IsNullOrWhiteSpace(gate.Message)
                ? "Step 5: export is blocked by workflow prerequisites."
                : gate.Message!;
            _operations.SetSummary($"{exportKind} export blocked at stage 1: {message}");
            _operations.SetProgressText(_operations.GetSummary());
            status.ReportBlocked(message);
            _operations.WarnFormat("{0} export blocked at stage 1: {1}", exportKind, message);
            return false;
        }

        return true;
    }

    public void ReportNotchExportStage2Summary(
        NotchExportStatus status,
        string exportKind,
        NotchTable table,
        CrossIcMismatchSnapshot crossIcMismatch)
    {
        _operations.InfoFormat("{0} export stage 2 generated {1} row(s).", exportKind, table.Rows.Count);
        var stage2Summary = NotchExportText.BuildStage2RowSummary(exportKind, table, crossIcMismatch, NotchExportStageCount);
        _operations.SetSummary(stage2Summary);
        _operations.SetProgressText(stage2Summary);
        status.ReportProgress(stage2Summary);
        _operations.Info(stage2Summary);
    }

    public NotchTable? ResolveNotchExportTableWithoutSelectionDialog(
        NotchExportStatus status,
        string exportKind,
        NotchTable table,
        SimulationSafetyAuditResult? safetyAudit)
    {
        var directSelectionViewModel = new NotchExportSelectionViewModel(table);
        directSelectionViewModel.AttachSimulationSafetyAudit(safetyAudit);
        if (TryBlockNotchExportBySimulationSafety(exportKind, status, directSelectionViewModel))
        {
            return null;
        }

        _operations.DebugFormat("{0} export stage 3 skipped row selection: dialog handler not wired.", exportKind);
        return table;
    }

    public bool TryBlockNotchExportBySimulationSafety(
        string exportKind,
        NotchExportStatus status,
        NotchExportSelectionViewModel selectionViewModel)
    {
        if (!selectionViewModel.TryGetExportBlockMessage(out var title, out var message))
        {
            return false;
        }

        var blockedMessage = $"{exportKind} export blocked at stage 3: {title}.";
        _operations.SetSummary(blockedMessage);
        _operations.SetProgressText(message);
        status.ReportBlocked(blockedMessage);
        _operations.WarnFormat("{0} {1}", blockedMessage, message);
        return true;
    }

    public (string ExportKind, string Extension, Func<NotchTable, CadPadSet, RegularGrid, string> Serialize) ResolveDynamicNotchExportTarget()
    {
        var option = _ports.GetExportType();
        if (NotchExportFileTypeMetadata.IsCExportType(option.Value))
        {
            return (
                ExportKind: NotchExportFileTypeMetadata.GetExportKindLabel(option),
                Extension: option.Extension,
                Serialize: (table, cad, grid) => NotchTableExporter.ExportAsCInitializer(
                    table,
                    cad,
                    grid,
                    _ports.GetSettings(),
                    _ports.GetSettings().Notch.ExportProfile));
        }

        return (
            ExportKind: "CSV review",
            Extension: option.Extension,
            Serialize: (table, _, _) => NotchTableExporter.ExportAsCsv(table));
    }

    public void ReportExportStage(string exportKind, int stage, string detail)
    {
        var status = _operations.CreateStatus("NotchExport");
        var clampedStage = Math.Clamp(stage, 0, NotchExportStageCount);
        _operations.SetProgress(clampedStage / (double)NotchExportStageCount);
        var message = $"{exportKind} export [{clampedStage}/{NotchExportStageCount}]: {detail}";
        _operations.SetSummary(message);
        _operations.SetProgressText(message);
        status.ReportProgress(message);
        _operations.Info(message);
    }

    public Progress<NotchGenerationProgress> CreateNotchGenerationProgressReporter(string exportKind)
    {
        var isCurrent = _ports.CaptureGenerationCurrency();
        var gate = new NotchProgressGate(
            TimeProvider.System,
            isCurrent,
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
        var status = _operations.CreateStatus("NotchExport");
        var phaseFraction = NotchExportText.ComputeGenerationPhaseFraction(progress);
        var stageStart = 1.0 / NotchExportStageCount;
        var stageSpan = 1.0 / NotchExportStageCount;
        _operations.SetProgress(stageStart + stageSpan * Math.Clamp(phaseFraction, 0.0, 1.0));

        var (displayTotal, displayProcessed, phaseLabel) = NotchExportText.GetGenerationDisplayFacts(progress);
        var message = $"{exportKind} export [2/{NotchExportStageCount}]: {phaseLabel} {displayProcessed}/{displayTotal}, built rows={progress.GeneratedRowCount}";
        _operations.SetSummary(message);
        status.ReportProgress(message);
        _operations.SetProgressText(message);
    }

    public void ReportNotchExportNoRows(NotchExportStatus status, string exportKind)
    {
        _operations.SetSummary($"{exportKind} export stopped at stage 2: no rows generated.");
        _operations.SetProgressText(_operations.GetSummary());
        status.ReportBlocked("Export notch: no rows generated.");
        _operations.WarnFormat("{0} export stage 2 produced no rows.", exportKind);
    }

    public void ReportNotchExportRowsKept(string exportKind, NotchTable selectedTable, NotchTable table)
    {
        _operations.InfoFormat("{0} export stage 3 kept {1}/{2} row(s).",
            exportKind,
            selectedTable.Rows.Count,
            table.Rows.Count);
    }
}
