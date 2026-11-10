// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadDrawDensityPolicyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RegularStepIsOneUnlessBothFlagsAreSet(bool showRegular, bool lowDetailMode)
    {
        Assert.Equal(1, PadDrawDensityPolicy.RegularStep(showRegular, lowDetailMode, 0.3, 0.01, 4.0));
    }

    [Theory]
    [InlineData(0.300001, 1)]
    [InlineData(0.3, 1)]
    [InlineData(0.299999, 2)]
    [InlineData(0.15, 2)]
    [InlineData(0.149999, 3)]
    [InlineData(0.1, 3)]
    [InlineData(0.099999, 4)]
    [InlineData(0.01, 4)]
    public void RegularStepCeilsTheZoomRatioAndClampsToTheResource(double zoom, int expected)
    {
        Assert.Equal(expected, PadDrawDensityPolicy.RegularStep(true, true, 0.3, zoom, 4.0));
    }

    [Theory]
    [InlineData(-5.0, 1)]
    [InlineData(0.0, 1)]
    [InlineData(0.5, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 2)]
    [InlineData(3.5, 4)]
    [InlineData(4.5, 4)]
    [InlineData(5.5, 6)]
    public void RegularStepRoundsTheResourceToEvenBeforeApplyingTheMinimum(double maxStepResource, int expected)
    {
        Assert.Equal(expected, PadDrawDensityPolicy.RegularStep(true, true, 10.0, 1.0, maxStepResource));
    }

    [Theory]
    [InlineData(-1.0, 2)]
    [InlineData(0.0, 2)]
    [InlineData(0.0000005, 2)]
    [InlineData(0.000001, 2)]
    [InlineData(0.000002, 1)]
    public void RegularStepFloorsZoomBeforeDividing(double zoom, int expected)
    {
        Assert.Equal(expected, PadDrawDensityPolicy.RegularStep(true, true, 0.000002, zoom, 10.0));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    public void RegularStepKeepsAtLeastOneForNonpositiveThresholds(double threshold)
    {
        Assert.Equal(1, PadDrawDensityPolicy.RegularStep(true, true, threshold, 1.0, 4.0));
    }

    [Theory]
    [InlineData(-1, 100.0, 1)]
    [InlineData(0, 100.0, 1)]
    [InlineData(100, 100.0, 1)]
    [InlineData(101, 100.0, 2)]
    [InlineData(400, 100.0, 4)]
    [InlineData(401, 100.0, 5)]
    [InlineData(2500, 2500.0, 1)]
    [InlineData(2501, 2500.0, 2)]
    [InlineData(10001, 2500.0, 5)]
    public void CadStepCeilsTheCountOverTheBudgetAndKeepsAtLeastOne(int count, double budgetResource, int expected)
    {
        Assert.Equal(expected, PadDrawDensityPolicy.CadStep(count, budgetResource));
    }

    [Theory]
    [InlineData(101, -5.0, 2)]
    [InlineData(101, 0.0, 2)]
    [InlineData(101, 99.5, 2)]
    [InlineData(101, 100.5, 2)]
    [InlineData(102, 101.5, 1)]
    [InlineData(103, 102.5, 2)]
    [InlineData(104, 103.5, 1)]
    public void CadStepRoundsTheBudgetToEvenBeforeApplyingTheHundredPadMinimum(int count, double budgetResource, int expected)
    {
        Assert.Equal(expected, PadDrawDensityPolicy.CadStep(count, budgetResource));
    }

    [Theory]
    [InlineData(false, 1, 1, 2, true)]
    [InlineData(false, 0, 1, 2, false)]
    [InlineData(false, 1, 0, 2, false)]
    [InlineData(false, 0, 0, 2, false)]
    [InlineData(false, 2, 1, 2, false)]
    [InlineData(false, 1, 2, 2, false)]
    [InlineData(false, -1, -1, 2, true)]
    [InlineData(false, -2, -1, 2, false)]
    [InlineData(false, -1, -3, 3, false)]
    [InlineData(false, -3, -2, 3, false)]
    [InlineData(false, -2, -2, 3, true)]
    [InlineData(false, -4, -1, 4, false)]
    [InlineData(false, -1, -4, 4, false)]
    [InlineData(true, int.MinValue, int.MinValue, 4, false)]
    [InlineData(false, int.MinValue, int.MinValue, 1, false)]
    [InlineData(false, int.MinValue, int.MinValue, 0, false)]
    [InlineData(false, int.MinValue, int.MinValue, -1, false)]
    [InlineData(false, 0, int.MinValue, 2, false)]
    public void RegularDecimationPreservesSelectionGuardsAndBothAxisModuloTests(
        bool isSelected, int row, int col, int step, bool expected)
    {
        Assert.Equal(expected, PadDrawDensityPolicy.IsRegularDecimated(isSelected, row, col, step));
    }

    [Theory]
    [InlineData(int.MinValue, 1)]
    [InlineData(1, int.MinValue)]
    public void RegularDecimationKeepsTheExistingAbsoluteValueOverflow(int row, int col)
    {
        Assert.Throws<OverflowException>(() => PadDrawDensityPolicy.IsRegularDecimated(false, row, col, 2));
    }
}
