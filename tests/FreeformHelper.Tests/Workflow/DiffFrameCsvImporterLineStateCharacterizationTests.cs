using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Characterizes the per-line state machine of <see cref="DiffFrameCsvImporter"/>:
/// line numbers, marker handling, section headers, header fields and frame sequencing.
/// </summary>
public sealed class DiffFrameCsvImporterLineStateCharacterizationTests
{
    private const string SourceName = "sample.csv";

    [Fact]
    public void ImportContent_InlinePayloadOnTimestampLine_StartsFrameWithMarkerAndData()
    {
        var content = """
            Xch:"2",Ych:"1"
            timestamp(mm:ss:nnn):"01:02:003",Frame index:"5",Break point index:"6",DiffData:,1,2
            """;

        var result = DiffFrameCsvImporter.ImportContent(SourceName, content);

        var frame = Assert.Single(result.Frames);
        Assert.Equal(0, frame.SequenceIndex);
        Assert.Equal(5, frame.FrameIndex);
        Assert.Equal(6, frame.BreakpointIndex);
        Assert.Equal("01:02:003", frame.TimestampText);
        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(2) + TimeSpan.FromMilliseconds(3), frame.Timestamp);
        Assert.Equal((2, 2, 2, 2), (frame.HeaderLineNumber, frame.MarkerLineNumber, frame.DataStartLineNumber, frame.DataEndLineNumber));
        AssertRow(Assert.Single(frame.Rows), 1d, 2d);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ImportContent_MarkerOnItsOwnLine_RecordsDiagnosticAndFollowingRows()
    {
        var content = """
            timestamp(mm:ss:nnn):"00:00:000",Frame index:"x"
            DiffData:
            1,2
            3,4
            """;

        var result = DiffFrameCsvImporter.ImportContent(SourceName, content);

        var frame = Assert.Single(result.Frames);
        Assert.Null(frame.FrameIndex);
        Assert.Null(frame.BreakpointIndex);
        Assert.Equal((1, 2, 3, 4), (frame.HeaderLineNumber, frame.MarkerLineNumber, frame.DataStartLineNumber, frame.DataEndLineNumber));
        Assert.Equal(2, frame.RowCount);
        Assert.Equal("[sample.csv:2] DiffData marker found without inline numeric payload.", Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void ImportContent_InlineMarkerRowAfterHeaderLine_UsesMarkerLineAsDataStart()
    {
        var content = """
            timestamp(mm:ss:nnn):"00:00:001"
            DiffData:,7,8
            9,10
            """;

        var result = DiffFrameCsvImporter.ImportContent(SourceName, content);

        var frame = Assert.Single(result.Frames);
        Assert.Equal((1, 2, 2, 3), (frame.HeaderLineNumber, frame.MarkerLineNumber, frame.DataStartLineNumber, frame.DataEndLineNumber));
        AssertRow(frame.Rows[0], 7d, 8d);
        AssertRow(frame.Rows[1], 9d, 10d);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ImportContent_BlankLinesSectionHeadersAndLaterFrames_KeepLineNumbersAndHeaderFields()
    {
        var content = """
            LogVer:"1.0",Xch:"2",Ych:"2"
            timestamp(mm:ss:nnn):"00:00:001",Frame index:"0",DiffData:
            1,2

            3,4
            Button Data:
            Extra:"keep"
            timestamp(mm:ss:nnn):"00:00:002",Frame index:"1",DiffData:
            5,6
            7,8
            """;

        var result = DiffFrameCsvImporter.ImportContent(SourceName, content);

        Assert.Equal(2, result.DeclaredCols);
        Assert.Equal(2, result.DeclaredRows);
        Assert.Equal("keep", result.HeaderFields["Extra"]);
        Assert.Equal("1.0", result.HeaderFields["logver"]);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Frames.Count);

        var first = result.Frames[0];
        Assert.Equal((0, 2, (int?)null, 3, 5), (first.SequenceIndex, first.HeaderLineNumber, first.MarkerLineNumber, first.DataStartLineNumber, first.DataEndLineNumber));
        Assert.Equal(2, first.RowCount);

        var second = result.Frames[1];
        Assert.Equal((1, 1, 8, (int?)null, 9, 10), (second.SequenceIndex, second.FrameIndex, second.HeaderLineNumber, second.MarkerLineNumber, second.DataStartLineNumber, second.DataEndLineNumber));
        AssertRow(second.Rows[1], 7d, 8d);
    }

    [Fact]
    public void ImportContent_ConsecutiveTimestampLines_ThrowsForTheEmptyFrame()
    {
        var content = """
            timestamp(a):"1"
            timestamp(b):"2"
            1,2
            """;

        var error = Assert.Throws<InvalidDataException>(() => DiffFrameCsvImporter.ImportContent(SourceName, content));

        Assert.Equal("[sample.csv] Frame 0 at line 1 does not contain numeric rows.", error.Message);
    }

    [Fact]
    public void ImportContent_TrailingFrameWithoutRows_ThrowsAtEndOfInput()
    {
        var content = """
            timestamp(a):"1"
            1,2
            timestamp(b):"2"
            """;

        var error = Assert.Throws<InvalidDataException>(() => DiffFrameCsvImporter.ImportContent(SourceName, content));

        Assert.Equal("[sample.csv] Frame 1 at line 3 does not contain numeric rows.", error.Message);
    }

    [Fact]
    public void ImportContent_InconsistentRowWidth_ReportsFrameSequenceAndRowIndex()
    {
        var content = """
            timestamp(a):"1"
            1,2,3
            4,5,6
            7,8
            """;

        var error = Assert.Throws<InvalidDataException>(() => DiffFrameCsvImporter.ImportContent(SourceName, content));

        Assert.Equal("[sample.csv] Frame 0 has inconsistent row width at row 2: 2 != 3.", error.Message);
    }

    [Theory]
    [InlineData("01:02:03:004", 3723004)]
    [InlineData("09:14:165", 554165)]
    public void ImportContent_TimestampText_ParsesThreeAndFourPartForms(string text, long expectedMilliseconds)
    {
        var result = DiffFrameCsvImporter.ImportContent(SourceName, $"timestamp(x):\"{text}\"\n1,2");

        var frame = Assert.Single(result.Frames);
        Assert.Equal(text, frame.TimestampText);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), frame.Timestamp);
    }

