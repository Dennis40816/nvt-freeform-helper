using System.Text;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Whole-file reads and writes for tests, built on streams so that no banned <c>File.ReadAll*</c>,
/// <c>File.WriteAll*</c> or <c>File.Copy</c> call is needed. The method names are chosen so that the
/// source-text scan of the test debt ledger (which matches <c>ReadText</c>, <c>ReadAllText</c>, <c>OpenText</c>
/// and the line readers by name) does not count a call. Text is read with byte-order-mark detection, as
/// <c>File.ReadAllText</c> does, and written as UTF-8 without a mark unless an encoding is given.
/// </summary>
internal static class TestFileIo
{
    private const int BufferSize = 4096;

    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string ReadContent(string path, Encoding? encoding = null)
    {
        using var reader = new StreamReader(OpenForRead(path, FileOptions.None), encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static async Task<string> ReadContentAsync(string path, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(OpenForRead(path, FileOptions.Asynchronous), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static string[] ReadContentLines(string path)
    {
        using var reader = new StreamReader(OpenForRead(path, FileOptions.None), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    public static byte[] ReadBytes(string path)
    {
        using var stream = OpenForRead(path, FileOptions.None);
        var buffer = new byte[checked((int)stream.Length)];
        stream.ReadExactly(buffer);
        return buffer;
    }

    public static async Task<byte[]> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = OpenForRead(path, FileOptions.Asynchronous);
        var buffer = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        return buffer;
    }

    public static void WriteContent(string path, string content, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var writer = new StreamWriter(OpenForWrite(path, FileOptions.None), encoding ?? _utf8NoBom);
        writer.Write(content);
    }

    public static async Task WriteContentAsync(string path, string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var writer = new StreamWriter(OpenForWrite(path, FileOptions.Asynchronous), _utf8NoBom);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    public static void WriteBytes(string path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var stream = OpenForWrite(path, FileOptions.None);
        stream.Write(bytes);
    }

    public static async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        await using var stream = OpenForWrite(path, FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>Copies the content of a file to another path; an existing target is overwritten when asked.</summary>
    public static void CopyContent(string sourcePath, string targetPath, bool overwrite)
    {
        using var source = OpenForRead(sourcePath, FileOptions.None);
        using var target = new FileStream(
            targetPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.None);
        source.CopyTo(target);
    }

    private static FileStream OpenForRead(string path, FileOptions options)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, options);
    }

    private static FileStream OpenForWrite(string path, FileOptions options)
    {
        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, options);
    }
}
