// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Presentation;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchRowPresentationTests
{
    [Fact]
    public void SampleShowsCellValuesTotalsBankersRoundingAndContinuationFormula()
    {
        var source = NotchRowExplanationProjector.Build(NotchDetailPreviewSample.Evidence,
            NotchDetailPreviewSample.Rows, NotchCompensationModel.CurrentGain, false);

        Assert.Equal(new int?[] { 60, 38, 38, 16 }, source.Cells.Select(static cell => cell.InRow));
        Assert.Equal(new[] { 60.5, 38.5, 38.5, 16 }, source.Cells.Select(static cell => cell.ExactPercent));
        double[] expected1 = [30.25, 19.25, 19.25, 12.25];
        Assert.Equal(expected1, source.Cells.Select(static cell => cell.CadPercent));
        double[] expected2 = [30.25, 19.25, 19.25, 3.75];
        Assert.Equal(expected2, source.Cells.Select(static cell => cell.AddedPercent));
        string[] expected3 = ["capped", "capped", "capped", "reach 16"];
        Assert.Equal(expected3, source.Cells.Select(static cell => cell.LimitText));
        Assert.Equal(153.5, source.ExactTotal);
        Assert.Equal(152, source.RowTotal);
        Assert.Equal("half to even: −1.50", source.DifferenceText);
        string[] expected4 = ["diff 0 keeps 60 = 136 − 38 − 38", "MAIN combine 136 = 152 total − 16 sent by CONT", "CONT sends 16 to diff 13: one row holds two targets"];
        Assert.Equal(expected4, source.FormulaLines);
        Assert.True(source.Rows[1].Values[1].IsMuted);
        Assert.True(source.Rows[1].Values[4].IsMuted);
        Assert.True(source.Rows[1].Values[5].IsMuted);
        Assert.False(source.Rows[0].Values[6].IsMuted);
    }

    [Fact]
    public void NoContinuationHasOnlyTheKeepFormulaAndNoZeroDifferenceChip()
    {
        var evidence = NotchDetailPreviewSample.Evidence with
        {
            Targets = [new(0, 0, .6, 60, true, 0, NotchV22TargetDisposition.RetainedAnchor),
                new(0, 1, .4, 40, true, 0, NotchV22TargetDisposition.Emitted)],
        };
        var rows = new[] { Row(100, 1, 40) };

        var source = NotchRowExplanationProjector.Build(evidence, rows, NotchCompensationModel.CurrentGain, true);

        Assert.Equal("Output rows (V2.2)", source.RowsTitle);
        Assert.Equal("diff 0 keeps 60 = 100 − 40", Assert.Single(source.FormulaLines));
        Assert.False(source.HasDifference);
        Assert.Equal(100, source.RowTotal);
    }

    [Theory]
    [InlineData(NotchV22TargetDisposition.BelowStrictThreshold, "below TH", .385, 38)]
    [InlineData(NotchV22TargetDisposition.RoundedToZero, "rounds to 0", .004, 0)]
    [InlineData(NotchV22TargetDisposition.GuardRoundedToZero, "guard rounded to 0", .385, 38)]
    public void DroppedTargetKeepsItsExactValueAndExplainsTheOmission(
        NotchV22TargetDisposition disposition, string reason, double ratio, int rounded)
    {
        var evidence = NotchDetailPreviewSample.Evidence with
        {
            Targets = [NotchDetailPreviewSample.Evidence.Targets[0], new(0, 1, ratio, rounded, false, 0, disposition)],
        };

        var source = NotchRowExplanationProjector.Build(evidence, [Row(60)], NotchCompensationModel.CurrentGain, false);

        var dropped = source.Cells[1];
        Assert.Null(dropped.InRow);
        Assert.Equal("—", dropped.InRowText);
        Assert.Equal(reason, dropped.OmissionReason);
        Assert.Equal(ratio * 100, dropped.ExactPercent);
        Assert.StartsWith("rounding and drops: ", source.DifferenceText, StringComparison.Ordinal);
        Assert.Equal(60, source.RowTotal);
    }

    [Fact]
    public void CoverageGuardHasItsOwnFormulaAndDoesNotClaimPlainRounding()
    {
        var evidence = NotchDetailPreviewSample.Evidence with
        {
            Targets = [NotchDetailPreviewSample.Evidence.Targets[0], NotchDetailPreviewSample.Evidence.Targets[1]],
            Guard = [new(1, false, 38, .83, 31)],
        };

        var source = NotchRowExplanationProjector.Build(evidence, [Row(91, 1, 31)], NotchCompensationModel.CurrentGain, false);

        Assert.Equal(31, source.Cells[1].InRow);
        Assert.Equal(99, source.ExactTotal);
        Assert.Equal(91, source.RowTotal);
        Assert.Equal("rounding and drops: −8.00", source.DifferenceText);
        Assert.Contains("guard x0.83: diff 1 38 → 31", source.FormulaLines);
    }

    [Fact]
    public void NoGainShowsSourceCoverageWithoutClaimingAddedToFullArea()
    {
        var evidence = NotchDetailPreviewSample.Evidence with
        {
            Targets = [new(0, 0, .3025, 30, true, 1, NotchV22TargetDisposition.RetainedAnchor),
                new(0, 1, .1925, 19, true, 1, NotchV22TargetDisposition.Emitted)],
        };

        var source = NotchRowExplanationProjector.Build(evidence, [Row(49, 1, 19)], NotchCompensationModel.ConservativeNoGain, false);

        Assert.True(source.ShowCoverage);
        Assert.Equal("To Full (support only)", source.AddedTitle);
        Assert.All(source.Cells, static cell => Assert.Equal(0, cell.AddedPercent, precision: 8));
        Assert.Equal(49.5, source.ExactTotal, precision: 8);
        Assert.Equal(49, source.RowTotal);
        Assert.Equal("half to even: −0.50", source.DifferenceText);
        Assert.Single(source.FormulaLines);
    }

    [Fact]
    public void DisabledUsesSourceAreaSharesAndHidesTheCoverageColumns()
    {
        var evidence = NotchDetailPreviewSample.Evidence with
        {
            Targets = [new(0, 0, .6, 60, true, 0, NotchV22TargetDisposition.RetainedAnchor),
                new(0, 1, .4, 40, true, 0, NotchV22TargetDisposition.Emitted)],
        };

        var source = NotchRowExplanationProjector.Build(evidence, [Row(100, 1, 40)], NotchCompensationModel.Disabled, false);

        Assert.False(source.ShowCoverage);
        Assert.All(source.Cells, static cell => Assert.False(cell.ShowCoverage));
        Assert.Equal("Why (share of source area)", source.WhyTitle);
        Assert.Equal(100, source.ExactTotal);
        Assert.Equal(100, source.RowTotal);
        Assert.Empty(source.DifferenceText);
        Assert.Single(source.FormulaLines);
    }

    [Fact]
    public void ProjectedSourceWithoutACoverageCellStillIncludesItsKeepInTheRowTotal()
    {
        var sample = NotchDetailPreviewSample.Evidence;
        var evidence = sample with
        {
            SourceDiffIndex = 144,
            Targets = [new(0, 0, .335, 34, true, 0, NotchV22TargetDisposition.Emitted),
                new(0, 1, .335, 34, true, 0, NotchV22TargetDisposition.Emitted),
                new(0, 12, .33, 33, true, 0, NotchV22TargetDisposition.Emitted)],
            Combine = sample.Combine with { BeforeGuard = 100, AfterGuard = 100, ContinuationSubtraction = 33 },
        };
        var rows = new[]
        {
            new NotchTableRow(0, 144, 0, 1, new NotchV22Node(144, 67, 0, 34, 1, 34, 0), "synthetic"),
            new NotchTableRow(0, 144, 0, 1, new NotchV22Node(144, 100, 12, 33, 65535, 0, 1), "synthetic"),
        };

        var source = NotchRowExplanationProjector.Build(evidence, rows, NotchCompensationModel.Disabled, false);

        Assert.Equal(101, source.Cells.Sum(static cell => cell.InRow ?? 0));
        Assert.Equal(100, source.RowTotal);
        Assert.Equal(100, source.ExactTotal);
        Assert.Contains("diff 144 keeps -1 = 67 − 34 − 34", source.FormulaLines);
    }

    private static NotchTableRow Row(int combine, int target = 65535, int percent = 0) =>
        new(0, 0, 0, 1, new NotchV22Node(0, combine, target, percent, 65535, 0, 0), "synthetic");
}
