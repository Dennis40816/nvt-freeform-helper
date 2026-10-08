using System.ComponentModel;
using Avalonia.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

/// <summary>Projects the project session's editing availability into an owned window.</summary>
public sealed class ProjectEditingCommands : ObservableObject
{
    private readonly List<IRelayCommand> _commands = [];
    private FreeformHelperViewModel? _project;

    public bool IsProjectEditingEnabled => _project?.IsProjectEditingEnabled ?? true;

    internal void AttachProject(FreeformHelperViewModel? project)
    {
        if (ReferenceEquals(_project, project)) return;
        if (_project is not null)
        {
            WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, ProjectEditingCommands>(
                _project, nameof(_project.PropertyChanged), OnProjectPropertyChanged);
        }

        _project = project;
        if (_project is not null)
        {
            WeakEventHandlerManager.Subscribe<FreeformHelperViewModel, PropertyChangedEventArgs, ProjectEditingCommands>(
                _project, nameof(_project.PropertyChanged), OnProjectPropertyChanged);
        }

        NotifyAvailabilityChanged();
    }

    internal RelayCommand Create(Action execute, Func<bool>? canExecute = null)
    {
        bool CanExecute() => IsProjectEditingEnabled && (canExecute?.Invoke() ?? true);
        var command = new RelayCommand(() =>
        {
            if (CanExecute()) execute();
        }, CanExecute);
        _commands.Add(command);
        return command;
    }

    internal RelayCommand<T> Create<T>(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        bool CanExecute(T? parameter) => IsProjectEditingEnabled && (canExecute?.Invoke(parameter) ?? true);
        var command = new RelayCommand<T>(parameter =>
        {
            if (CanExecute(parameter)) execute(parameter);
        }, CanExecute);
        _commands.Add(command);
        return command;
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FreeformHelperViewModel.IsProjectEditingEnabled)) NotifyAvailabilityChanged();
    }

    private void NotifyAvailabilityChanged()
    {
        OnPropertyChanged(nameof(IsProjectEditingEnabled));
        foreach (var command in _commands) command.NotifyCanExecuteChanged();
    }
}
