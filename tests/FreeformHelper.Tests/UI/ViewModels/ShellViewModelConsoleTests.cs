using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShellViewModelConsoleTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dispose_StopsReceivingGlobalLogEntries(bool isConsoleExpanded)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var store = AppLogStore.Instance;
        store.MarkUiReady();
        store.Clear();
        Dispatcher.UIThread.RunJobs();
        try
        {
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
            store.Clear();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public void ConsoleText_UsesRingTailWhenExpandedBeforeUiEntriesFlush()
    {
        AppLogStore.Instance.Clear();
        try
        {
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
            AppLogStore.Instance.Clear();
        }
    }
}
