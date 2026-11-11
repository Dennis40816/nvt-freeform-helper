// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal delegate bool NotchExportTryGetInputs(string missingStatus, out CadPadSet cad, out RegularGrid grid);

internal delegate Task NotchExportRunOperationAsync(
    string operationName, Func<Task> operationAsync, Action<Exception> onError, Action onStart, bool showModalSpinner);

internal delegate void NotchExportLogMessage(string message, params object?[] arguments);

internal delegate void NotchExportLogError(Exception exception, string message, params object?[] arguments);

/// <summary>Live input and I/O adapters; generation remains on the existing host path.</summary>
internal sealed class NotchExportPorts(
    NotchExportTryGetInputs tryGetInputs,
    Func<Func<string, string, Task<string?>>?> getSavePicker,
    Func<CadPadSet, RegularGrid, string, bool, Task<NotchTable?>> buildTableAsync,
    Func<FreeformHelperViewModel.NotchExportFileTypeOption> getExportType,
    Func<ProjectSettings> getSettings,
    Func<(bool IsAllowed, string? Message)> validateWorkflowGate,
    Func<Func<bool>> captureGenerationCurrency,
    Func<string, string, Task> writeAsync)
{
    public NotchExportTryGetInputs TryGetInputs { get; } = tryGetInputs;
    public Func<Func<string, string, Task<string?>>?> GetSavePicker { get; } = getSavePicker;
    public Func<CadPadSet, RegularGrid, string, bool, Task<NotchTable?>> BuildTableAsync { get; } = buildTableAsync;
    public Func<FreeformHelperViewModel.NotchExportFileTypeOption> GetExportType { get; } = getExportType;
    public Func<ProjectSettings> GetSettings { get; } = getSettings;
    public Func<(bool IsAllowed, string? Message)> ValidateWorkflowGate { get; } = validateWorkflowGate;
    public Func<Func<bool>> CaptureGenerationCurrency { get; } = captureGenerationCurrency;
    public Func<string, string, Task> WriteAsync { get; } = writeAsync;
}

/// <summary>Retains host notification senders, status scopes, dispatcher, busy scope, and logger identity.</summary>
internal sealed class NotchExportOperationPorts(
    Func<string, NotchExportStatus> createStatus,
    NotchExportRunOperationAsync runAsync,
    Func<Task> yieldUiAsync,
    Action beginBusy,
    Action endBusy,
    Action<double> setProgress,
    Action<string> setSummary,
    Func<string> getSummary,
    Action<string> setProgressText,
    Func<string, Exception, string> formatError,
    Action<string> info,
    Action<string> warn,
    NotchExportLogMessage infoFormat,
    NotchExportLogMessage warnFormat,
    NotchExportLogMessage debugFormat,
    NotchExportLogError error)
{
    public Func<string, NotchExportStatus> CreateStatus { get; } = createStatus;
    public NotchExportRunOperationAsync RunAsync { get; } = runAsync;
    public Func<Task> YieldUiAsync { get; } = yieldUiAsync;
    public Action BeginBusy { get; } = beginBusy;
    public Action EndBusy { get; } = endBusy;
    public Action<double> SetProgress { get; } = setProgress;
    public Action<string> SetSummary { get; } = setSummary;
    public Func<string> GetSummary { get; } = getSummary;
    public Action<string> SetProgressText { get; } = setProgressText;
    public Func<string, Exception, string> FormatError { get; } = formatError;
    public Action<string> Info { get; } = info;
    public Action<string> Warn { get; } = warn;
    public NotchExportLogMessage InfoFormat { get; } = infoFormat;
    public NotchExportLogMessage WarnFormat { get; } = warnFormat;
    public NotchExportLogMessage DebugFormat { get; } = debugFormat;
    public NotchExportLogError Error { get; } = error;
}

internal sealed class NotchExportStatus(
    Action<string> reportSuccess,
    Action<string> reportProgress,
    Action<string> reportBlocked,
    Action<string> reportStatus)
{
    public Action<string> ReportSuccess { get; } = reportSuccess;
    public Action<string> ReportProgress { get; } = reportProgress;
    public Action<string> ReportBlocked { get; } = reportBlocked;
    public Action<string> ReportStatus { get; } = reportStatus;
}
