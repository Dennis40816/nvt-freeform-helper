// Copyright (c) 2026 Dennis Liu. All rights reserved.
using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.Services;

/// <summary>Owns the UI-thread-only project loading flag and its guarded command registry.</summary>
internal sealed class ProjectEditingSession
{
    private readonly ProjectEditingCommands _commands;
    private readonly Action<Action> _publishAvailabilityChange;
    private bool _isLoading;

    /// <param name="publishAvailabilityChange">
    /// Wraps the flag assignment in binding notifications. It must invoke the supplied action once, synchronously.
    /// </param>
    public ProjectEditingSession(Action<Action>? publishAvailabilityChange = null)
    {
        _publishAvailabilityChange = publishAvailabilityChange ?? (change => change());
        _commands = new ProjectEditingCommands(() => IsProjectEditingEnabled);
    }

    public bool IsProjectEditingEnabled => !_isLoading;

    public RelayCommand Create(Action execute, Func<bool>? canExecute = null) => _commands.Create(execute, canExecute);

    public RelayCommand<T> Create<T>(Action<T?> execute) => _commands.Create(execute);

    public AsyncRelayCommand CreateAsyncCommand(Func<Task> execute) => _commands.CreateAsyncCommand(execute);

    public AsyncRelayCommand CreateAsyncCommand(Func<Task<bool>> execute) => _commands.CreateAsyncCommand(execute);

    /// <summary>Runs an admitted load; callers must check availability before entering.</summary>
    public async Task<T> RunLoadAsync<T>(Func<Task<T>> load)
    {
        SetLoading(true);
        try
        {
            return await load();
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        if (_isLoading == isLoading) return;

        // The binding publisher can raise PropertyChanging before the assignment and PropertyChanged after it.
        _publishAvailabilityChange(() => _isLoading = isLoading);
        _commands.NotifyAvailabilityChanged();
    }
}
