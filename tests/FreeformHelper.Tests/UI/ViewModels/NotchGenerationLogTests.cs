// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchGenerationLogTests
{
    private const string LoggerName = "NotchGenerationLogTests";

    [Fact]
    public void PhaseTimingsWithoutCandidateBreakdownLogOneSummaryLine()
    {
        var timings = new NotchGenerationPhaseTimings(1, 2, 3, 4, 5);

        var messages = Capture(logger => NotchGenerationLog.LogPhaseTimings(logger, "export", timings));

        var message = Assert.Single(messages);
        Assert.Equal(
            "export phase timings: profiles=1 ms, candidates=2 ms, merge=3 ms, legacy=4 ms, finalize=5 ms, total=15 ms.",
            message);
    }

    [Fact]
    public void CandidateBreakdownWithoutStageCDataLogsTwoLines()
    {
        var stages = new NotchV22CompensationStageTimings(10, 20, 30, 40, NotchV22StageCSubphaseTimings.Empty);
        var candidates = new NotchCandidatePhaseTimings(7, 100, 60, stages);
        var timings = new NotchGenerationPhaseTimings(1, 2, 3, 4, 5, candidates);

        var messages = Capture(logger => NotchGenerationLog.LogPhaseTimings(logger, "preview", timings));

        Assert.Equal(2, messages.Count);
        Assert.StartsWith("preview phase timings:", messages[0], StringComparison.Ordinal);
        Assert.Equal(
            "preview candidate breakdown: count=7, buildCadCandidate=100 ms, compensation=60 ms, postCompute=40 ms, stageA=10 ms, stageB=20 ms, stageC=30 ms, stageD=40 ms.",
            messages[1]);
    }

    [Fact]
    public void StageCBreakdownWithDataAddsThirdLine()
    {
        var stageC = new NotchV22StageCSubphaseTimings(1, 2, 3, 4, 5, 6);
        var stages = new NotchV22CompensationStageTimings(10, 20, 30, 40, stageC);
        var candidates = new NotchCandidatePhaseTimings(7, 100, 60, stages);
        var timings = new NotchGenerationPhaseTimings(1, 2, 3, 4, 5, candidates);

        var messages = Capture(logger => NotchGenerationLog.LogPhaseTimings(logger, "export", timings));

        Assert.Equal(3, messages.Count);
        Assert.Equal(
            "export stageC breakdown: ownerLookup=1 ms, occupiedArea=2 ms, sourceCoverage=3 ms, reachability=4 ms, ruleDecision=5 ms, previewDebug=6 ms.",
            messages[2]);
    }

    [Fact]
    public void EmptyCandidateBreakdownIsNotLogged()
    {
        var timings = new NotchGenerationPhaseTimings(1, 0, 0, 0, 0, NotchCandidatePhaseTimings.Empty);

        var messages = Capture(logger => NotchGenerationLog.LogPhaseTimings(logger, "export", timings));

        Assert.Single(messages);
    }

    private static List<string> Capture(Action<Logger> act)
    {
        var previousConfiguration = LogManager.Configuration;
        using var target = new MemoryTarget { Layout = "${message}" };
        var configuration = new LoggingConfiguration();
        configuration.AddRule(LogLevel.Debug, LogLevel.Fatal, target, LoggerName);
        try
        {
            LogManager.Configuration = configuration;
            act(LogManager.GetLogger(LoggerName));
            return [.. target.Logs];
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
        }
    }
}
