// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;

namespace FreeformHelper.Tests;

/// <summary>Holds replacement work at the existing runner seam while cancelled work finishes.</summary>
internal sealed class PadInspectorWorkQueue : IBackgroundWorkRunner
{
    private readonly RecordingBackgroundWorkRunner _started = new();
    private readonly Queue<(string Name, Func<Task> Work)> _queued = new();

    public bool HoldNewWork { get; set; }

    public int QueuedCount => _queued.Count;

    public void Run(string name, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (HoldNewWork)
        {
            _queued.Enqueue((name, work));
            return;
        }

        _started.Run(name, work);
    }

    public void StartQueued()
    {
        while (_queued.TryDequeue(out var entry))
        {
            _started.Run(entry.Name, entry.Work);
        }
    }

    public Task LastStartedAsync(string name) => _started.LastStartedAsync(name);
}
