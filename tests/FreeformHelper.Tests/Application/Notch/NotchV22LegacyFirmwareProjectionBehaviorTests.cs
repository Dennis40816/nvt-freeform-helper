// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22LegacyFirmwareProjectionBehaviorTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 100)]
    [InlineData(2, 75)]
    public void ExportAndSimulation_TruncatedLegacyV22Payload_DefaultsMissingRatiosAndPreservesComment(
        int payloadLength, int expectedPercent)
    {
        using var culture = TestCulture.UseEnUs();
        int[] legacyValues = [999, 75];
        var table = new NotchTable([
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 10, 0, 101, legacyValues[..payloadLength], "legacy pad"),
        ]);
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var projection = new DiffFrameGridProjectionResult(true, null, [new DiffFrameGridCellValue(0, 0, 0, 0, 0, 100)]);

        var text = NotchEdgeCaseHarness.ExportC(table);
        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid, grid.Pads.Select(pad => pad.RegularPadId).ToHashSet(), [projection], table,
            NotchAlgorithmVersion.V22, 65535));

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        Assert.Equal((double)expectedPercent, Assert.Single(result.Cells).AfterValue);
        var nodeLine = Assert.Single(text.Split('\n'), line => line.Contains("// legacy pad;", StringComparison.Ordinal));
        var fields = nodeLine[(nodeLine.IndexOf('{', StringComparison.Ordinal) + 1)..nodeLine.IndexOf('}', StringComparison.Ordinal)].Split(',', StringSplitOptions.TrimEntries);
        Assert.Equal([
            "10", expectedPercent.ToString(CultureInfo.InvariantCulture), "NHC_DIFF_NONE", "0", "NHC_DIFF_NONE", "0", "NHC_FLAG_NONE",
        ], fields);
        Assert.DoesNotContain("// LEGACY C=", text, StringComparison.Ordinal);
    }
}
