using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public ProjectSessionMode SessionMode
        => IsProjectEditingEnabled ? ProjectSessionMode.Ready : ProjectSessionMode.LoadingProject;

    public bool IsProjectEditingEnabled => _projectEditingSession.IsProjectEditingEnabled;

    // True while settings or a project load apply values. Derived from the counter; there is no second copy.
    // The old field name is kept on purpose: members that carry analyzer debt (the RebuildGridAsync async lambda
    // and the FilterCadPadsByLayer try statement) read it, and renaming it would change their ledger fingerprints.
    private bool _isLoadingSettings => _settingsSuppression.IsSettingsLoadingActive;

    private void PublishProjectEditingAvailabilityChange(Action changeMode)
    {
        OnPropertyChanging(nameof(SessionMode));
        changeMode();
        OnPropertyChanged(nameof(SessionMode));
        OnPropertyChanged(nameof(IsProjectEditingEnabled));
    }

    private Task<ProjectLoadCommandResult> RunProjectLoadAsync(Func<Task<ProjectLoadCommandResult>> load)
    {
        if (!IsProjectEditingEnabled)
        {
            return Task.FromResult(ProjectLoadCommandResult.Failed("BUSY", "A project is already loading."));
        }

        return _projectEditingSession.RunLoadAsync(load);
    }

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
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>
    /// Gets the command to save the current project.
    /// </summary>
    public IAsyncRelayCommand SaveProjectCommand => _projectCommands.SaveProjectCommand;

    /// <summary>
    /// Gets the command to load an existing project.
    /// </summary>
    public IAsyncRelayCommand LoadProjectCommand => _projectCommands.LoadProjectCommand;

    private readonly ProjectCommands _projectCommands;

    /// <summary>Creates the project commands once during construction.</summary>
    private ProjectCommands CreateProjectCommands()
    {
        return new ProjectCommands(
            SaveProjectCommand: CreateEditingAsyncCommand(SaveProjectAsync),
            LoadProjectCommand: CreateEditingAsyncCommand(LoadProjectAsync));
    }
}
