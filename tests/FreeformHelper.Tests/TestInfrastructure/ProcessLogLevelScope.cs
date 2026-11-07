// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.Logging;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Gives a test a known minimum log level and restores the previous one when the test ends.
/// The level is a static of the process. A view model reads it when it is created, and a project load
/// compares the saved level with it, so a test that changes the level and leaves it changed alters
/// the start state of every test after it.
/// </summary>
internal sealed class ProcessLogLevelScope : IDisposable
{
    private readonly string _previousLevel = LoggingBootstrapper.GetCurrentMinimumLevelName();

    public ProcessLogLevelScope(string startLevel)
    {
        LoggingBootstrapper.ApplyMinimumLevel(startLevel);
    }

    public void Dispose()
    {
        LoggingBootstrapper.ApplyMinimumLevel(_previousLevel);
    }
}
