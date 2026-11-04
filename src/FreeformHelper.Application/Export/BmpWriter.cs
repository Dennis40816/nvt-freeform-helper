namespace FreeformHelper.Application.Export;

/// <summary>
/// Writes an uncompressed 24-bit Windows BMP from opaque BGRA pixels.
/// </summary>
/// <remarks>
/// Skia has no BMP encoder, so image export writes the file itself. The alpha byte of every
/// input pixel is ignored: composite onto a background before calling.
/// </remarks>
public static class BmpWriter
{
    private const int FileHeaderBytes = 14;
    private const int InfoHeaderBytes = 40;
    private const int HeadersBytes = FileHeaderBytes + InfoHeaderBytes;
    private const int InputBytesPerPixel = 4;
    private const int OutputBytesPerPixel = 3;
    private const int RowAlignment = 4;

    /// <summary>
    /// Gets the number of bytes in the BMP file for the given pixel size.
    /// </summary>
    public static long ComputeFileSize(int width, int height)
    {
        ValidateSize(width, height);
        return HeadersBytes + ((long)ComputeOutputRowBytes(width) * height);
    }

    /// <summary>
    /// Writes the pixels as a bottom-up 24-bit BMP.
    /// </summary>
    /// <param name="output">Destination stream. It is not closed.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="bgraPixels">Top-down pixels, four bytes each in blue, green, red, alpha order.</param>
    /// <param name="inputRowBytes">Bytes from the start of one input row to the next.</param>
    public static void WriteBgra32AsBmp24(
        Stream output,
        int width,
        int height,
        ReadOnlySpan<byte> bgraPixels,
        int inputRowBytes)
    {
        ArgumentNullException.ThrowIfNull(output);
        var fileSize = ComputeFileSize(width, height);
        if (fileSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "BMP file would be larger than 2 GiB.");
        }

        var minimumRowBytes = (long)width * InputBytesPerPixel;
        if (inputRowBytes < minimumRowBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRowBytes), "Input row is shorter than the image width.");
        }

        var requiredBytes = (inputRowBytes * (long)(height - 1)) + minimumRowBytes;
        if (bgraPixels.Length < requiredBytes)
        {
            throw new ArgumentException("Pixel buffer is smaller than the image size.", nameof(bgraPixels));
        }

        var outputRowBytes = ComputeOutputRowBytes(width);
        WriteHeaders(output, width, height, (int)fileSize, outputRowBytes * height);

        var row = new byte[outputRowBytes];
        for (var y = height - 1; y >= 0; y--)
        {
            var source = bgraPixels.Slice(y * inputRowBytes, width * InputBytesPerPixel);
            for (var x = 0; x < width; x++)
            {
                var input = x * InputBytesPerPixel;
                var target = x * OutputBytesPerPixel;
                row[target] = source[input];
                row[target + 1] = source[input + 1];
                row[target + 2] = source[input + 2];
            }

            output.Write(row, 0, row.Length);
        }
    }

    private static int ComputeOutputRowBytes(int width)
    {
        var unpadded = (long)width * OutputBytesPerPixel;
        var padded = ((unpadded + RowAlignment - 1) / RowAlignment) * RowAlignment;
        if (padded > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "BMP row is too large.");
        }

        return (int)padded;
    }

    private static void ValidateSize(int width, int height)
    {
        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 1.");
        }

        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 1.");
        }
    }

    private static void WriteHeaders(Stream output, int width, int height, int fileSize, int imageSize)
    {
        Span<byte> header = stackalloc byte[HeadersBytes];
        header.Clear();
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        WriteInt32(header, 2, fileSize);
        WriteInt32(header, 10, HeadersBytes);
        WriteInt32(header, 14, InfoHeaderBytes);
        WriteInt32(header, 18, width);
        WriteInt32(header, 22, height);
        WriteInt16(header, 26, 1);
        WriteInt16(header, 28, OutputBytesPerPixel * 8);
        WriteInt32(header, 34, imageSize);
        output.Write(header);
    }

    private static void WriteInt32(Span<byte> target, int offset, int value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(target.Slice(offset, sizeof(int)), value);
    }

    private static void WriteInt16(Span<byte> target, int offset, short value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(target.Slice(offset, sizeof(short)), value);
    }
}
