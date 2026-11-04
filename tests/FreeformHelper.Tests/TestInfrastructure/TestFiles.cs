using System.Text;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class TestFiles
{
    private static readonly Lazy<DirectoryInfo> _sharedTempRoot = new(CreateSharedTempRoot);

    public static DirectoryInfo CreateTempDirectory()
    {
        return Directory.CreateTempSubdirectory("freeform-helper-test-");
    }

    /// <summary>Returns a new file path in a folder that the test process removes when it exits.</summary>
    /// <remarks>The file does not exist yet. A test that creates it can delete it, or leave it to the cleanup at exit.</remarks>
    public static string NewTempFilePath(string prefix, string extension)
    {
        return Path.Combine(_sharedTempRoot.Value.FullName, $"{prefix}{Guid.NewGuid():N}{extension}");
    }

    public static string WriteTempCsv(string content)
    {
        var path = NewTempFilePath(string.Empty, ".csv");
        WriteText(path, content.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        return path;
    }

    public static void WriteText(string path, string content)
    {
        using var writer = new StreamWriter(path, append: false);
        writer.Write(content);
    }

    public static void WriteText(string path, string content, Encoding encoding)
    {
        using var writer = new StreamWriter(path, append: false, encoding);
        writer.Write(content);
    }

    public static async Task WriteBenchmarkOutputAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path, append: false);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    public static string ReadContent(string path)
    {
        using var reader = new StreamReader(path);
        return reader.ReadToEnd();
    }

    public static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path, append: false);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    public static async Task<string> ReadContentAsync(string path, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(path);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static async Task<byte[]> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var content = new byte[stream.Length];
        await stream.ReadExactlyAsync(content, cancellationToken);
        return content;
    }

    public static void WriteBytes(string path, byte[] content)
    {
        using var stream = File.Create(path);
        stream.Write(content);
    }

    public static byte[] ReadBytes(string path)
    {
        using var stream = File.OpenRead(path);
        var content = new byte[stream.Length];
        stream.ReadExactly(content);
        return content;
    }

    public static void CopyOverwriting(string sourcePath, string destinationPath)
    {
        using var source = File.OpenRead(sourcePath);
        using var destination = File.Create(destinationPath);
        source.CopyTo(destination);
    }

    private static DirectoryInfo CreateSharedTempRoot()
    {
        var root = CreateTempDirectory();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TestDirectoryCleanup.DeleteBestEffort(root.FullName);
        return root;
    }
}