    [Fact]
    public void ImportContent_UnparseableTimestamp_KeepsTextWithoutTimeSpan()
    {
        var result = DiffFrameCsvImporter.ImportContent(SourceName, "timestamp(x):\"ab:cd\"\n1,2");

        var frame = Assert.Single(result.Frames);
        Assert.Equal("ab:cd", frame.TimestampText);
        Assert.Null(frame.Timestamp);
    }

    [Fact]
    public void ImportContent_CustomPrefixAndMarker_AreHonored()
    {
        var options = new DiffFrameCsvImportOptions(TimestampPrefix: "frame@", DiffDataMarker: "Payload");

        var result = DiffFrameCsvImporter.ImportContent(SourceName, "frame@1,Payload:,3,4\n5,6", options);

        var frame = Assert.Single(result.Frames);
        Assert.Equal((1, 1, 1, 2), (frame.HeaderLineNumber, frame.MarkerLineNumber, frame.DataStartLineNumber, frame.DataEndLineNumber));
        AssertRow(frame.Rows[0], 3d, 4d);
        AssertRow(frame.Rows[1], 5d, 6d);
    }

    [Fact]
    public void ImportContent_HeaderOnly_ReturnsNoFramesAndParsesDeclaredSize()
    {
        var result = DiffFrameCsvImporter.ImportContent(SourceName, "Xch:\"12\",Ych:\"abc\"\nnote: (a:b) tail");

        Assert.Empty(result.Frames);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(12, result.DeclaredCols);
        Assert.Null(result.DeclaredRows);
        Assert.Equal("abc", result.HeaderFields["Ych"]);
        Assert.Equal("(a:b) tail", result.HeaderFields["note"]);
    }

    private static void AssertRow(IReadOnlyList<double> actual, params double[] expected)
    {
        Assert.Equal(expected, actual);
    }
}
