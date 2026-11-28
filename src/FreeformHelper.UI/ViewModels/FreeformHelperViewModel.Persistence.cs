using System.Globalization;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages all persistence-related
/// operations related to notch table export.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private readonly NotchExportWorkflow _notchExportWorkflow;

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

    private Task ExportNotchTableAsync(
        string exportKind,
        string operationName,
        string baseName,
        string extension,
        Func<NotchTable, CadPadSet, RegularGrid, string> serialize,
        bool allowSelectionExportTypeOverride = false)
        => _notchExportWorkflow.ExportNotchTableAsync(
            exportKind, operationName, baseName, extension, serialize, allowSelectionExportTypeOverride);

    private async Task<NotchTable?> BuildNotchTableForExportAsync(
        CadPadSet cad,
        RegularGrid grid,
        string exportKind,
        bool allowExportTypeSelection = false)
    {
        var status = CreateNotchExportStatus("BuildNotchTableForExport");
        if (!_notchExportWorkflow.TryPassNotchExportWorkflowGate(exportKind, status))
        {
            return null;
        }

        _notchExportWorkflow.ReportExportStage(exportKind, 2, "Generating notch table");
        var generationProgress = _notchExportWorkflow.CreateNotchGenerationProgressReporter(exportKind);
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
            _notchExportWorkflow.ReportNotchExportNoRows(status, exportKind);
            return null;
        }

        _notchExportWorkflow.ReportNotchExportStage2Summary(status, exportKind, table, crossIcMismatch);

        var safetyAudit = GetCurrentSimulationSafetyAudit?.Invoke();
        if (OpenNotchExportSelectionAsync is null)
        {
            return _notchExportWorkflow.ResolveNotchExportTableWithoutSelectionDialog(status, exportKind, table, safetyAudit);
        }

        _notchExportWorkflow.ReportExportStage(exportKind, 3, "Waiting row selection dialog");
        var selectionViewModel = CreateNotchExportSelectionViewModel(table, allowExportTypeSelection, safetyAudit);
        var selectedTable = await OpenNotchExportSelectionAsync(selectionViewModel);
        if (selectedTable is null)
        {
            _notchExportWorkflow.ReportNotchExportCanceled(status, $"{exportKind} export canceled at stage 3 (row selection).");
            return null;
        }

        if (allowExportTypeSelection)
        {
            ApplyNotchExportTypeSelection(selectionViewModel.SelectedExportTypeOption, exportKind);
            ApplyNotchExportVersionSelection(selectionViewModel.SelectedVersionOption, exportKind);
        }

        if (selectedTable.Rows.Count == 0)
        {
            _notchExportWorkflow.ReportNotchExportCanceled(status, $"{exportKind} export canceled at stage 3: no rows selected.");
            return null;
        }

        if (_notchExportWorkflow.TryBlockNotchExportBySimulationSafety(exportKind, status, selectionViewModel))
        {
            return null;
        }

        _notchExportWorkflow.ReportNotchExportRowsKept(exportKind, selectedTable, table);

        return selectedTable;
    }

    private NotchExportSelectionViewModel CreateNotchExportSelectionViewModel(
        NotchTable table,
        bool allowExportTypeSelection,
        SimulationSafetyAuditResult? safetyAudit)
    {
        var selectionViewModel = allowExportTypeSelection
            ? new NotchExportSelectionViewModel(
                table,
                PreviewNotchExportRowSelection,
                NotchExportFileTypeOptions,
                SelectedNotchExportFileTypeOption)
            : new NotchExportSelectionViewModel(table, PreviewNotchExportRowSelection);
        selectionViewModel.Editing.AttachProject(this);
        selectionViewModel.AttachSimulationSafetyAudit(safetyAudit);
        return selectionViewModel;
    }

    private NotchExportWorkflow CreateNotchExportWorkflow() => new(
        new NotchExportPorts(
            tryGetInputs: TryGetNotchExportInputs,
            getSavePicker: () => PickSaveNotchPathAsync,
            buildTableAsync: BuildNotchTableForExportAsync,
            getExportType: () => SelectedNotchExportFileTypeOption,
            getSettings: () => _projectFile.Settings,
            validateWorkflowGate: () =>
            {
                var gate = WorkflowStepGateService.Validate(WorkflowStepId.Step5Export, BuildWorkflowStateSnapshot());
                return (gate.IsAllowed, gate.Message);
            },
            captureGenerationCurrency: CaptureNotchExportGenerationCurrency,
            writeAsync: (path, payload) => TextExportWriter.WriteAsync(path, payload, includeBom: false, CancellationToken.None)),
        CreateNotchExportOperationPorts());

    private NotchExportOperationPorts CreateNotchExportOperationPorts() => new(
        createStatus: CreateNotchExportStatus,
        runAsync: RunUiOperationAsync,
        yieldUiAsync: YieldNotchExportUiAsync,
        beginBusy: BeginNotchOperationBusyScope,
        endBusy: EndNotchOperationBusyScope,
        setProgress: value => NotchExportProgress = value,
        setSummary: value => NotchExportSummary = value,
        getSummary: () => NotchExportSummary,
        setProgressText: value => NotchExportProgressText = value,
        formatError: NotchExportErrorFormatter.Format,
        info: message => _logger.Info(message),
        warn: message => _logger.Warn(message),
        infoFormat: (message, arguments) => _logger.Info(CultureInfo.InvariantCulture, message, arguments),
        warnFormat: (message, arguments) => _logger.Warn(CultureInfo.InvariantCulture, message, arguments),
        debugFormat: (message, arguments) => _logger.Debug(CultureInfo.InvariantCulture, message, arguments),
        error: (exception, message, arguments) => _logger.Error(exception, message, arguments));

    private bool TryGetNotchExportInputs(string missingStatus, out CadPadSet cad, out RegularGrid grid)
        => TryGetOperationCadAndGrid(BuildFilteredCadPadSet, missingStatus, out cad, out grid);

    private NotchExportStatus CreateNotchExportStatus(string operationName)
    {
        var status = CreateStatusScope(operationName);
        return new NotchExportStatus(status.ReportSuccess, status.ReportProgress, status.ReportBlocked, status.ReportStatus);
    }

    private Func<bool> CaptureNotchExportGenerationCurrency()
    {
        var finalProjectionRevision = _notchFinalProjection.Read();
        var sourceRevision = SimulationWorkspaceSourceRevision;
        return () => IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision);
    }

    private static async Task YieldNotchExportUiAsync()
    {
        if (UiThread.TryGetRunningDispatcher(out var dispatcher))
        {
            await dispatcher!.InvokeAsync(static () => { }, DispatcherPriority.Background);
        }
    }
}
