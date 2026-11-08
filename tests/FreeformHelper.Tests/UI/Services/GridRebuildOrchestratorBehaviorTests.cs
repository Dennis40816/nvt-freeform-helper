// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class GridRebuildOrchestratorBehaviorTests
{
    private static readonly int[] _expectedDiffIndexes = [0, 1, 2, 3];

    [Fact]
    public void Build_DxfLayer_UsesVisibleSourcePolygonsAndRestoresFreeformOverrides()
    {
        var bottomLeft = TestGeometryFactory.CreateCadPad(10, "regular", 0, 0, 2, 2);
        var bottomRight = TestGeometryFactory.CreateCadPad(11, "REGULAR", 4, 0, 6, 2);
        var topLeft = TestGeometryFactory.CreateCadPad(12, "regular", 0, 4, 2, 6);
        var topRight = TestGeometryFactory.CreateCadPad(13, "REGULAR", 4, 4, 6, 6);
        var hidden = TestGeometryFactory.CreateCadPad(99, "regular", 20, 0, 22, 2);
        var otherLayer = TestGeometryFactory.CreateCadPad(98, "signal", 30, 0, 32, 2);
        var cad = new CadPadSet([topRight, hidden, bottomLeft, otherLayer, topLeft, bottomRight]);
        var settings = new GridSettings { XChannels = 2, YChannels = 2 };
        var project = new ProjectFile { FreeformOverrides = new Dictionary<int, FreeformType> { [1] = FreeformType.YWay } };

        var result = GridRebuildOrchestrator.Build(CreateLayerRequest(cad, settings, project, new HashSet<int> { 99 }));

        Assert.True(result.Success);
        Assert.Equal("Ready.", result.StatusText);
        Assert.Null(result.CanvasHint);
        Assert.Null(result.DxfRegularSourceHint);
        var grid = Assert.IsType<RegularGrid>(result.Grid);
        Assert.Equal(2, grid.Rows);
        Assert.Equal(2, grid.Cols);
        Assert.Equal(4, grid.Pads.Count);
        Assert.Same(bottomLeft.Polygon, grid.GetPad(0, 0).Polygon);
        Assert.Same(bottomRight.Polygon, grid.GetPad(0, 1).Polygon);
        Assert.Same(topLeft.Polygon, grid.GetPad(1, 0).Polygon);
        Assert.Same(topRight.Polygon, grid.GetPad(1, 1).Polygon);
        Assert.Equal(FreeformType.YWay, grid.GetPad(0, 1).Freeform);
        Assert.Equal(_expectedDiffIndexes, grid.Pads.Select(pad => pad.DiffIndex).Order());
    }

    [Fact]
    public void Build_DxfLayerWithTooManyIcs_ReturnsValidationAndCorrectionHints()
    {
        using var culture = TestCulture.UseEnUs();
        var cad = CreateLayerPads(2);
        var settings = new GridSettings { XChannels = 2, YChannels = 1, CascadeNum = 3 };

        var result = GridRebuildOrchestrator.Build(CreateLayerRequest(cad, settings));

        const string expectedHint = "Regular layer pads=2, current X*Y=2*1=2. " +
            "Keep Y=1 -> set X=2. Keep X=2 -> set Y=1. " +
            "Cascade=3 exceeds suggested X=2; set Cascade <= 2.";
        Assert.False(result.Success);
        Assert.Null(result.Grid);
        Assert.Equal("CascadeNum cannot exceed XChannels.", result.StatusText);
        Assert.Equal(expectedHint, result.DxfRegularSourceHint);
        Assert.Equal("Regular source mismatch.\n" + expectedHint, result.CanvasHint);
    }

    [Fact]
    public void Build_DxfLayerCountMismatch_SuggestsUnevenIcColumnsWithoutBuildingAGrid()
    {
        using var culture = TestCulture.UseEnUs();
        var cad = CreateLayerPads(10);
        var settings = new GridSettings { XChannels = 4, YChannels = 2, CascadeNum = 3 };

        var result = GridRebuildOrchestrator.Build(CreateLayerRequest(cad, settings));

        const string expectedHint = "Regular layer pads=10, current X*Y=4*2=8. " +
            "Keep Y=2 -> set X=5. Closest exact pair: X=5, Y=2. " +
            "With Cascade=3, suggested per-IC X: 2/2/1.";
        Assert.False(result.Success);
        Assert.Null(result.Grid);
        Assert.Equal(expectedHint, result.DxfRegularSourceHint);
        Assert.Equal("DXF regular source mismatch. " + expectedHint, result.StatusText);
        Assert.Equal("Regular source count mismatch.\n" + expectedHint, result.CanvasHint);
    }

    [Fact]
    public void BuildDxfRegularSourceMismatchHint_EquallyClosePairs_PrefersTheCloserAspectRatio()
    {
        using var culture = TestCulture.UseEnUs();

        var hint = GridRebuildOrchestrator.BuildDxfRegularSourceMismatchHint(12, x: 2, y: 2, cascade: 2);

        Assert.Equal(
            "Regular layer pads=12, current X*Y=2*2=4. " +
            "Keep Y=2 -> set X=6. Keep X=2 -> set Y=6. " +
            "Closest exact pair: X=3, Y=4. With Cascade=2, suggested per-IC X: 3/3.", hint);
    }

    private static GridRebuildBuildRequest CreateLayerRequest(
        CadPadSet cad,
        GridSettings settings,
        ProjectFile? projectFile = null,
        IReadOnlySet<int>? hiddenPadIds = null)
    {
        return new GridRebuildBuildRequest
        {
            ProjectFile = projectFile ?? new ProjectFile(),
            Settings = settings,
            RegularSourceMode = RegularSourceMode.FromDxfLayer,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            Cad = cad,
            RegularSourceLayerName = "regular",
            HiddenCadPadIds = hiddenPadIds ?? new HashSet<int>(),
            BuildCadPadSetForBounds = static () => throw new InvalidOperationException("Layer source must not request CAD bounds."),
        };
    }

    private static CadPadSet CreateLayerPads(int count)
    {
        return new CadPadSet(Enumerable.Range(0, count)
            .Select(index => TestGeometryFactory.CreateCadPad(index, "regular", index * 2, 0, index * 2 + 1, 1))
            .ToArray());
    }
}
