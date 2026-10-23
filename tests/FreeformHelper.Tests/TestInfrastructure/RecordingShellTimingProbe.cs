// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// A shell timing probe that holds a workspace build back until the task a test sets completes.
/// </summary>
internal sealed class RecordingShellTimingProbe : IShellTimingProbe
{
    /// <summary>Gets or sets the task that a workspace build waits for before it continues.</summary>
    public Task? WorkspaceBuildPause { get; set; }

    public Task WorkspaceBuildPauseAsync() => WorkspaceBuildPause ?? Task.CompletedTask;
}
