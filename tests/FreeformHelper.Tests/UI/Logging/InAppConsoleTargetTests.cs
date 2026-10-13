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
        using var logging = new TargetLogging(NLog.LogLevel.Trace);

        logging.GetLogger(loggerName).Log(NLog.LogLevel.FromString(nlogLevelName), message);

        using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
        var entry = Assert.Single(snapshot.Entries, candidate => candidate.SourceId == loggerName);
        Assert.Equal(expected, entry.Level);
        Assert.Equal(message, ConsoleLogTestSupport.Read(entry.TextContent));
    }

    [Fact]
    public async Task Write_EventBelowTheRuleMinimum_IsNotStored()
    {
        var loggerName = $"target-filtered-{Guid.NewGuid():N}";
        using var logging = new TargetLogging(NLog.LogLevel.Warn);

        logging.GetLogger(loggerName).Info("below minimum");

        using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
        Assert.DoesNotContain(snapshot.Entries, candidate => candidate.SourceId == loggerName);
    }

    private sealed class TargetLogging : IDisposable
    {
        private readonly InAppConsoleTarget _target;
        private readonly LogFactory _factory;

        public TargetLogging(NLog.LogLevel minimumLevel)
        {
            _target = new InAppConsoleTarget { Layout = "${message}" };
            var configuration = new LoggingConfiguration();
            configuration.AddTarget("console", _target);
            configuration.AddRule(minimumLevel, NLog.LogLevel.Fatal, _target);
            _factory = new LogFactory { Configuration = configuration };
        }

        public Logger GetLogger(string loggerName) => _factory.GetLogger(loggerName);

        public void Dispose()
        {
            _factory.Dispose();
            _target.Dispose();
        }
    }
}
