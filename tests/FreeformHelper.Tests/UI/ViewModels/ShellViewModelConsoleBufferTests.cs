using System.Collections.Specialized;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using NLog;
using NLog.Config;
using Nvt.Core.Avalonia.Threading;
using Xunit;

namespace FreeformHelper.Tests;

// The legacy console text buffer is one StringBuilder. A log notification on another thread must not
// change it while the view model rebuilds the text.
[Collection("HeadlessUiSerial")]
public sealed class ShellViewModelConsoleBufferTests
{
    private static readonly DateTimeOffset _entryTime = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void ConsoleText_WhenNotificationArrivesOnAnotherThreadDuringRebuild_ShowsEachEntryOnce()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var previousConfiguration = LogManager.Configuration;
        var store = AppLogStore.Instance;
        ShellViewModel? shell = null;
        try
        {
            LogManager.Configuration = new LoggingConfiguration();
            store.MarkUiReady();
            store.Clear();
            RunPendingUiJobs();
            var initial = new AppLogEntry(_entryTime, "INFO", "test", "before rebuild");
            var added = initial with { Message = "during rebuild" };
            store.Add(initial);
            shell = new ShellViewModel { IsConsoleExpanded = false };
            RunPendingUiJobs();
            var notifier = new Thread(() => shell.OnLogEntriesChanged(
                store.Entries,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added, 1)));
            shell.ConsoleSnapshotReadForTests = () =>
            {
                shell.ConsoleSnapshotReadForTests = null;
                // The hook runs inside the gate. The store posts this add to the registered dispatcher, so the
                // writer does not need the gate and can be joined here.
                var writer = new Thread(() => store.Add(added));
                writer.Start();
                writer.Join();
                notifier.Start();
                // The notification either finishes (no guard) or waits for the rebuild (guard). Both end states
                // are reached without a fixed delay; the cap only stops a hang.
                Assert.True(SpinWait.SpinUntil(
                    () => !notifier.IsAlive || (notifier.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(30)));
            };

            shell.IsConsoleExpanded = true;
            notifier.Join();

            var expected = AppLogFormatter.FormatLine(initial) + Environment.NewLine + AppLogFormatter.FormatLine(added);
            Assert.Equal(expected, shell.ConsoleText);
            Assert.Equal(2, shell.ConsoleRenderedLineCount);
        }
        finally
        {
            shell?.Dispose();
            store.Clear();
            RunPendingUiJobs();
            LogManager.Configuration = previousConfiguration;
        }
    }

    // The store posts to the registered UI dispatcher. Run what it posted, through the Core seam.
    private static void RunPendingUiJobs()
    {
        UiThread.TryGetRunningDispatcher(out var dispatcher);
        dispatcher?.RunJobs();
    }
}
