using System.Buffers.Binary;
using FreeformHelper.Application.Export;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class BmpWriterTests
{
    [Fact]
    public void Write_ProducesBmpHeadersForSizeAndDepth()
    {
        // 3 pixels per row need 9 bytes, padded to 12.
        var pixels = new byte[3 * 2 * 4];
        using var output = new MemoryStream();

        BmpWriter.WriteBgra32AsBmp24(output, 3, 2, pixels, inputRowBytes: 12);

        var bytes = output.ToArray();
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
        Assert.Equal(54 + (12 * 2), bytes.Length);
        Assert.Equal(bytes.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(54, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(10)));
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(14)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(26)));
        Assert.Equal(24, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(28)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(30)));
        Assert.Equal(12 * 2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(34)));
    }

    [Fact]
    public void Write_StoresRowsBottomUpAsBgrAndZeroPadsRows()
    {
        // Top row: red, green. Bottom row: blue, white. Input order is B, G, R, A.
        byte[] pixels =
        [
            0, 0, 255, 255, 0, 255, 0, 255,
            255, 0, 0, 255, 255, 255, 255, 255,
        ];
        using var output = new MemoryStream();

        BmpWriter.WriteBgra32AsBmp24(output, 2, 2, pixels, inputRowBytes: 8);

        var bytes = output.ToArray();
        // 2 pixels are 6 bytes, padded to 8. The first stored row is the bottom image row.
        Assert.Equal(54 + 16, bytes.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 255, 255, 0, 0 }, bytes[54..62]);
        Assert.Equal(new byte[] { 0, 0, 255, 0, 255, 0, 0, 0 }, bytes[62..70]);
    }

    [Fact]
    public void Write_IgnoresAlphaAndRowStrideGap()
    {
        // The row stride is 12 bytes for 2 pixels. The last 4 bytes of each row are not part of the image.
        byte[] pixels =
        [
            10, 20, 30, 0, 40, 50, 60, 7, 99, 99, 99, 99,
            70, 80, 90, 255, 1, 2, 3, 128, 99, 99, 99, 99,
        ];
        using var output = new MemoryStream();

        BmpWriter.WriteBgra32AsBmp24(output, 2, 2, pixels, inputRowBytes: 12);

        var bytes = output.ToArray();
        Assert.Equal(new byte[] { 70, 80, 90, 1, 2, 3, 0, 0 }, bytes[54..62]);
        Assert.Equal(new byte[] { 10, 20, 30, 40, 50, 60, 0, 0 }, bytes[62..70]);
    }

    [Fact]
    public void ComputeFileSize_MatchesWrittenLength()
    {
        var pixels = new byte[5 * 3 * 4];
        using var output = new MemoryStream();

        BmpWriter.WriteBgra32AsBmp24(output, 5, 3, pixels, inputRowBytes: 20);

        Assert.Equal(output.Length, BmpWriter.ComputeFileSize(5, 3));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void Write_RejectsEmptySize(int width, int height)
    {
        using var output = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BmpWriter.WriteBgra32AsBmp24(output, width, height, new byte[16], inputRowBytes: 4));
    }

    [Fact]
    public void Write_RejectsShortRowStride()
    {
        using var output = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BmpWriter.WriteBgra32AsBmp24(output, 2, 1, new byte[8], inputRowBytes: 4));
    }

    [Fact]
    public void Write_RejectsBufferSmallerThanImage()
    {
        using var output = new MemoryStream();

        Assert.Throws<ArgumentException>(() =>
            BmpWriter.WriteBgra32AsBmp24(output, 2, 2, new byte[12], inputRowBytes: 8));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void ComputeFileSize_ReportsSizesAboveTwoGigabytesWithoutAllocating()
    {
        Assert.Equal(54L + (300_000L * 100_000L), BmpWriter.ComputeFileSize(100_000, 100_000));
    }

    [Fact]
    public void Write_RejectsAFileLargerThanTwoGigabytesBeforeReadingPixels()
    {
        using var output = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BmpWriter.WriteBgra32AsBmp24(output, 100_000, 100_000, ReadOnlySpan<byte>.Empty, inputRowBytes: 400_000));
        Assert.Equal(0, output.Length);
    }
}
