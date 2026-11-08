// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchAllocationMissingCadBehaviorTests
{
    private static readonly int[] _expectedRegularPadIndices = [0, 2];
    private static readonly int[] _expectedDiffIndices = [7, 9];

    [Fact]
    public void Generate_LegacyGridReferencesFilteredOutCad_KeepsTheRemainingCadRows()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([7, 8, 9]);
        var cad = TestGeometryFactory.CreateCadPad(31, "Visible", 0, 0, 1, 1);
        var filteredOutCad = TestGeometryFactory.CreateCadPad(32, "Hidden", 1, 0, 2, 1);
        var otherCad = TestGeometryFactory.CreateCadPad(33, "Visible", 2, 0, 3, 1);
        PadMatcher.Match(new CadPadSet([cad, filteredOutCad, otherCad]), grid);
        foreach (var pad in grid.Pads)
        {
            pad.Freeform = FreeformType.XWay;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = [NotchAlgorithmVersion.V21],
                ThresholdQ7 = 64,
            },
        };

        var table = new NotchTableGenerator().Generate(new CadPadSet([cad, otherCad]), grid, settings);

        Assert.Equal(new int?[] { 31, 33 }, table.Rows.Select(row => row.CadPadId));
        Assert.Equal(_expectedRegularPadIndices, table.Rows.Select(row => row.RegularPadIndex));
        Assert.Equal(_expectedDiffIndices, table.Rows.Select(row => row.DiffIndex));
        Assert.All(table.Rows, row =>
        {
            Assert.Equal(NotchAlgorithmVersion.V21, row.Version);
            Assert.Equal(row.DiffIndex, row.Values[0]);
        });
        Assert.Equal(32, grid.Pads[1].MatchedCadPadId);
    }
}
