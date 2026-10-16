// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// A shell timing probe that runs the delegates a test sets, and holds a workspace build back until the
/// task a test sets completes.
/// </summary>
internal sealed class RecordingShellTimingProbe : IShellTimingProbe
{
    /// <summary>Gets or sets the code that runs at the snapshot read of a console rebuild.</summary>
    public Action? OnConsoleSnapshotRead { get; set; }

    /// <summary>Gets or sets the code that runs when a notification finds the console text gate held.</summary>
    public Action? OnConsoleGateContended { get; set; }

    /// <summary>Gets or sets the task that a workspace build waits for before it continues.</summary>
    public Task? WorkspaceBuildPause { get; set; }

    public void ConsoleSnapshotRead() => OnConsoleSnapshotRead?.Invoke();

    public void ConsoleGateContended() => OnConsoleGateContended?.Invoke();

    public Task WorkspaceBuildPauseAsync() => WorkspaceBuildPause ?? Task.CompletedTask;
}
