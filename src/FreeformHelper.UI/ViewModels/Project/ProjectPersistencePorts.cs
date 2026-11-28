// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels.Project;

internal delegate Task<(bool IsCancelled, string? Path, string? EmbedWarning)> ProjectSaveAsync(
    Func<Task<string?>> pickPathAsync, Func<Task<bool>>? confirmEmbedAsync);

internal delegate Task<ProjectReplacementPlan> ProjectPrepareReplacementAsync(
    Func<Task<string?>> pickPathAsync, Func<bool> hasUnsavedWork, Func<Task<string?>> backUpAsync);

internal delegate Task<bool> ProjectSaveThenLoadAsync(
    string path, string? backupPath, Func<Task<bool>> saveAsync, Action<string?> deleteBackup, Func<string, Task> loadAsync);

internal delegate Task ProjectRunOperationAsync(
    string operationName, Func<Task> operationAsync, Action<Exception> onError, Action onStart, bool showModalSpinner);

internal delegate Task<bool> ProjectRunBooleanOperationAsync(
    string operationName, Func<Task<bool>> operationAsync, Func<Exception, bool> onError, Action onStart, bool showModalSpinner);

internal delegate void ProjectLogMessage(string message, params object?[] arguments);

/// <summary>Live persistence adapters. Service decisions are projected without being recomputed.</summary>
internal sealed class ProjectPersistencePorts(
    Func<Func<Task<string?>>?> getSavePicker,
    Func<Func<Task<string?>>?> getLoadPicker,
    Func<Func<Task<bool>>?> getEmbedConfirmation,
    ProjectSaveAsync saveAsync,
    Func<string, Task<(bool IsCancelled, ProjectFile? Project, string? Path)>> loadAsync,
    Func<string, (string? NormalizedPath, string? Code, string? Message)> resolvePath,
    ProjectPrepareReplacementAsync prepareReplacementAsync,
    ProjectSaveThenLoadAsync saveThenLoadAsync,
    Func<string> getBackupPath,
    Action<string?> deleteBackup)
{
    public Func<Func<Task<string?>>?> GetSavePicker { get; } = getSavePicker;
    public Func<Func<Task<string?>>?> GetLoadPicker { get; } = getLoadPicker;
    public Func<Func<Task<bool>>?> GetEmbedConfirmation { get; } = getEmbedConfirmation;
    public ProjectSaveAsync SaveAsync { get; } = saveAsync;
    public Func<string, Task<(bool IsCancelled, ProjectFile? Project, string? Path)>> LoadAsync { get; } = loadAsync;
    public Func<string, (string? NormalizedPath, string? Code, string? Message)> ResolvePath { get; } = resolvePath;
    public ProjectPrepareReplacementAsync PrepareReplacementAsync { get; } = prepareReplacementAsync;
    public ProjectSaveThenLoadAsync SaveThenLoadAsync { get; } = saveThenLoadAsync;
    public Func<string> GetBackupPath { get; } = getBackupPath;
    public Action<string?> DeleteBackup { get; } = deleteBackup;
}

/// <summary>Session operations retain their existing state and notification owner.</summary>
internal sealed class ProjectSessionPorts(
    Func<bool> isEditingEnabled,
    Func<Func<Task<ProjectLoadCommandResult>>, Task<ProjectLoadCommandResult>> runLoadAsync,
    Func<bool> hasUnsavedWork,
    Action captureCadEdits,
    Action<bool> setDirty,
    Action<string?> setLastSavedPath,
    Action flushDeferredAppSettings)
{
    public Func<bool> IsEditingEnabled { get; } = isEditingEnabled;
    public Func<Func<Task<ProjectLoadCommandResult>>, Task<ProjectLoadCommandResult>> RunLoadAsync { get; } = runLoadAsync;
    public Func<bool> HasUnsavedWork { get; } = hasUnsavedWork;
    public Action CaptureCadEdits { get; } = captureCadEdits;
    public Action<bool> SetDirty { get; } = setDirty;
    public Action<string?> SetLastSavedPath { get; } = setLastSavedPath;
    public Action FlushDeferredAppSettings { get; } = flushDeferredAppSettings;
}

/// <summary>Status scopes, operation runners, and the original host logger.</summary>
internal sealed class ProjectOperationPorts(
    Func<string, ProjectOperationStatus> createStatus,
    ProjectRunOperationAsync runAsync,
    ProjectRunBooleanOperationAsync runBooleanAsync,
    ProjectLogMessage info,
    ProjectLogMessage debug,
    ProjectLogMessage warnFormat,
    Action<string> infoMessage,
    Action<string> warn,
    Action<Exception, string> error)
{
    public Func<string, ProjectOperationStatus> CreateStatus { get; } = createStatus;
    public ProjectRunOperationAsync RunAsync { get; } = runAsync;
    public ProjectRunBooleanOperationAsync RunBooleanAsync { get; } = runBooleanAsync;
    public ProjectLogMessage Info { get; } = info;
    public ProjectLogMessage Debug { get; } = debug;
    public ProjectLogMessage WarnFormat { get; } = warnFormat;
    public Action<string> InfoMessage { get; } = infoMessage;
    public Action<string> Warn { get; } = warn;
    public Action<Exception, string> Error { get; } = error;
}

internal sealed class ProjectOperationStatus(
    Action<string> reportSuccess,
    Action<string> reportProgress,
    Action<string> reportWarning,
    Action<string> reportBlocked,
    Action<string, Exception> reportError)
{
    public Action<string> ReportSuccess { get; } = reportSuccess;
    public Action<string> ReportProgress { get; } = reportProgress;
    public Action<string> ReportWarning { get; } = reportWarning;
    public Action<string> ReportBlocked { get; } = reportBlocked;
    public Action<string, Exception> ReportError { get; } = reportError;
}

internal enum ProjectReplacementOutcome
{
    Other,
    Cancel,
    Load,
    SaveThenLoad
}

// The outcome and message are copied from ReplaceProjectWorkflow's accepted plan.
internal readonly record struct ProjectReplacementPlan(
    ProjectReplacementOutcome Outcome, string? Path, string? BackupPath, string Message);
