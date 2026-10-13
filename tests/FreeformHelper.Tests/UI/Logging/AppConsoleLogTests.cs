using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using Nvt.Core.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AppConsoleLogTests
{
    [Fact]
    public async Task Write_WithLoggerName_UsesTheFullLoggerNameAsSourceId()
    {
        var loggerName = $"FreeformHelper.UI.Tests.{Guid.NewGuid():N}";
        var message = $"full logger name {Guid.NewGuid():N}";

        AppConsoleLog.Write(DateTimeOffset.UtcNow, LogLevel.Warn, loggerName, message);

        using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
        var entry = Assert.Single(snapshot.Entries, candidate => candidate.SourceId == loggerName);
        Assert.Equal(LogLevel.Warn, entry.Level);
        Assert.Equal(message, ConsoleLogTestSupport.Read(entry.TextContent));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Write_WithBlankLoggerName_UsesTheDefaultAppSource(string? loggerName)
    {
        var message = $"blank logger {Guid.NewGuid():N}";

        AppConsoleLog.Write(DateTimeOffset.UtcNow, LogLevel.Info, loggerName, message);

        using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
        var entry = Assert.Single(
            snapshot.Entries,
            candidate => ConsoleLogTestSupport.Read(candidate.TextContent) == message);
        Assert.Equal("App", entry.SourceId);
    }

    [Fact]
    public async Task Write_WithLocalOffsetTimestamp_StoresTheSameInstantInUtc()
    {
        var loggerName = $"time-{Guid.NewGuid():N}";
        var localTime = new DateTimeOffset(2026, 3, 4, 10, 20, 30, TimeSpan.FromHours(8));

        AppConsoleLog.Write(localTime, LogLevel.Info, loggerName, "offset");

        using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
        var entry = Assert.Single(snapshot.Entries, candidate => candidate.SourceId == loggerName);
        Assert.Equal(localTime.UtcDateTime, entry.Timestamp.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, entry.Timestamp.Offset);
    }

    [Fact]
    public void Write_WithNullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => AppConsoleLog.Write(DateTimeOffset.UtcNow, LogLevel.Info, "logger", null!));
    }
}
