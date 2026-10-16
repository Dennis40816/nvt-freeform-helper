using System.Collections.Specialized;
using System.ComponentModel;
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
// change it while the view model rebuilds the text, and the view model must not hold the gate while it
// calls subscribers.
[Collection("HeadlessUiSerial")]
public sealed class ShellViewModelConsoleBufferTests
{
    // Only stops a hang. Every test below reaches its end state through an event, not through this time.
    private static readonly TimeSpan _hangCap = TimeSpan.FromSeconds(30);
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
            var probe = new RecordingShellTimingProbe();
            shell = new ShellViewModel(null, probe) { IsConsoleExpanded = false };
            RunPendingUiJobs();
            using var contended = new ManualResetEventSlim();
            var notifier = new Thread(() => shell.OnLogEntriesChanged(
                store.Entries,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added, 1)));
            probe.OnConsoleGateContended = contended.Set;
            probe.OnConsoleSnapshotRead = () =>
            {
                probe.OnConsoleSnapshotRead = null;
                // The hook runs inside the gate. The store posts this add to the registered dispatcher, so the
                // writer does not need the gate. The writer is joined with a bound, so a missing dispatcher
                // fails the test instead of hanging it.
                var writer = new Thread(() => store.Add(added));
                writer.Start();
                Assert.True(writer.Join(_hangCap), "The log write did not finish. Is a UI dispatcher registered?");
                notifier.Start();
                // The notifier signals just before it waits for the gate. It can only do that while the rebuild
                // holds the gate, so the notification is in the middle of the rebuild when this returns.
                Assert.True(contended.WaitHandle.WaitOne(_hangCap), "The notification did not wait for the gate of the rebuild.");
            };

            shell.IsConsoleExpanded = true;
            Assert.True(notifier.Join(_hangCap), "The notification did not finish after the rebuild.");

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

    [AvaloniaFact]
    public void ConsoleText_WhenSubscriberIsNotified_DoesNotHoldTheGate()
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
            store.Add(new AppLogEntry(_entryTime, "INFO", "test", "one line"));
            shell = new ShellViewModel { IsConsoleExpanded = false };
            RunPendingUiJobs();
            // A line the collapsed console did not render, so expanding changes the text.
            store.Add(new AppLogEntry(_entryTime, "INFO", "test", "second line"));
            RunPendingUiJobs();
            var probe = new GateProbe(shell, store, _hangCap);
            shell.PropertyChanged += probe.OnPropertyChanged;

            shell.IsConsoleExpanded = true;

            // The other thread takes the gate while the subscriber runs. It cannot do that if the view model
            // raises the notification inside the gate.
            Assert.Equal(GateProbeResult.TookGate, probe.Outcome);
        }
        finally
        {
            shell?.Dispose();
            store.Clear();
            RunPendingUiJobs();
            LogManager.Configuration = previousConfiguration;
        }
    }

    [AvaloniaFact]
    public void ConsoleSummaryText_WhenConsoleIsRebuilt_ShowsBothCountsOfTheRebuild()
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
            store.Add(new AppLogEntry(_entryTime, "INFO", "test", "first"));
            store.Add(new AppLogEntry(_entryTime, "INFO", "test", "second"));
            shell = new ShellViewModel { IsConsoleExpanded = false };
            RunPendingUiJobs();

            shell.IsConsoleExpanded = true;

            Assert.Equal("Lines 2", shell.ConsoleSummaryText);
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
        Assert.True(UiThread.TryGetRunningDispatcher(out var dispatcher), "No UI dispatcher is registered.");
        dispatcher!.RunJobs();
    }

    private enum GateProbeResult
    {
        NotRun,
        TookGate,
        BlockedOnGate,
    }

    // On the first console text notification, another thread calls the notification handler. That call
    // needs the gate. It finishes only when the notifying thread does not hold the gate.
    private sealed class GateProbe(ShellViewModel shell, AppLogStore store, TimeSpan cap)
    {
        public GateProbeResult Outcome { get; private set; }

        public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ShellViewModel.ConsoleText) || Outcome != GateProbeResult.NotRun)
            {
                return;
            }

            var other = new Thread(() => shell.OnLogEntriesChanged(
                store.Entries,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
            Outcome = GateProbeResult.BlockedOnGate;
            other.Start();
            if (other.Join(cap))
            {
                Outcome = GateProbeResult.TookGate;
            }
        }
    }
}
