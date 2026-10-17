// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Starts the background work like the production runner and keeps every started task by name,
/// so a test can await the work and see its failure.
/// </summary>
internal sealed class RecordingBackgroundWorkRunner : IBackgroundWorkRunner
{
    private readonly Lock _gate = new();
    private readonly List<(string Name, Task Work)> _started = [];

    public void Run(string name, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var started = work();
        lock (_gate)
        {
            _started.Add((name, started));
        }
    }

    public int StartedCount(string name)
    {
        lock (_gate)
        {
            return _started.Count(entry => entry.Name == name);
        }
    }

    // A task that completes like the last work of that name and fails with its exception. WhenAll wraps the
    // stored task so the caller awaits a task this method hands out, not a field.
    public Task LastStartedAsync(string name)
    {
        Task? found = null;
        lock (_gate)
        {
            for (var index = _started.Count - 1; index >= 0 && found is null; index--)
            {
                if (_started[index].Name == name)
                {
                    found = _started[index].Work;
                }
            }
        }

        return found is null
            ? throw new InvalidOperationException($"No background work named '{name}' was started.")
            : Task.WhenAll(found);
    }
}
