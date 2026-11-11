using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.ViewModels.Project;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly ProjectSessionState _projectSession;

    internal ProjectSessionState ProjectSession => _projectSession;

    public ProjectSessionMode SessionMode => _projectSession.SessionMode;

    public bool IsProjectEditingEnabled => _projectSession.IsProjectEditingEnabled;

    // True while settings or a project load apply values. Derived from the counter; there is no second copy.
    // The old field name is kept on purpose: members that carry analyzer debt (the RebuildGridAsync async lambda
    // and the FilterCadPadsByLayer try statement) read it, and renaming it would change their ledger fingerprints.
    private bool _isLoadingSettings => _settingsSuppression.IsSettingsLoadingActive;

    private void PublishProjectEditingAvailabilityChange(Action changeMode)
    {
        _projectSession.PublishEditingAvailabilityChange(changeMode);
        DxfEdit.NotifyEditingAvailabilityChanged();
    }

    private Task<ProjectLoadCommandResult> RunProjectLoadAsync(Func<Task<ProjectLoadCommandResult>> load)
        => _projectSession.RunLoadAsync(load);

    private RelayCommand CreateEditingCommand(Action execute, Func<bool>? canExecute = null)
        => _projectEditingSession.Create(execute, canExecute);

    private RelayCommand<T> CreateEditingCommand<T>(Action<T?> execute)
        => _projectEditingSession.Create(execute);

    private AsyncRelayCommand CreateEditingAsyncCommand(Func<Task> execute)
        => _projectEditingSession.CreateAsyncCommand(execute);

    private AsyncRelayCommand CreateEditingAsyncCommand(Func<Task<bool>> execute)
        => _projectEditingSession.CreateAsyncCommand(execute);

    // --- Project State Flags ---

    /// <summary>
    /// Gets or sets a value indicating whether the current project has unsaved changes.
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => _projectSession.HasUnsavedChanges;
        set => _projectSession.HasUnsavedChanges = value;
    }

    /// <summary>
    /// Gets the command to save the current project.
    /// </summary>
    public IAsyncRelayCommand SaveProjectCommand => _projectCommands.SaveProjectCommand;

    /// <summary>
    /// Gets the command to load an existing project.
    /// </summary>
    public IAsyncRelayCommand LoadProjectCommand => _projectCommands.LoadProjectCommand;

    private readonly ProjectCommandOwner _projectCommands;
}
