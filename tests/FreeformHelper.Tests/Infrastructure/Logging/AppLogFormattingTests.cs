using FreeformHelper.UI.Logging;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AppLogFormattingTests
{
    [Fact]
    public void FormatLine_UsesUnifiedPipeSeparatedFields()
    {
        var line = AppLogFormatter.FormatLine(
            new DateTimeOffset(2026, 2, 12, 9, 8, 7, TimeSpan.Zero),
            "Info",
            "FreeformHelper.UI.ViewModels.FreeformHelperViewModel",
            "Grid built.");

        var localTime = new DateTimeOffset(2026, 2, 12, 9, 8, 7, TimeSpan.Zero).ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Assert.StartsWith(localTime + " | INFO  | FreeformHelper.UI.ViewModels.FreeformHelperViewModel", line, StringComparison.Ordinal);
        Assert.Contains(" | INFO  | ", line, StringComparison.Ordinal);
        Assert.EndsWith("Grid built.", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLine_UsesFallbackForEmptyLevelAndLogger()
    {
        var line = AppLogFormatter.FormatLine(
            new DateTimeOffset(2026, 2, 12, 9, 8, 7, TimeSpan.Zero),
            "",
            "",
            "Message");

        Assert.Contains(" | INFO  | App", line, StringComparison.Ordinal);
        Assert.EndsWith(" | Message", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLine_WithLoggerLongerThanTheColumn_KeepsTheEndOfTheName()
    {
        var logger = new string('a', 60) + ".Tail";

        var line = AppLogFormatter.FormatLine(DateTimeOffset.UnixEpoch, "Warn", logger, "m");

        Assert.Contains(" | ..." + logger[^53..] + " | ", line, StringComparison.Ordinal);
    }
}
