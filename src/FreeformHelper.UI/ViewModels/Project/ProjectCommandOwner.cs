// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels.Project;

/// <summary>Keeps the save and load commands registered once for the workspace lifetime.</summary>
internal sealed class ProjectCommandOwner
{
    public ProjectCommandOwner(
        Func<Func<Task<bool>>, AsyncRelayCommand> createSaveCommand,
        Func<Func<Task>, AsyncRelayCommand> createLoadCommand,
        Func<Task<bool>> saveAsync,
        Func<Task> loadAsync)
    {
        SaveProjectCommand = createSaveCommand(saveAsync);
        LoadProjectCommand = createLoadCommand(loadAsync);
    }

    public IAsyncRelayCommand SaveProjectCommand { get; }

    public IAsyncRelayCommand LoadProjectCommand { get; }

    public Task<bool>? TryStartSaveAsync()
    {
        if (SaveProjectCommand.IsRunning || !SaveProjectCommand.CanExecute(null))
        {
            return null;
        }

        // The save command preserves the Task<bool> returned by the existing save operation.
        return (Task<bool>)SaveProjectCommand.ExecuteAsync(null);
    }
}
