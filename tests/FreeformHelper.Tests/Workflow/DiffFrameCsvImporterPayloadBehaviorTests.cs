// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DiffFrameCsvImporterPayloadBehaviorTests
{
    private static readonly double[] _firstRow = [1.25, -2.5];
    private static readonly double[] _secondRow = [3.75, 4.5];

    [Theory]
    [InlineData("00:bad:001")]
    [InlineData("00:00:00:2147483648")]
    public void ImportContent_InvalidTimestampPart_PreservesTimestampTextAndMeasurements(string timestampText)
    {
        using var culture = TestCulture.UseEnUs();
        var content = $"""
            timestamp(mm:ss:nnn):"{timestampText}",Frame index:"4",DiffData:,1.25,-2.5
            3.75,+4.5
            """;

        var result = DiffFrameCsvImporter.ImportContent("measurements.csv", content);

        var frame = Assert.Single(result.Frames);
        Assert.Equal(timestampText, frame.TimestampText);
        Assert.Null(frame.Timestamp);
        Assert.Equal(4, frame.FrameIndex);
        Assert.Equal(2, frame.RowCount);
        Assert.Equal(2, frame.ColCount);
        Assert.Equal(_firstRow, frame.Rows[0]);
        Assert.Equal(_secondRow, frame.Rows[1]);
        Assert.Empty(result.Diagnostics);
    }
}
