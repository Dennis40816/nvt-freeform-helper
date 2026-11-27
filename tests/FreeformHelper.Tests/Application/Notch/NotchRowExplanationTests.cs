// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchRowExplanationTests
{
    [Fact]
    public void EveryGeneratedRowHasOneMatchingExplanation()
    {
        var (cad, grid, settings) = NotchRowExplanationFixture.Create();
        var collector = new NotchV22ExplanationCollector();

        var table = new NotchTableGenerator().Generate(cad, grid, settings, explanationCollector: collector);

        var source = Assert.Single(collector.Explanations);
        Assert.Equal(table.Rows.Count, source.Rows.Length);
        Assert.Equal(source.Rows.Length, source.Rows.Select(static row => row.Ordinal).Distinct().Count());
        Assert.All(source.Rows, row => Assert.Equal(table.Rows[row.Ordinal].Values, row.Values));
        Assert.Equal(source.SelectedCadPadId, Assert.Single(source.Candidates).CadPadId);
    }

    [Fact]
    public void EqualSourceDiffsOnDifferentIcsHaveSeparateExplanations()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, 10, 10,
            diffIndexSelector: static (_, _) => 0, icIndexSelector: static (_, col) => col);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[1].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = 1;
        grid.Pads[1].MatchedCadPadId = 2;
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 6, 10),
            TestGeometryFactory.CreateCadPad(2, "PAD", 10, 0, 16, 10)]);
        var collector = new NotchV22ExplanationCollector();

        var table = new NotchTableGenerator().Generate(cad, grid, NotchHolePanelFixture.CreateSettings(true),
            explanationCollector: collector);

        Assert.Equal(2, collector.Explanations.Length);
        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(row.Values,
            Assert.Single(Assert.Single(collector.Explanations,
                source => source.IcIndex == row.IcIndex && source.SourceDiffIndex == row.DiffIndex).Rows).Values));
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    [InlineData(NotchCompensationModel.Disabled)]
    public void CollectorPreservesAllGeneratedRows(NotchCompensationModel model)
    {
        var (cad, grid, settings) = NotchRowExplanationFixture.Create();
        settings.Notch.CompensationModel = model;
        var collector = new NotchV22ExplanationCollector();

        var plain = new NotchTableGenerator().Generate(cad, grid, settings);
        var explained = new NotchTableGenerator().Generate(cad, grid, settings, explanationCollector: collector);

        Assert.Equal(plain.Rows.Count, explained.Rows.Count);
        Assert.All(Enumerable.Range(0, plain.Rows.Count), index =>
        {
            Assert.Equal(plain.Rows[index].Values, explained.Rows[index].Values);
            Assert.Equal(plain.Rows[index].Comment, explained.Rows[index].Comment);
        });
    }

    [Theory]
    [InlineData(104)]
    [InlineData(90)]
    public void RoundedGroupsReproduceGuardedLegsAndContinuationCombine(int cap)
    {
        var source = Generate(cap);

        var unguardedLegs = source.Targets.Where(static target => target.Disposition == NotchV22TargetDisposition.Emitted)
            .ToDictionary(static target => target.DiffIndex, static target => target.RoundedPercent);
        var guardedLegs = source.Guard.Where(static item => !item.IsRetainedAnchor)
            .ToDictionary(static item => item.TargetDiffIndex, static item => item.AfterPercent);

        Assert.Equal(source.Combine.BeforeGuard, source.Combine.RetainedBeforeGuard + unguardedLegs.Values.Sum());
        Assert.Equal(source.Combine.RetainedBeforeGuard,
            Assert.Single(source.Targets, static target => target.Disposition == NotchV22TargetDisposition.RetainedAnchor).RoundedPercent);
        Assert.All(source.Combine.LegsBeforeGuard, leg => Assert.Equal(unguardedLegs[leg.TargetDiffIndex], leg.Percent));
        Assert.All(source.Guard, item =>
            Assert.Equal((int)Math.Floor(item.BeforePercent * item.Scale), item.AfterPercent));
        Assert.All(source.Rows.SelectMany(static row => row.Legs), leg =>
            Assert.Equal(guardedLegs.GetValueOrDefault(leg.TargetDiffIndex, unguardedLegs[leg.TargetDiffIndex]), leg.Percent));
        Assert.Equal(2, source.Rows.Length);
        Assert.Equal(100, source.Rows[1].Values[1]);
        Assert.Equal(source.Combine.AfterGuard - source.Combine.ContinuationSubtraction, source.Rows[0].Values[1]);
        Assert.Equal(source.Rows[1].Legs.Sum(static leg => leg.Percent), source.Combine.ContinuationSubtraction);
        var retainedAfter = source.Guard.Where(static item => item.IsRetainedAnchor)
            .Select(static item => item.AfterPercent).DefaultIfEmpty(source.Combine.RetainedBeforeGuard).Max();
        Assert.Equal(source.Combine.AfterGuard, retainedAfter + source.Rows.SelectMany(static row => row.Legs).Sum(static leg => leg.Percent));
    }

    [Fact]
    public void CandidateSelectionListsEveryCompetingCadAndItsWinningKeys()
    {
        var (cad, grid, settings) = NotchRowExplanationFixture.Create();
        var rival = TestGeometryFactory.CreateCadPad(2, "RIVAL", 0, 0, 12, 12);
        var collector = new NotchV22ExplanationCollector();

        new NotchTableGenerator().Generate(new CadPadSet([.. cad.Pads, rival]), grid, settings, explanationCollector: collector);

        var source = Assert.Single(collector.Explanations);
        Assert.Equal(2, source.Candidates.Length);
        Assert.Equal(1, source.SelectedCadPadId);
        Assert.Contains(source.Candidates, static candidate => candidate.CadPadId == 1 && candidate.IsAnchorMatchedCad);
        Assert.Contains(source.Candidates, static candidate => candidate.CadPadId == 2 && !candidate.IsAnchorMatchedCad);
    }

    [Fact]
    public void TargetFlooredAwayByGuardExplainsWhyItWasNotEmitted()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 3, 10, 10, diffIndexSelector: static (_, col) => col);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[2].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = 1;
        grid.Pads[2].MatchedCadPadId = 2;
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 12, 10),
            TestGeometryFactory.CreateCadPad(2, "PAD", 18, 0, 30, 10)]);
        var settings = NotchHolePanelFixture.CreateSettings(capsEnabled: true);
        settings.Notch.TargetCoverageCapPercent = 1;
        var collector = new NotchV22ExplanationCollector();

        new NotchTableGenerator().Generate(cad, grid, settings, explanationCollector: collector);

        Assert.Equal(2, collector.Explanations.Length);
        Assert.All(collector.Explanations, source =>
        {
            var target = Assert.Single(source.Targets, static target => target.DiffIndex == 1);
            Assert.Equal(NotchV22TargetDisposition.GuardRoundedToZero, target.Disposition);
            Assert.True(target.RoundedPercent > 0);
            Assert.Contains(source.Guard, static item => item.TargetDiffIndex == 1 && item.AfterPercent == 0);
            Assert.DoesNotContain(source.Rows.SelectMany(static row => row.Legs), static leg => leg.TargetDiffIndex == 1);
        });
    }

    [Theory]
    [InlineData(true, NotchV22ToFullLimit.VirtualAreaCap)]
    [InlineData(false, NotchV22ToFullLimit.None)]
    public void ToFullLimitUsesTheProducedEffectiveAndReachableAreas(bool cap, NotchV22ToFullLimit expected)
    {
        var settings = NotchHolePanelFixture.CreateSettings(capsEnabled: true);
        var grid = TestGeometryFactory.CreateRegularGrid(3, 3, 10, 10);
        grid.Pads[4].Freeform = FreeformType.XWay;
        grid.Pads[4].MatchedCadPadId = 1;
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 10, 10, 13, 20)]);
        settings.Notch.EnableBoundaryVirtualAreaCap = cap;
        var collector = new NotchV22ExplanationCollector();

        new NotchTableGenerator().Generate(cad, grid, settings, explanationCollector: collector);

        var source = Assert.Single(collector.Explanations);
        var regular = Assert.Single(source.Regulars);
        Assert.Equal(expected, regular.AdditionLimit);
        Assert.True(regular.ToFullApplied);
        Assert.False(regular.IsBoundaryRegular);
    }

    [Theory]
    [InlineData(100.0 - 1e-12, 100.0, 100.0, NotchV22ToFullLimit.None)]
    [InlineData(80.0 - 1e-12, 80.0, 100.0, NotchV22ToFullLimit.ReachableFreeArea)]
    [InlineData(60.0, 80.0, 100.0, NotchV22ToFullLimit.VirtualAreaCap)]
    public void ToFullLimitIgnoresFloatingPointNoise(
        double effective, double reachable, double regular, NotchV22ToFullLimit expected)
    {
        var limit = NotchV22ExplanationCollector.DescribeLimit(true, regular, 50.0, effective, reachable);

        Assert.Equal(expected, limit);
    }

    internal static NotchV22RowExplanation Generate(int cap = 90)
    {
        var (cad, grid, settings) = NotchRowExplanationFixture.Create(cap);
        var collector = new NotchV22ExplanationCollector();
        new NotchTableGenerator().Generate(cad, grid, settings, explanationCollector: collector);
        return Assert.Single(collector.Explanations);
    }
}
