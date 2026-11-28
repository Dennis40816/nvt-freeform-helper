// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels.Project;

/// <summary>Owns document identity and save history for one workspace session.</summary>
internal sealed class ProjectSessionState(
    ProjectFile document,
    Func<bool> hasCad,
    Func<bool> isEditingEnabled,
    Func<Func<Task<ProjectLoadCommandResult>>, Task<ProjectLoadCommandResult>> runLoadAsync,
    Action<string> changing,
    Action<string> changed)
{
    private readonly Func<bool> _hasCad = hasCad;
    private readonly Func<bool> _isEditingEnabled = isEditingEnabled;
    private readonly Func<Func<Task<ProjectLoadCommandResult>>, Task<ProjectLoadCommandResult>> _runLoadAsync = runLoadAsync;
    private readonly Action<string> _changing = changing;
    private readonly Action<string> _changed = changed;

    // UI thread only. These three values change independently during load, save, and DXF opening.
    private bool _hasUnsavedChanges;

    public ProjectFile Document { get; set; } = document;

    public string? LastSavedPath { get; set; }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            if (_hasUnsavedChanges == value) return;

            _changing(nameof(HasUnsavedChanges));
            _hasUnsavedChanges = value;
            _changed(nameof(HasUnsavedChanges));
        }
    }

    public ProjectSessionMode SessionMode
        => IsProjectEditingEnabled ? ProjectSessionMode.Ready : ProjectSessionMode.LoadingProject;

    public bool IsProjectEditingEnabled => _isEditingEnabled();

    public bool ShouldPromptSaveOnExit()
    {
        return ProjectClosePolicy.ShouldPromptSave(HasUnsavedChanges, _hasCad(), LastSavedPath);
    }

    // A fresh session can be dirty after the View applies settings. Replacement also needs session content.
    public bool HasUnsavedProjectWork()
    {
        return ShouldPromptSaveOnExit() && (_hasCad() || !string.IsNullOrWhiteSpace(LastSavedPath));
    }

    public void PublishEditingAvailabilityChange(Action changeMode)
    {
        _changing(nameof(SessionMode));
        changeMode();
        _changed(nameof(SessionMode));
        _changed(nameof(IsProjectEditingEnabled));
    }

    public Task<ProjectLoadCommandResult> RunLoadAsync(Func<Task<ProjectLoadCommandResult>> load)
    {
        if (!IsProjectEditingEnabled)
        {
            return Task.FromResult(ProjectLoadCommandResult.Failed("BUSY", "A project is already loading."));
        }

        return _runLoadAsync(load);
    }
}
