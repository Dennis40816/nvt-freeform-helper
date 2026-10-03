using FreeformHelper.UI.Logging;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AppLogFormattingTests
{
    [Fact]
    public void FormatLine_UsesUnifiedPipeSeparatedFields()
    {
        var entry = new AppLogEntry(
            new DateTimeOffset(2026, 2, 12, 9, 8, 7, TimeSpan.Zero),
            "Info",
            "FreeformHelper.UI.ViewModels.FreeformHelperViewModel",
            "Grid built.");

        var line = AppLogFormatter.FormatLine(entry);

        Assert.StartsWith("09:08:07 | INFO  | FreeformHelper.UI.ViewModels.FreeformHelperViewModel", line, StringComparison.Ordinal);
        Assert.Contains(" | INFO  | ", line, StringComparison.Ordinal);
        Assert.EndsWith("Grid built.", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLine_UsesFallbackForEmptyLevelAndLogger()
    {
        var entry = new AppLogEntry(
            new DateTimeOffset(2026, 2, 12, 9, 8, 7, TimeSpan.Zero),
            "",
            "",
            "Message");

        var line = AppLogFormatter.FormatLine(entry);

        Assert.Contains(" | INFO  | App", line, StringComparison.Ordinal);
        Assert.EndsWith(" | Message", line, StringComparison.Ordinal);
    }
}
