using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularVisibilityMaskServiceTests
{
    [Fact]
    public void Load_SyntheticCsv_BuildsActiveAndInactiveRegularSets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"regular-mask-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(
                path,
                """
                Xch:"2",Ych:"2"
                timestamp(0:0:0),DiffData,10,0
                0,0
                timestamp(0:0:1),DiffData,0,0
                0,0
                """);

            var grid = BuildRectGrid(rows: 2, cols: 2);

            var result = RegularVisibilityMaskService.Load(path, grid, DiffFrameGridRowOrigin.TopRowIsRegularLastRow);

            Assert.True(result.HasLoadedMask);
            Assert.Equal(2, result.FrameCount);
            Assert.Single(result.ActiveRegularPadIds);
            Assert.Equal(3, result.InactiveRegularPadIds.Count);
            Assert.Contains(2, result.ActiveRegularPadIds);
            Assert.DoesNotContain(0, result.ActiveRegularPadIds);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [ExampleDataTheory]
    [InlineData("example/TM 8.1/SeeRegular.csv", 18, 36)]
    [InlineData("example/BOE36.35/SeeRegular.csv", 26, 192)]
    public void Load_RealSeeRegularCsv_IsCompatibleWithMatchingGrid(string relativePath, int rows, int cols)
    {
        var repoRoot = TestPaths.RepoRoot;
        var path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Expected fixture path to exist: {path}");

        var tempPath = Path.Combine(Path.GetTempPath(), $"regular-mask-fixture-{Guid.NewGuid():N}.csv");
        File.Copy(path, tempPath, overwrite: true);

        try
        {
            var grid = BuildRectGrid(rows, cols);

            var result = RegularVisibilityMaskService.Load(tempPath, grid, DiffFrameGridRowOrigin.TopRowIsRegularLastRow);

            Assert.True(result.HasLoadedMask, result.Diagnostic);
            Assert.True(result.ActiveRegularCount > 0);
            Assert.True(result.InactiveRegularCount > 0);
            Assert.Equal(rows * cols, result.ActiveRegularCount + result.InactiveRegularCount);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
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
                var regularPadId = row * cols + col;
                pads.Add(new RegularPad(
                    row,
                    col,
                    regularPadId,
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
