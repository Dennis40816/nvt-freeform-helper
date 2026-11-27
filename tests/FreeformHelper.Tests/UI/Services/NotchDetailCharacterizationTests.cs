// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDetailCharacterizationTests
{
    [Fact]
    public void DetailKeepsEveryAllocationAndPreviewGeometry()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, cellWidth: 10, cellHeight: 10);
        var cad = TestGeometryFactory.CreateCadPad(1, "SENSOR", 5, 0, 15, 10);

        var detail = NotchDetailUseCase.Build(cad, 0, grid, new ProjectSettings());

        Assert.Same(cad, detail.CadPad);
        Assert.Equal("CAD idx 0 (CAD id 1)", detail.CadTitle);
        Assert.Equal("Layer: SENSOR", detail.CadLayer);
        Assert.Equal(2, detail.Allocations.Count);
        Assert.Equal(detail.Allocations.Select(static a => a.RegularIndex),
            detail.PreviewRegularPads.Select(static p => p.Index));
        Assert.All(detail.Allocations, static a => Assert.Equal($"{a.Q7}/128", a.Q7Text));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(64, false)]
    [InlineData(65, true)]
    public void BelowThresholdKeepsTheStrictQ7Predicate(int threshold, bool below)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, cellWidth: 10, cellHeight: 10);
        var cad = TestGeometryFactory.CreateCadPad(1, "SENSOR", 0, 0, 20, 10);
        var settings = new ProjectSettings();
        settings.Notch.ThresholdQ7 = threshold;

        var detail = NotchDetailUseCase.Build(cad, 0, grid, settings);

        Assert.Equal(64, detail.MaxAllocationQ7);
        Assert.Equal(below, detail.IsBelowThreshold);
    }

    [Theory]
    [InlineData(0, "0/128", "0.0 %")]
    [InlineData(64, "64/128", "50.0 %")]
    [InlineData(128, "128/128", "100.0 %")]
    [InlineData(255, "255/128", "199.2 %")]
    public void AllocationTextKeepsItsQ7AndPercentageFormatting(int q7, string q7Text, string ratioText)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var allocation = new NotchDetailAllocation(0, 0, 0, 0, q7, q7 / 128d);

            Assert.Equal(q7Text, allocation.Q7Text);
            Assert.Equal(ratioText, allocation.RatioText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
