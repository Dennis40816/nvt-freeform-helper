// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class V21NotchAlgorithmBehaviorTests
{
    private static readonly int[] _expectedInsetValues = [20, 50, 50, 20, 2, 26, 20, 2, 38];

    [Theory]
    [InlineData(FreeformType.XWay, 10, 30)]
    [InlineData(FreeformType.YWay, 30, 10)]
    public void Generate_NonuniformGrid_UsesCurrentCellForSubtractionAndNeighborsForAddition(
        FreeformType axis,
        int leadingDiff,
        int trailingDiff)
    {
        using var culture = TestCulture.UseEnUs();
        var grid = CreateNonuniformGrid(axis);
        var regular = grid.Pads[1];
        regular.Freeform = axis;
        regular.MatchedCadPadId = 42;
        regular.MatchScore = 1;
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = [NotchAlgorithmVersion.V21],
            },
        };
        var insetCad = axis == FreeformType.XWay
            ? TestGeometryFactory.CreateCadPad(42, "PAD", 6, 0, 11, 10)
            : TestGeometryFactory.CreateCadPad(42, "PAD", 0, 23, 10, 28);
        var expandedCad = axis == FreeformType.XWay
            ? TestGeometryFactory.CreateCadPad(42, "PAD", 2, 0, 20, 10)
            : TestGeometryFactory.CreateCadPad(42, "PAD", 0, 14, 10, 32);
        var generator = new NotchTableGenerator();

        var inset = Assert.Single(generator.Generate(new CadPadSet([insetCad]), grid, settings).Rows);
        var expanded = Assert.Single(generator.Generate(new CadPadSet([expandedCad]), grid, settings).Rows);

        Assert.Equal(_expectedInsetValues, inset.Values);
        Assert.Equal(new[] { 20, 180, 180, leadingDiff, 1, 64, trailingDiff, 1, 38 }, expanded.Values);
        Assert.Equal(NotchAlgorithmVersion.V21, inset.Version);
        Assert.Equal(NotchAlgorithmVersion.V21, expanded.Version);
        Assert.Equal(regular.RegularPadId, expanded.RegularPadIndex);
        Assert.Equal(42, expanded.CadPadId);
    }

    private static RegularGrid CreateNonuniformGrid(FreeformType axis)
    {
        if (axis == FreeformType.XWay)
        {
            return new RegularGrid(1, 3, [0, 4, 14, 34], [0, 10],
            [
                TestGeometryFactory.CreateRegularPad(0, 0, 100, 0, 0, 4, 10, diffIndex: 10),
                TestGeometryFactory.CreateRegularPad(0, 1, 101, 4, 0, 14, 10, diffIndex: 20),
                TestGeometryFactory.CreateRegularPad(0, 2, 102, 14, 0, 34, 10, diffIndex: 30),
            ]);
        }

        return new RegularGrid(3, 1, [0, 10], [0, 20, 30, 34],
        [
            TestGeometryFactory.CreateRegularPad(0, 0, 100, 0, 0, 10, 20, diffIndex: 10),
            TestGeometryFactory.CreateRegularPad(1, 0, 101, 0, 20, 10, 30, diffIndex: 20),
            TestGeometryFactory.CreateRegularPad(2, 0, 102, 0, 30, 10, 34, diffIndex: 30),
        ]);
    }
}
