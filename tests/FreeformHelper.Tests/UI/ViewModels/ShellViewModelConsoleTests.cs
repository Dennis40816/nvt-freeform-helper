using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ShellViewModelConsoleTests
{
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

            var shell = new ShellViewModel
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
