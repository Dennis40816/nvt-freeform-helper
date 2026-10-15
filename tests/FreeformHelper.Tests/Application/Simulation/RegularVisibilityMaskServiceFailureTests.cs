using System.Runtime.CompilerServices;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how <see cref="RegularVisibilityMaskService"/> reports a CSV that cannot be turned into a mask.
/// </summary>
public sealed class RegularVisibilityMaskServiceFailureTests
{
    // An existing file that holds no CSV frames, so the path branch is taken without creating anything.
    private static string ExistingNonCsvPath => typeof(RegularVisibilityMaskServiceFailureTests).Assembly.Location;

    [Fact]
    public void LoadEmbedded_WhenFrameHasNoNumericRows_ReturnsFailedWithImporterMessage()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("timestamp(0:0:0),DiffData\ntimestamp(0:0:1),DiffData\n");

        var result = RegularVisibilityMaskService.LoadEmbedded(content, "mask.csv", BuildRectGrid(2, 2));

        Assert.Equal(RegularVisibilityMaskStatus.Failed, result.Status);
        Assert.Equal("mask.csv", result.SourcePath);
        Assert.Contains("does not contain numeric rows", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadEmbedded_WhenRowWidthsDiffer_ReturnsFailedWithImporterMessage()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("timestamp(0:0:0),DiffData,1,2\n3\n");

        var result = RegularVisibilityMaskService.LoadEmbedded(content, "mask.csv", BuildRectGrid(2, 2));

        Assert.Equal(RegularVisibilityMaskStatus.Failed, result.Status);
        Assert.Contains("inconsistent row width", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadEmbedded_WhenTextHasNoFrames_ReturnsFailed()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("not a csv\n");

        var result = RegularVisibilityMaskService.LoadEmbedded(content, null, BuildRectGrid(2, 2));

        Assert.Equal(RegularVisibilityMaskStatus.Failed, result.Status);
        Assert.Equal("embedded SeeRegular.csv", result.SourcePath);
        Assert.Equal("CSV does not contain any frames.", result.Diagnostic);
    }

    [Fact]
    public void LoadEmbedded_WhenContentIsEmpty_ReturnsNotFound()
    {
        var result = RegularVisibilityMaskService.LoadEmbedded([], "mask.csv", BuildRectGrid(2, 2));

        Assert.Equal(RegularVisibilityMaskStatus.NotFound, result.Status);
    }

    [Fact]
    public void Load_WhenFileHoldsNoFrames_ReturnsFailed()
    {
        var result = RegularVisibilityMaskService.Load(ExistingNonCsvPath, BuildRectGrid(2, 2));

        Assert.Equal(RegularVisibilityMaskStatus.Failed, result.Status);
        Assert.Equal(ExistingNonCsvPath, result.SourcePath);
        Assert.Equal("CSV does not contain any frames.", result.Diagnostic);
    }

    [Fact]
    public void ReadAndProject_WhenReadFailsWithExpectedException_ReturnsFailedWithItsMessage()
    {
        var result = RegularVisibilityMaskService.ReadAndProject(
            static () => throw new IOException("disk read failed"),
            "mask.csv",
            BuildRectGrid(2, 2),
            DiffFrameGridRowOrigin.TopRowIsRegularLastRow);

        Assert.Equal(RegularVisibilityMaskStatus.Failed, result.Status);
        Assert.Equal("mask.csv", result.SourcePath);
        Assert.Equal("disk read failed", result.Diagnostic);
    }

    [Fact]
    public void ReadAndProject_WhenReadFailsWithUnexpectedRecoverableException_ReturnsFailed()
    {
        var result = RegularVisibilityMaskService.ReadAndProject(
            static () => throw new InvalidOperationException("unexpected"),
            "mask.csv",
            BuildRectGrid(2, 2),
            DiffFrameGridRowOrigin.TopRowIsRegularLastRow);

        Assert.Equal(RegularVisibilityMaskStatus.Failed, result.Status);
        Assert.Equal("unexpected", result.Diagnostic);
    }

    [Fact]
    public void ReadAndProject_WhenReadRunsOutOfMemory_Propagates()
    {
        // CA2201 forbids creating the type directly, and the message is never read.
        var outOfMemory = (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

        var thrown = Assert.Throws<OutOfMemoryException>(() => RegularVisibilityMaskService.ReadAndProject(
            () => throw outOfMemory,
            "mask.csv",
            BuildRectGrid(2, 2),
            DiffFrameGridRowOrigin.TopRowIsRegularLastRow));

        Assert.Same(outOfMemory, thrown);
    }

    private static RegularGrid BuildRectGrid(int rows, int cols)
    {
        var pads = new List<RegularPad>(rows * cols);
        var xEdges = Enumerable.Range(0, cols + 1).Select(static value => (double)value).ToArray();
        var yEdges = Enumerable.Range(0, rows + 1).Select(static value => (double)value).ToArray();

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                pads.Add(new RegularPad(
                    row,
                    col,
                    row * cols + col,
                    new Polygon2(new[]
                    {
                        new Point2(col, row),
                        new Point2(col + 1, row),
                        new Point2(col + 1, row + 1),
                        new Point2(col, row + 1),
                    })));
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }
}
