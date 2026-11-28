// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadSpatialQueryPolicyTests
{
    [Fact]
    public void BuildBoundsWithNoPadsReturnsTheOriginRectangle()
    {
        Assert.Equal(new Rect2(0, 0, 0, 0), PadSpatialQueryPolicy.BuildBounds([]));
    }

    [Fact]
    public void BuildBoundsWithOneDegeneratePadReturnsItsBounds()
    {
        var pad = CreateCadPad(1, new Rect2(3, 4, 3, 9));

        Assert.Equal(pad.Bounds, PadSpatialQueryPolicy.BuildBounds([pad]));
    }

    [Fact]
    public void BuildBoundsUnionsPadsInOrderAndIgnoresAnEmptyTrailingBound()
    {
        CadPad[] pads =
        [
            CreateCadPad(1, new Rect2(-2, 3, 4, 9)),
            CreateCadPad(2, new Rect2(5, -4, 8, 1)),
            CreateCadPad(3, new Rect2(100, 100, 100, 100)),
        ];

        Assert.Equal(new Rect2(-2, -4, 8, 9), PadSpatialQueryPolicy.BuildBounds(pads));
    }

    [Theory]
    [InlineData(9.9999999, 0)]
    [InlineData(10, 0)]
    [InlineData(14.9999999, 0)]
    [InlineData(15, 1)]
    [InlineData(22, 2)]
    [InlineData(29.9999999, 3)]
    [InlineData(30, 3)]
    [InlineData(30.0000001, 3)]
    public void GetColumnClampsOutsideAndFloorsInteriorCoordinates(double x, int expected)
    {
        var bounds = new Rect2(10, -20, 30, 20);

        Assert.Equal(expected, PadSpatialQueryPolicy.GetColumn(x, bounds, 4, 5));
    }

    [Theory]
    [InlineData(-20.0000001, 0)]
    [InlineData(-20, 0)]
    [InlineData(-15.0000001, 0)]
    [InlineData(-15, 1)]
    [InlineData(2, 4)]
    [InlineData(19.9999999, 7)]
    [InlineData(20, 7)]
    [InlineData(20.0000001, 7)]
    public void GetRowClampsOutsideAndFloorsInteriorCoordinates(double y, int expected)
    {
        var bounds = new Rect2(10, -20, 30, 20);

        Assert.Equal(expected, PadSpatialQueryPolicy.GetRow(y, bounds, 8, 5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void SingleOrEmptyCadAxisReturnsZeroBeforeUsingCellSize(int count)
    {
        var bounds = new Rect2(3, 4, 3, 4);

        Assert.Equal(0, PadSpatialQueryPolicy.GetColumn(100, bounds, count, 0));
        Assert.Equal(0, PadSpatialQueryPolicy.GetRow(-100, bounds, count, 0));
    }

    [Fact]
    public void ValidateSeparableGridAcceptsEmptyInput()
    {
        Assert.True(PadSpatialQueryPolicy.ValidateSeparableGrid([], 0, 0, [], [], [], []));
    }

    [Fact]
    public void ValidateSeparableGridNormalizesRowsAndColumns()
    {
        RegularPad[] pads =
        [
            CreateRegularPad(7, 9, new Rect2(0, 0, 10, 10)),
            CreateRegularPad(7, 10, new Rect2(10, 0, 20, 10)),
            CreateRegularPad(8, 9, new Rect2(0, 10, 10, 20)),
            CreateRegularPad(8, 10, new Rect2(10, 10, 20, 20)),
        ];

        Assert.True(PadSpatialQueryPolicy.ValidateSeparableGrid(pads, 7, 9, [0, 10], [10, 20], [0, 10], [10, 20]));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    public void ValidateSeparableGridRejectsOutOfRangeRowsAndColumns(int row, int col)
    {
        var pad = CreateRegularPad(row, col, new Rect2(0, 0, 1, 1));

        Assert.False(PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [0], [1], [0], [1]));
    }

    [Theory]
    [InlineData(0.000000999, true)]
    [InlineData(0.000001, true)]
    [InlineData(0.000001001, false)]
    public void ValidateSeparableGridAppliesTheInclusiveToleranceToEveryBound(double delta, bool expected)
    {
        var pad = CreateRegularPad(0, 0, new Rect2(0, 0, 0, 0));

        Assert.Equal(expected, PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [delta], [0], [0], [0]));
        Assert.Equal(expected, PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [0], [delta], [0], [0]));
        Assert.Equal(expected, PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [0], [0], [delta], [0]));
        Assert.Equal(expected, PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [0], [0], [0], [delta]));
    }

    [Fact]
    public void ValidateSeparableGridKeepsTheExistingShortCircuitAndArrayException()
    {
        var pad = CreateRegularPad(0, 0, new Rect2(0, 0, 1, 1));

        Assert.False(PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [], [], [], []));
        Assert.False(PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [2], [], [0], []));
        Assert.Throws<IndexOutOfRangeException>(() =>
            PadSpatialQueryPolicy.ValidateSeparableGrid([pad], 0, 0, [0], [], [0], [1]));
    }

    [Theory]
    [InlineData(-0.0000001, -1, 0, 0)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(5, 0, 0, 0)]
    [InlineData(9.9999999, 0, 0, 0)]
    [InlineData(10, 1, 0, 1)]
    [InlineData(10.0000001, 1, 1, 1)]
    [InlineData(15, 1, 1, 1)]
    [InlineData(20, 1, 1, 1)]
    [InlineData(20.0000001, -1, 1, 1)]
    public void CellLookupsPreserveInteriorEdgeAndOutsideResults(double value, int bucket, int first, int last)
    {
        double[] edges = [0, 10, 20];

        Assert.Equal(bucket, PadSpatialQueryPolicy.FindBucket(edges, value));
        Assert.Equal(first, PadSpatialQueryPolicy.FindFirstCell(edges, value));
        Assert.Equal(last, PadSpatialQueryPolicy.FindLastCell(edges, value));
    }

    [Theory]
    [InlineData(3, -1)]
    [InlineData(4, 0)]
    [InlineData(6, 0)]
    [InlineData(9, 0)]
    [InlineData(10, -1)]
    public void SingleCellLookupsPreserveOutsideAndInclusiveEdgeResults(double value, int bucket)
    {
        double[] edges = [4, 9];

        Assert.Equal(bucket, PadSpatialQueryPolicy.FindBucket(edges, value));
        Assert.Equal(0, PadSpatialQueryPolicy.FindFirstCell(edges, value));
        Assert.Equal(0, PadSpatialQueryPolicy.FindLastCell(edges, value));
    }

    [Fact]
    public void CellLookupsWithEmptyEdgesKeepThrowing()
    {
        Assert.Throws<IndexOutOfRangeException>(() => PadSpatialQueryPolicy.FindBucket([], 0));
        Assert.Throws<IndexOutOfRangeException>(() => PadSpatialQueryPolicy.FindFirstCell([], 0));
        Assert.Throws<IndexOutOfRangeException>(() => PadSpatialQueryPolicy.FindLastCell([], 0));
    }

    private static CadPad CreateCadPad(int id, Rect2 bounds)
    {
        return new CadPad(id, "CAD", "L1", CreatePolygon(bounds));
    }

    private static RegularPad CreateRegularPad(int row, int col, Rect2 bounds)
    {
        return new RegularPad(row, col, (row * 2) + col, CreatePolygon(bounds));
    }

    private static Polygon2 CreatePolygon(Rect2 bounds)
    {
        return new Polygon2(
            [
                new Point2(bounds.MinX, bounds.MinY), new Point2(bounds.MaxX, bounds.MinY),
                new Point2(bounds.MaxX, bounds.MaxY), new Point2(bounds.MinX, bounds.MaxY),
            ]);
    }
}
