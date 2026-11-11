// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Diagnostics;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels.Project;

/// <summary>Sequences save, backup, replacement, and load without storing operation state.</summary>
internal sealed class ProjectPersistenceCoordinator(
    ProjectPersistencePorts persistence,
    ProjectSessionPorts session,
    ProjectOperationPorts operations,
    Func<ProjectFile, string, long, Task<ProjectLoadApplyTiming>> applyAsync)
{
    private readonly ProjectPersistencePorts _persistence = persistence;
    private readonly ProjectSessionPorts _session = session;
    private readonly ProjectOperationPorts _operations = operations;
    private readonly Func<ProjectFile, string, long, Task<ProjectLoadApplyTiming>> _applyAsync = applyAsync;

    public async Task<bool> SaveProjectAsync()
    {
        if (!_session.IsEditingEnabled()) return false;

        var status = _operations.CreateStatus("SaveProject");
        if (_persistence.GetSavePicker() is null)
        {
            status.ReportBlocked("Save project: dialog handler not wired.");
            return false;
        }

        return await _operations.RunBooleanAsync(
            operationName: "Save project",
            showModalSpinner: false,
            onStart: () => _operations.Info("Saving project."),
            operationAsync: async () =>
            {
                _session.CaptureCadEdits();
                var result = await _persistence.SaveAsync(_persistence.GetSavePicker()!, _persistence.GetEmbedConfirmation());
                if (result.IsCancelled)
                {
                    return false;
                }

                var successMessage = result.EmbedWarning is null
                    ? $"Project saved: {result.Path}"
                    : $"Project saved: {result.Path}. {result.EmbedWarning}";
                status.ReportSuccess(successMessage);
                _operations.Info("Project saved: {0}", result.Path);
                if (!string.IsNullOrWhiteSpace(result.EmbedWarning))
                {
                    _operations.Warn(result.EmbedWarning);
                }

                _session.SetDirty(false);
                _session.SetLastSavedPath(result.Path);
                _session.FlushDeferredAppSettings();
                return true;
            },
            onError: ex =>
            {
                status.ReportError("Save project failed", ex);
                _operations.Error(ex, "Save project failed.");
                return false;
            });
    }

    public async Task LoadProjectAsync()
    {
        ProjectReplacementPlan? plan = null;
        _ = await _session.RunLoadAsync(async () =>
        {
            var status = _operations.CreateStatus("LoadProject");
            if (_persistence.GetLoadPicker() is null)
            {
                status.ReportBlocked("Load project: dialog handler not wired.");
                return ProjectLoadCommandResult.Failed("LOAD_FAILED", "Load project: dialog handler not wired.");
            }

            var prepared = await _persistence.PrepareReplacementAsync(
                _persistence.GetLoadPicker()!, _session.HasUnsavedWork, TryBackUpUnsavedProjectAsync);
            plan = prepared;
            if (prepared.Outcome != ProjectReplacementOutcome.Load)
            {
                if (prepared.Outcome == ProjectReplacementOutcome.Cancel && prepared.Path is not null)
                {
                    status.ReportBlocked(prepared.Message);
                }

                return ProjectLoadCommandResult.Failed("CANCELLED", prepared.Message, prepared.Path);
            }

            return await LoadProjectFromPathCoreAsync(prepared.Path!);
        });

        if (plan is not { Outcome: ProjectReplacementOutcome.SaveThenLoad } savePlan)
        {
            return;
        }

        if (!await _persistence.SaveThenLoadAsync(
            savePlan.Path!, savePlan.BackupPath, SaveProjectAsync, _persistence.DeleteBackup,
            async path => { _ = await LoadProjectFromPathAsync(path); }))
        {
            _operations.CreateStatus("LoadProject").ReportBlocked("Load cancelled: the project was not saved, so unsaved changes were kept.");
        }
    }

    // Backup mutates the current document, and failure still allows the replacement prompt.
    private async Task<string?> TryBackUpUnsavedProjectAsync()
    {
        var backupPath = _persistence.GetBackupPath();
        try
        {
            _session.CaptureCadEdits();
            _ = await _persistence.SaveAsync(() => Task.FromResult<string?>(backupPath), null);
            _operations.Info("Unsaved project backed up: {0}", backupPath);
            return backupPath;
        }
        catch (IOException ex)
        {
            return ReportBackupFailed(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ReportBackupFailed(ex);
        }
        catch (InvalidOperationException ex)
        {
            // The store refuses settings that fail validation, and the backup must not block the prompt.
            return ReportBackupFailed(ex);
        }
    }

    private string? ReportBackupFailed(Exception ex)
    {
        _operations.WarnFormat("Unsaved project backup failed: {0}", ex.Message);
        _operations.CreateStatus("LoadProject").ReportWarning($"Could not back up the unsaved project: {ex.Message}");
        return null;
    }

    public Task<ProjectLoadCommandResult> LoadProjectFromPathAsync(string path)
    {
        return _session.RunLoadAsync(() => LoadProjectFromPathCoreAsync(path));
    }

    // Every caller publishes the same rejection to the status bar.
    private ProjectLoadCommandResult ReportLoadRejected(string code, string message, string? path)
    {
        _operations.CreateStatus("LoadProject").ReportBlocked(message);
        return ProjectLoadCommandResult.Failed(code, message, path);
    }

    private async Task<ProjectLoadCommandResult> LoadProjectFromPathCoreAsync(string path)
    {
        var status = _operations.CreateStatus("LoadProject");
        var resolution = _persistence.ResolvePath(path);
        if (resolution.Code is not null)
        {
            return ReportLoadRejected(resolution.Code, resolution.Message!, resolution.NormalizedPath);
        }

        var normalizedPath = resolution.NormalizedPath!;
        var commandResult = ProjectLoadCommandResult.Failed(
            code: "LOAD_FAILED",
            message: $"Load project failed: {normalizedPath}",
            path: normalizedPath);

        await _operations.RunAsync(
            operationName: "Load project",
            showModalSpinner: true,
            onStart: () =>
            {
                _operations.Info("Loading project from path: {0}", normalizedPath);
                _operations.Debug("PERF LOAD_PROJECT: stage=start path={0}.", normalizedPath);
            },
            operationAsync: async () =>
            {
                var loadSw = Stopwatch.StartNew();
                var result = await _persistence.LoadAsync(normalizedPath);
                loadSw.Stop();

                if (result.IsCancelled || result.Project is null || string.IsNullOrWhiteSpace(result.Path))
                {
                    _operations.Debug("PERF LOAD_PROJECT: stage=cancelled path={0}.", normalizedPath);
                    commandResult = ProjectLoadCommandResult.Failed(
                        code: "CANCELLED",
                        message: "Load cancelled (pending edits were not committed).",
                        path: normalizedPath);
                    return;
                }

                var timings = await _applyAsync(result.Project, result.Path, loadSw.ElapsedMilliseconds);
                commandResult = ProjectLoadCommandResult.Loaded(
                    path: result.Path,
                    totalElapsedMs: timings.TotalElapsedMs,
                    persistenceElapsedMs: loadSw.ElapsedMilliseconds,
                    applyStateElapsedMs: timings.ApplyStateElapsedMs,
                    dxfElapsedMs: timings.DxfElapsedMs,
                    rebuildElapsedMs: timings.RebuildElapsedMs,
                    step1ReplayElapsedMs: timings.Step1ReplayElapsedMs,
                    step2ReplayElapsedMs: timings.Step2ReplayElapsedMs,
                    step1Replayed: timings.Step1Replayed,
                    step2Replayed: timings.Step2Replayed);
            },
            onError: ex =>
            {
                status.ReportError("Load project failed", ex);
                _operations.Error(ex, "Load project failed.");
                commandResult = ProjectLoadCommandResult.Failed("LOAD_FAILED", ex.Message, normalizedPath);
            });

        return commandResult;
    }
}
