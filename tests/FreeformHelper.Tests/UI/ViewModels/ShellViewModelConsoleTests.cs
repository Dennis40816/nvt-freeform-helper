using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using NLog;
using NLog.Config;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShellViewModelConsoleTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConsoleText_WhenAddNotificationsFollowRingSnapshot_ShowsEachEntryOnce(bool expandExistingShell)
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
            Dispatcher.UIThread.RunJobs();
            if (expandExistingShell)
            {
                shell = new ShellViewModel { IsConsoleExpanded = false };
            }

            var entry = new AppLogEntry(DateTimeOffset.UtcNow, "INFO", "test", "queued snapshot entry");
            var writer = new Thread(() =>
            {
                store.Add(entry);
                store.Add(entry);
            });
            writer.Start();
            writer.Join();
            Assert.Empty(store.Entries);

            shell ??= new ShellViewModel();
            shell.IsConsoleExpanded = true;
            var expected = string.Join(Environment.NewLine, AppLogFormatter.FormatLine(entry), AppLogFormatter.FormatLine(entry));
            Assert.Equal(expected, shell.ConsoleText);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(expected, shell.ConsoleText);
            Assert.Equal(2, shell.ConsoleSourceLineCount);
            Assert.Equal(2, shell.ConsoleRenderedLineCount);

            var next = entry with { Message = "after snapshot catch-up" };
            store.Add(next);
            Assert.Equal(expected + Environment.NewLine + AppLogFormatter.FormatLine(next), shell.ConsoleText);
            Assert.Equal(3, shell.ConsoleSourceLineCount);
            Assert.Equal(3, shell.ConsoleRenderedLineCount);
        }
        finally
        {
            try
            {
                shell?.Dispose();
                store.Clear();
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                LogManager.Configuration = previousConfiguration;
            }
        }
    }

    [AvaloniaFact]
    public void ConsoleText_WhenBackgroundAddOccursBetweenCountAndSnapshot_ShowsEachEntryOnceInSimulation()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var previousConfiguration = LogManager.Configuration;
        var store = AppLogStore.Instance;
        ShellViewModel? shell = null;
        ConsolePanel? panel = null;
        try
        {
            LogManager.Configuration = new LoggingConfiguration();
            store.MarkUiReady();
            store.Clear();
            Dispatcher.UIThread.RunJobs();
            var initial = new AppLogEntry(DateTimeOffset.UtcNow, "INFO", "test", "before snapshot read");
            var added = initial with { Message = "during snapshot read" };
            store.Add(initial);
            shell = new ShellViewModel { IsConsoleExpanded = false };
            Dispatcher.UIThread.RunJobs();
            panel = new ConsolePanel { UseShellHostedBehavior = true, DataContext = shell };
            var editor = panel.FindControl<TextEditor>("ConsoleEditor");
            Assert.NotNull(editor);
            shell.ConsoleSnapshotReadForTests = () =>
            {
                shell.ConsoleSnapshotReadForTests = null;
                Assert.Equal(1, store.GetTotalCount());
                var writer = new Thread(() => store.Add(added));
                writer.Start();
                writer.Join();
                Assert.Single(store.Entries);
                Assert.Equal(2, store.GetTotalCount());
            };

            shell.IsConsoleExpanded = true;
            var expected = AppLogFormatter.FormatLine(initial) + Environment.NewLine + AppLogFormatter.FormatLine(added);
            Assert.Equal(expected, shell.ConsoleText);
            Assert.Equal(expected, editor.Text);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(expected, shell.ConsoleText);
            Assert.Equal(expected, editor.Text);
            Assert.Equal(2, shell.ConsoleSourceLineCount);
            Assert.Equal(2, shell.ConsoleRenderedLineCount);

            var next = initial with { Message = "after snapshot notification" };
            store.Add(next);
            Assert.Equal(expected + Environment.NewLine + AppLogFormatter.FormatLine(next), shell.ConsoleText);
            Assert.Equal(shell.ConsoleText, editor.Text);
            Assert.Equal(3, shell.ConsoleSourceLineCount);
            Assert.Equal(3, shell.ConsoleRenderedLineCount);
        }
        finally
        {
            try
            {
                if (panel is not null)
                {
                    panel.DataContext = null;
                }

                shell?.Dispose();
                store.Clear();
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                LogManager.Configuration = previousConfiguration;
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dispose_StopsReceivingGlobalLogEntries(bool isConsoleExpanded)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var previousConfiguration = LogManager.Configuration;
        var store = AppLogStore.Instance;
        try
        {
            LogManager.Configuration = new LoggingConfiguration();
            store.MarkUiReady();
            store.Clear();
            Dispatcher.UIThread.RunJobs();
            using var shell = new ShellViewModel
            {
                IsConsoleExpanded = isConsoleExpanded,
                IsConsoleDedupEnabled = false,
            };
            var sourceCountBefore = shell.ConsoleSourceLineCount;
            store.Add(new AppLogEntry(DateTimeOffset.UtcNow, "INFO", "test", "before dispose"));
            Dispatcher.UIThread.RunJobs();

            Assert.True(shell.ConsoleSourceLineCount > sourceCountBefore);
            if (isConsoleExpanded)
            {
                Assert.Contains("before dispose", shell.ConsoleText);
            }

            shell.Dispose();
            shell.Dispose();
            var text = shell.ConsoleText;
            var sourceCount = shell.ConsoleSourceLineCount;
            var renderedCount = shell.ConsoleRenderedLineCount;
            store.Add(new AppLogEntry(DateTimeOffset.UtcNow, "INFO", "test", "after dispose"));
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(store.Entries, entry => entry.Message == "after dispose");
            Assert.Equal(text, shell.ConsoleText);
            Assert.Equal(sourceCount, shell.ConsoleSourceLineCount);
            Assert.Equal(renderedCount, shell.ConsoleRenderedLineCount);
        }
        finally
        {
            try
            {
                store.Clear();
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                LogManager.Configuration = previousConfiguration;
            }
        }
    }

    [Fact]
    public void ConsoleText_UsesRingTailWhenExpandedBeforeUiEntriesFlush()
    {
        var previousConfiguration = LogManager.Configuration;
        try
        {
            LogManager.Configuration = new LoggingConfiguration();
            AppLogStore.Instance.Clear();
            var marker = $"console tail fallback line {Guid.NewGuid():N}";
            AppLogStore.Instance.Add(new AppLogEntry(
                DateTimeOffset.UtcNow,
                "INFO",
                "test",
                marker));

            using var shell = new ShellViewModel
            {
                IsConsoleExpanded = true,
            };

            Assert.Contains(marker, shell.ConsoleText);
            Assert.True(shell.ConsoleSourceLineCount >= 1);
            Assert.True(shell.ConsoleRenderedLineCount >= 1);
        }
        finally
        {
            try
            {
                AppLogStore.Instance.Clear();
            }
            finally
            {
                LogManager.Configuration = previousConfiguration;
            }
        }
    }
}
