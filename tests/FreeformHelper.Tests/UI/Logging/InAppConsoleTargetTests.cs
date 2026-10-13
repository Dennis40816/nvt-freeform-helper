using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using NLog;
using NLog.Config;
using Xunit;
using CoreLogLevel = Nvt.Core.LogConsole.LogLevel;

namespace FreeformHelper.Tests;

public sealed class InAppConsoleTargetTests
{
    [Theory]
    [InlineData("Trace", CoreLogLevel.Trace)]
    [InlineData("Debug", CoreLogLevel.Debug)]
    [InlineData("Info", CoreLogLevel.Info)]
    [InlineData("Warn", CoreLogLevel.Warn)]
    [InlineData("Error", CoreLogLevel.Error)]
    [InlineData("Fatal", CoreLogLevel.Fatal)]
    public async Task Write_NLogEvent_StoresTheMatchingLevelLoggerAndMessage(string nlogLevelName, CoreLogLevel expected)
    {
        var loggerName = $"target-{nlogLevelName}-{Guid.NewGuid():N}";
        var message = $"target message {Guid.NewGuid():N}";
        var logger = CreateLogger(loggerName);

        logger.Log(NLog.LogLevel.FromString(nlogLevelName), message);

        using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
        var entry = Assert.Single(snapshot.Entries, candidate => candidate.SourceId == loggerName);
        Assert.Equal(expected, entry.Level);
        Assert.Equal(message, ConsoleLogTestSupport.Read(entry.TextContent));
    }

    [Fact]
    public async Task Write_EventBelowTheRuleMinimum_IsNotStored()
    {
        var loggerName = $"target-filtered-{Guid.NewGuid():N}";
        var configuration = new LoggingConfiguration();
        var target = new InAppConsoleTarget { Layout = "${message}" };
        configuration.AddTarget("console", target);
        configuration.AddRule(NLog.LogLevel.Warn, NLog.LogLevel.Fatal, target);
        var factory = new LogFactory { Configuration = configuration };

        factory.GetLogger(loggerName).Info("below minimum");

        using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
        Assert.DoesNotContain(snapshot.Entries, candidate => candidate.SourceId == loggerName);
    }

    private static Logger CreateLogger(string loggerName)
    {
        var configuration = new LoggingConfiguration();
        var target = new InAppConsoleTarget { Layout = "${message}" };
        configuration.AddTarget("console", target);
        configuration.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, target);
        var factory = new LogFactory { Configuration = configuration };
        return factory.GetLogger(loggerName);
    }
}
