// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DiffFrameCsvImporterInlineRowBehaviorTests
{
    [Fact]
    public void ImportContent_EmptyInlineFields_PreserveNumericChannelOrder()
    {
        using var culture = TestCulture.UseEnUs();
        var content = """
            Xch:"2",Ych:"2"
            timestamp(mm:ss:nnn):"00:00:001",DiffData:,," ",1.25,," -2.5 ",
            3.75,4.5
            """;

        var result = DiffFrameCsvImporter.ImportContent("measurements.csv", content);

        var frame = Assert.Single(result.Frames);
        Assert.Equal(2, result.DeclaredCols);
        Assert.Equal(2, result.DeclaredRows);
        Assert.Equal(2, frame.RowCount);
        Assert.Equal(2, frame.ColCount);
        Assert.Collection(frame.Rows[0], value => Assert.Equal(1.25, value), value => Assert.Equal(-2.5, value));
        Assert.Collection(frame.Rows[1], value => Assert.Equal(3.75, value), value => Assert.Equal(4.5, value));
        Assert.Equal(2, frame.MarkerLineNumber);
        Assert.Equal(2, frame.DataStartLineNumber);
        Assert.Equal(3, frame.DataEndLineNumber);
        Assert.Empty(result.Diagnostics);
    }
}
