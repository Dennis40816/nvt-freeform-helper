using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly List<IRelayCommand> _projectSessionCommands = [];
    private ProjectSessionMode _sessionMode = ProjectSessionMode.Ready;

    public ProjectSessionMode SessionMode
    {
        get => _sessionMode;
        private set
        {
            if (!SetProperty(ref _sessionMode, value)) return;

            OnPropertyChanged(nameof(IsProjectEditingEnabled));
            foreach (var command in _projectSessionCommands)
            {
                command.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsProjectEditingEnabled => SessionMode == ProjectSessionMode.Ready;

    // True while settings or a project load apply values. Derived from the counter; there is no second copy.
    // The old field name is kept on purpose: members that carry analyzer debt (the RebuildGridAsync async lambda
    // and the FilterCadPadsByLayer try statement) read it, and renaming it would change their ledger fingerprints.
    private bool _isLoadingSettings => _settingsSuppression.IsSettingsLoadingActive;

    private async Task<ProjectLoadCommandResult> RunProjectLoadAsync(Func<Task<ProjectLoadCommandResult>> load)
    {
        if (!IsProjectEditingEnabled)
        {
            return ProjectLoadCommandResult.Failed("BUSY", "A project is already loading.");
        }

        SessionMode = ProjectSessionMode.LoadingProject;
        try
        {
            return await load();
        }
        finally
        {
            SessionMode = ProjectSessionMode.Ready;
        }
    }

    private bool CanEditProject() => IsProjectEditingEnabled;

    private RelayCommand CreateEditingCommand(Action execute, Func<bool>? canExecute = null)
    {
        bool CanExecute() => IsProjectEditingEnabled && (canExecute?.Invoke() ?? true);

        var command = new RelayCommand(() =>
        {
            if (CanExecute()) execute();
        }, CanExecute);
        _projectSessionCommands.Add(command);
        return command;
    }

    private RelayCommand<T> CreateEditingCommand<T>(Action<T?> execute)
    {
        var command = new RelayCommand<T>(parameter =>
        {
            if (IsProjectEditingEnabled) execute(parameter);
        }, _ => IsProjectEditingEnabled);
        _projectSessionCommands.Add(command);
        return command;
    }

    private AsyncRelayCommand CreateEditingAsyncCommand(Func<Task> execute)
    {
        var command = new AsyncRelayCommand(
            () => IsProjectEditingEnabled ? execute() : Task.CompletedTask,
            CanEditProject);
        _projectSessionCommands.Add(command);
        return command;
    }

    private AsyncRelayCommand CreateEditingAsyncCommand(Func<Task<bool>> execute)
    {
        // Preserve the result task so shortcut callers can display the result of this execution.
        var command = new AsyncRelayCommand(
            () => IsProjectEditingEnabled ? execute() : Task.FromResult(false),
            CanEditProject);
        _projectSessionCommands.Add(command);
        return command;
    }
}
