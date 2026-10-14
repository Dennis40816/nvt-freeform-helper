// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using Xunit;

namespace FreeformHelper.Tests;

[CollectionDefinition("AppContextBaseDirectorySerial", DisableParallelization = true)]
public sealed class AppContextBaseDirectoryCollectionDefinition;

/// <summary>
/// Pins what logging setup does when the log directory cannot be created (issue #64, inventory
/// finding SHELL-D06). The log directory sits four levels above the application base directory.
/// <c>Configure</c> creates it before NLog loads any configuration, and a failure goes straight
/// to the caller. <c>Program.Main</c> calls <c>Configure</c> before it registers the exception
/// handlers and before its startup try block, so the failure ends the process before any handler
/// or fallback sink exists. This test describes the current result, not the wanted one. A fix
/// that falls back to another location changes the expected result here and must say so in its
/// pull request.
/// </summary>
[Collection("AppContextBaseDirectorySerial")]
public sealed class LoggingBootstrapperUnwritableDirectoryCharacterizationTests
{
    private const string BaseDirectoryKey = "APP_CONTEXT_BASE_DIRECTORY";

    [Fact]
    public void Configure_LogDirectoryCannotBeCreated_ThrowsToTheCaller()
    {
        var root = TestFiles.CreateTempDirectory();
        var originalBaseDirectory = AppContext.GetData(BaseDirectoryKey);
        try
        {
            // The log directory is <base>/../../../../logs, which is <root>/logs for this base.
            var blockingEntry = Path.Combine(root.FullName, "logs");
            File.Create(blockingEntry).Dispose();
            var baseDirectory = Path.Combine(root.FullName, "a", "b", "c", "d") + Path.DirectorySeparatorChar;
            AppContext.SetData(BaseDirectoryKey, baseDirectory);

            var failure = Record.Exception(LoggingBootstrapper.Configure);

            // The failure is an IO exception from creating the directory. No handler catches it.
            Assert.IsAssignableFrom<IOException>(failure);
            Assert.True(File.Exists(blockingEntry));
        }
        finally
        {
            AppContext.SetData(BaseDirectoryKey, originalBaseDirectory);
            root.Delete(recursive: true);
        }
    }
}
