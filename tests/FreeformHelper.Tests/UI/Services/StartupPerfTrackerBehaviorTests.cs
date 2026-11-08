// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.RegularExpressions;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class StartupPerfTrackerBehaviorTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData(" \t ", "")]
    [InlineData("  pads=3  ", "; detail=pads=3")]
    public void MarkEmitsTrimmedParseableMarkerAndIgnoresBlankStages(string? detail, string expectedDetail)
    {
        using var culture = TestCulture.UseEnUs();
        var previousConfiguration = LogManager.Configuration;
        using var target = new MemoryTarget { Layout = "${message}" };
        var configuration = new LoggingConfiguration();
        configuration.AddRule(LogLevel.Debug, LogLevel.Info, target, typeof(StartupPerfTracker).FullName!);
        try
        {
            LogManager.Configuration = configuration;

            StartupPerfTracker.Mark(string.Empty, "ignored");
            StartupPerfTracker.Mark(" \t ", "ignored");
            StartupPerfTracker.Mark("  synthetic-load  ", detail);

            var message = Assert.Single(target.Logs);
            Assert.Matches(@"^PERF STARTUP #[1-9][0-9]*: stage=synthetic-load; elapsed=[0-9]+ms; delta=[0-9]+ms" +
                Regex.Escape(expectedDetail) + @"\.$", message);
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
        }
    }
}
