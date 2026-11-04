namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// A scratch folder for files that a test creates. It lives under the test output directory instead of the
/// machine temporary folder, so a test never writes outside the build tree and a crashed run leaves nothing
/// in a shared location. <see cref="Create"/> gives one folder per test that <see cref="Dispose"/> deletes;
/// <see cref="NewFilePath"/> gives a unique path in a per-process folder that is deleted when the process exits.
/// </summary>
internal sealed class TestWorkspace : IDisposable
{
    private const string RootFolderName = "test-workspaces";

    private static readonly Lazy<string> _processRoot = new(CreateProcessRoot);

    private TestWorkspace(string directory)
    {
        Directory = directory;
    }

    /// <summary>Gets the full path of the folder of this workspace. The folder exists until <see cref="Dispose"/>.</summary>
    public string Directory { get; }

    /// <summary>Creates a new, empty workspace folder.</summary>
    public static TestWorkspace Create()
    {
        return new TestWorkspace(CreateUniqueDirectory(_processRoot.Value));
    }

    /// <summary>
    /// Returns a unique path for a file that does not exist yet. The file is in a per-process folder that is
    /// deleted at process exit, so a call site that already deletes its own file keeps doing so.
    /// </summary>
    public static string NewFilePath(string prefix, string extension)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ArgumentNullException.ThrowIfNull(extension);

        var normalizedExtension = extension.Length == 0 || extension[0] == '.' ? extension : "." + extension;
        return Path.Combine(_processRoot.Value, $"{prefix}-{Guid.NewGuid():N}{normalizedExtension}");
    }

    /// <summary>Returns the full path of a file name inside the workspace folder. The file is not created.</summary>
    public string PathFor(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        if (Path.IsPathRooted(fileName))
        {
            throw new ArgumentException("A workspace path must be relative.", nameof(fileName));
        }

        return Path.Combine(Directory, fileName);
    }

    /// <summary>Creates a sub folder of the workspace (and the folders above it) and returns its full path.</summary>
    public string CreateSubdirectory(string relativePath)
    {
        var path = PathFor(relativePath);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Deletes the workspace folder; a locked file does not fail the test.</summary>
    public void Dispose()
    {
        TestDirectoryCleanup.DeleteBestEffort(Directory);
    }

    private static string CreateProcessRoot()
    {
        var root = CreateUniqueDirectory(Path.Combine(AppContext.BaseDirectory, RootFolderName));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TestDirectoryCleanup.DeleteBestEffort(root);
        return root;
    }

    private static string CreateUniqueDirectory(string parent)
    {
        var path = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(path);
        return path;
    }
}
