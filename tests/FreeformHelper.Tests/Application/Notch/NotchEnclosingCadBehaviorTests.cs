// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchEnclosingCadBehaviorTests
{
    private static readonly int[] _expected1 = [20, 170, 170, 10, 120, 20, 30, 50, 30];
    private static readonly int[] _expected2 = [20, 170, 170, 10, 50, 10, 30, 120, 20];
    private static readonly int[] _expected3 = [20, 200, 200, 10, 50, 10, 30, 150, 20];

    [Fact]
    public void Generate_LegacyV22_EnclosingCad_SelectsTheLargerOverhang()
    {
        using var culture = TestCulture.UseEnUs();

        var rightDominant = GenerateEnclosingRow(FreeformType.XWay, 8, 25);
        var leftDominant = GenerateEnclosingRow(FreeformType.XWay, 5, 22);

        Assert.Equal(_expected1, rightDominant.Values);
        Assert.Equal(_expected2, leftDominant.Values);
        Assert.Contains("Case=0", rightDominant.Comment, StringComparison.Ordinal);
        Assert.Contains("Case=1", leftDominant.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_LegacyV22_EqualOverhangs_SelectsFarSideOnBothAxes()
    {
        using var culture = TestCulture.UseEnUs();

        var horizontal = GenerateEnclosingRow(FreeformType.XWay, 5, 25);
        var vertical = GenerateEnclosingRow(FreeformType.YWay, 5, 25);

        Assert.Equal(_expected3, horizontal.Values);
        Assert.Equal(horizontal.Values, vertical.Values);
        Assert.Equal(NotchAlgorithmVersion.V22, horizontal.Version);
        Assert.Equal(NotchAlgorithmVersion.V22, vertical.Version);
        Assert.Contains("Case=1", horizontal.Comment, StringComparison.Ordinal);
        Assert.Contains("Case=1", vertical.Comment, StringComparison.Ordinal);
    }

    private static NotchTableRow GenerateEnclosingRow(FreeformType axis, double cadMin, double cadMax)
    {
        var isX = axis == FreeformType.XWay;
        var grid = TestGeometryFactory.CreateRegularGrid(
            rows: isX ? 1 : 3,
            cols: isX ? 3 : 1,
            cellWidth: 10,
            cellHeight: 10,
            diffIndexSelector: (row, col) => (row + col + 1) * 10);
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = 42;
        }

        grid.Pads[1].Freeform = axis;
        grid.Pads[1].MatchScore = 1;
        var cad = isX
            ? TestGeometryFactory.CreateCadPad(42, "PAD", cadMin, 0, cadMax, 10)
            : TestGeometryFactory.CreateCadPad(42, "PAD", 0, cadMin, 10, cadMax);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = [NotchAlgorithmVersion.V22],
                LenScale = 10,
            },
        };

        return Assert.Single(new NotchTableGenerator().Generate(new CadPadSet([cad]), grid, settings).Rows);
    }
}
