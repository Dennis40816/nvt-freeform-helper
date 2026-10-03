using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DiffFrameCsvImporterTests
{
    [Fact]
    public void Import_ParsesHeaderAndFrames_WithInlineMarkerPayload()
    {
        var path = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"3",Ych:"2",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            1,2,3
            4,5,6
            
            timestamp(mm:ss:nnn):"09:14:280",Frame index:"1",Break point index:"7",DiffData:
            7,8,9
            10,11,12
            """);

        var result = DiffFrameCsvImporter.Import(path);

        Assert.Equal(3, result.DeclaredCols);
        Assert.Equal(2, result.DeclaredRows);
        Assert.Equal(2, result.Frames.Count);
        Assert.Empty(result.Diagnostics);

        var frame0 = result.Frames[0];
        Assert.Equal(0, frame0.FrameIndex);
        Assert.Equal(0, frame0.BreakpointIndex);
        Assert.Equal("09:14:165", frame0.TimestampText);
        Assert.Equal(2, frame0.RowCount);
        Assert.Equal(3, frame0.ColCount);
        Assert.Collection(
            frame0.Rows[0],
            value => Assert.Equal(1d, value),
            value => Assert.Equal(2d, value),
            value => Assert.Equal(3d, value));
        Assert.Collection(
            frame0.Rows[1],
            value => Assert.Equal(4d, value),
            value => Assert.Equal(5d, value),
            value => Assert.Equal(6d, value));

        var frame1 = result.Frames[1];
        Assert.Equal(1, frame1.FrameIndex);
        Assert.Equal(7, frame1.BreakpointIndex);
        Assert.Collection(
            frame1.Rows[1],
            value => Assert.Equal(10d, value),
            value => Assert.Equal(11d, value),
            value => Assert.Equal(12d, value));
    }

    [Fact]
    public void Import_Throws_WhenFrameRowWidthsAreInconsistent()
    {
        var path = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"3",Ych:"2",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            1,2,3
            4,5
            """);

        var error = Assert.Throws<InvalidDataException>(() => DiffFrameCsvImporter.Import(path));
        Assert.Contains("inconsistent row width", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_WhenFrameFollowedByButtonData_DoesNotConsumeButtonRows()
    {
        var path = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"2",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            1,2
            3,4
            Button Data:
            5,6
            """);

        var result = DiffFrameCsvImporter.Import(path);

        Assert.Single(result.Frames);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Frames[0].RowCount);
        Assert.Equal(2, result.Frames[0].ColCount);
        Assert.Collection(
            result.Frames[0].Rows[0],
            value => Assert.Equal(1d, value),
            value => Assert.Equal(2d, value));
        Assert.Collection(
            result.Frames[0].Rows[1],
            value => Assert.Equal(3d, value),
            value => Assert.Equal(4d, value));
    }

    [Fact]
    public void ProjectFrame_WhenDimensionsMatch_MapsMatrixToRegularGrid()
    {
        var grid = BuildGrid(rows: 2, cols: 2);
        var frame = new DiffFrameCsvFrame(
            SequenceIndex: 0,
            FrameIndex: 0,
            BreakpointIndex: 0,
            TimestampText: "00:00:000",
            Timestamp: TimeSpan.Zero,
            HeaderLineNumber: 1,
            MarkerLineNumber: 1,
            DataStartLineNumber: 2,
            DataEndLineNumber: 3,
            Rows: new List<IReadOnlyList<double>>
            {
                new List<double> { 1d, 2d },
                new List<double> { 3d, 4d },
            });

        var projection = DiffFrameGridProjectionService.ProjectFrame(frame, grid);

        Assert.True(projection.IsCompatible);
        Assert.Equal(4, projection.Cells.Count);
        Assert.Equal((RegularRow: 0, RegularCol: 0, Value: 1d), (projection.Cells[0].RegularRow, projection.Cells[0].RegularCol, projection.Cells[0].Value));
        Assert.Equal((RegularRow: 1, RegularCol: 1, Value: 4d), (projection.Cells[3].RegularRow, projection.Cells[3].RegularCol, projection.Cells[3].Value));
    }

    [Fact]
    public void ProjectFrame_WhenRequested_CanFlipVerticalRowMapping()
    {
        var grid = BuildGrid(rows: 2, cols: 2);
        var frame = new DiffFrameCsvFrame(
            SequenceIndex: 0,
            FrameIndex: 0,
            BreakpointIndex: 0,
            TimestampText: "00:00:000",
            Timestamp: TimeSpan.Zero,
            HeaderLineNumber: 1,
            MarkerLineNumber: 1,
            DataStartLineNumber: 2,
            DataEndLineNumber: 3,
            Rows: new List<IReadOnlyList<double>>
            {
                new List<double> { 10d, 20d },
                new List<double> { 30d, 40d },
            });

        var projection = DiffFrameGridProjectionService.ProjectFrame(
            frame,
            grid,
            DiffFrameGridRowOrigin.TopRowIsRegularLastRow);

        Assert.True(projection.IsCompatible);
        Assert.Equal((RegularRow: 1, RegularCol: 0, Value: 10d), (projection.Cells[0].RegularRow, projection.Cells[0].RegularCol, projection.Cells[0].Value));
        Assert.Equal((RegularRow: 0, RegularCol: 1, Value: 40d), (projection.Cells[3].RegularRow, projection.Cells[3].RegularCol, projection.Cells[3].Value));
    }

    [Fact]
    public void ProjectFrame_WhenDimensionsMismatch_ReturnsDiagnostic()
    {
        var grid = BuildGrid(rows: 2, cols: 2);
        var frame = new DiffFrameCsvFrame(
            SequenceIndex: 0,
            FrameIndex: 0,
            BreakpointIndex: 0,
            TimestampText: "00:00:000",
            Timestamp: TimeSpan.Zero,
            HeaderLineNumber: 1,
            MarkerLineNumber: 1,
            DataStartLineNumber: 2,
            DataEndLineNumber: 4,
            Rows: new List<IReadOnlyList<double>>
            {
                new List<double> { 1d, 2d, 3d },
                new List<double> { 4d, 5d, 6d },
            });

        var projection = DiffFrameGridProjectionService.ProjectFrame(frame, grid);

        Assert.False(projection.IsCompatible);
        Assert.Contains("do not match", projection.Diagnostic);
        Assert.Empty(projection.Cells);
    }

    private static RegularGrid BuildGrid(int rows, int cols)
    {
        return TestGeometryFactory.CreateRegularGrid(rows, cols);
    }
}
