// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, 180)]
    [InlineData(-180, 180)]
    [InlineData(181, -179)]
    [InlineData(360, 0)]
    [InlineData(540, 180)]
    [InlineData(-190, 170)]
    [InlineData(1e-10, 0)]
    [InlineData(-1e-10, 0)]
    public void RotationDegreesWrapAndSnapNearZero(double input, double expected)
    {
        var actual = DxfEditRules.NormalizeRotationDegrees(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(5e-7, 0, true)]
    [InlineData(0, 5e-7, true)]
    [InlineData(2e-6, 0, false)]
    [InlineData(0, 2e-6, false)]
    public void PolygonEquivalenceComparesBothCoordinatesWithinDefaultEpsilon(double firstX, double firstY, bool expected)
    {
        var left = CreatePolygon();
        var right = CreatePolygon(firstX, firstY);

        var actual = DxfEditRules.ArePolygonVerticesEquivalent(left, right);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PolygonEquivalenceRejectsDifferentVertexCounts()
    {
        var rectangle = CreatePolygon();
        var triangle = new Polygon2([new Point2(0, 0), new Point2(2, 0), new Point2(2, 1)]);

        Assert.False(DxfEditRules.ArePolygonVerticesEquivalent(rectangle, triangle));
    }

    [Theory]
    [InlineData(1e-4, true)]
    [InlineData(1e-6, false)]
    public void PolygonEquivalenceUsesCustomEpsilon(double epsilon, bool expected)
    {
        var left = CreatePolygon();
        var right = CreatePolygon(5e-5, 0);

        var actual = DxfEditRules.ArePolygonVerticesEquivalent(left, right, epsilon);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t\r\n", "")]
    [InlineData("  Pads  ", "Pads")]
    public void LayerNamesAreTrimmedAndMissingNamesAreEmpty(string? input, string expected)
    {
        var actual = DxfEditRules.NormalizeLayerName(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RotationSelectionKeepsDistinctVisibleBaselineIds()
    {
        int[] selected = [1, 2, 3, 4, 1, 4];
        HashSet<int> baseline = [1, 2, 4];

        var ids = DxfEditRules.ResolveRotationSelectedCadIds(selected, baseline, static id => id == 2);

        Assert.Collection(ids.Order(),
            static id => Assert.Equal(1, id),
            static id => Assert.Equal(4, id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void RotationTargetWithBlankLayerNameIsEmpty(string? layerName)
    {
        var cad = new CadPadSet([CreatePad(1, "Pads")]);
        HashSet<int> baseline = [1];

        var ids = DxfEditRules.ResolveRotationTargetLayerCadIds(cad, baseline, layerName, static _ => false);

        Assert.Empty(ids);
    }

    [Theory]
    [InlineData("Pads")]
    [InlineData("PADS")]
    [InlineData("  pAdS  ")]
    public void RotationTargetKeepsVisibleBaselinePadsWithTrimmedCaseInsensitiveLayerName(string layerName)
    {
        var cad = new CadPadSet(
        [
            CreatePad(1, "Pads"), CreatePad(2, "Pads"), CreatePad(3, "Other"),
            CreatePad(4, "Pads"), CreatePad(5, "pAdS"),
        ]);
        HashSet<int> baseline = [1, 2, 3, 5];

        var ids = DxfEditRules.ResolveRotationTargetLayerCadIds(cad, baseline, layerName, static id => id == 2);

        Assert.Collection(ids.Order(),
            static id => Assert.Equal(1, id),
            static id => Assert.Equal(5, id));
    }

    private static Polygon2 CreatePolygon(double firstX = 0, double firstY = 0) => new(
    [
        new Point2(firstX, firstY), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1),
    ]);

    private static CadPad CreatePad(int id, string layer) => new(id, $"Pad {id}", layer, CreatePolygon());
}
