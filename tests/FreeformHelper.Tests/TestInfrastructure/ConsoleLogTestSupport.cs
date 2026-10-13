// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using FreeformHelper.UI.Logging;
using Nvt.Core.Avalonia.LogConsole;
using Nvt.Core.LogConsole;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Helpers for tests that write to the app console store and read what the console controller shows.
/// The store is process-wide, so tests use their own source IDs and filter by them.
/// </summary>
internal static class ConsoleLogTestSupport
{
    private static readonly DateTimeOffset _fixedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // A failure guard for a projection that never arrives. It is not a time that a test waits for.
    private static readonly TimeSpan _projectionGuard = TimeSpan.FromSeconds(30);

    public static void Write(string sourceId, string message, LogLevel level = LogLevel.Info)
    {
        AppConsoleLog.Write(_fixedTime, level, sourceId, message);
    }

    /// <summary>
    /// Creates a store of its own with the given lines and waits until the store has published them.
    /// The caller owns the store and disposes it. A window test passes it to the shell, so the test
    /// never clears the process-wide store that other tests write to.
    /// </summary>
    public static async Task<LogStore> CreateSeededStoreAsync(string sourceId, params string[] messages)
    {
        var store = new LogStore();
        foreach (var message in messages)
        {
            store.Add(LogLevel.Info, sourceId, message, _fixedTime);
        }

        using var snapshot = await store.CaptureLatestAsync();
        return store;
    }

    public static async Task<LogSnapshot> CaptureAsync()
    {
        return await AppConsoleLog.Store.CaptureLatestAsync();
    }

    public static IReadOnlyList<string> RowTexts(ConsoleProjection projection, string? sourceId = null)
    {
        var texts = new List<string>();
        foreach (var row in projection.Rows)
        {
            if (sourceId is not null && !string.Equals(row.SourceId, sourceId, StringComparison.Ordinal))
            {
                continue;
            }

            texts.Add(Read(row.TextContent));
        }

        return texts;
    }

    public static string Read(ILogTextContent content)
    {
        return string.Create(content.Length, content, static (span, value) => value.Read(0, span));
    }

    /// <summary>
    /// Waits until the controller publishes a projection that satisfies <paramref name="predicate"/>.
    /// Call it on the UI thread. The wait ends when the controller raises its change notification.
    /// </summary>
    public static async Task WaitForProjectionAsync(ConsoleController controller, Func<ConsoleProjection, bool> predicate)
    {
        if (predicate(controller.Projection))
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (predicate(controller.Projection))
            {
                completion.TrySetResult();
            }
        }

        controller.PropertyChanged += OnChanged;
        try
        {
            await completion.Task.WaitAsync(_projectionGuard);
        }
        finally
        {
            controller.PropertyChanged -= OnChanged;
        }
    }
}
