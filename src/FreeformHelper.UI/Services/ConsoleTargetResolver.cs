namespace FreeformHelper.UI.Services;

/// <summary>
/// The result of resolving the path text of a console link.
/// </summary>
/// <param name="FullPath">The full path of an existing file or folder, or null when nothing was found.</param>
/// <param name="IsDirectory">Whether <paramref name="FullPath"/> is a folder.</param>
internal readonly record struct ConsoleTargetResolution(string? FullPath, bool IsDirectory)
{
    /// <summary>Gets a value indicating whether the target exists.</summary>
    public bool Exists => FullPath is not null;
}

/// <summary>
/// Resolves the path text of a console link to an existing file or folder.
/// The method reads the file system. Run it off the UI thread.
/// </summary>
internal static class ConsoleTargetResolver
{
    private const int MaxFileNameMatches = 32;

    /// <summary>
    /// Resolves a link target. A rooted path is checked as it is.
    /// A relative path is tried against the working directory, the application folder and the repository root.
    /// A bare file name is also searched under the repository root.
    /// </summary>
    /// <param name="rawTarget">The path text of the link.</param>
    public static ConsoleTargetResolution Resolve(string rawTarget)
    {
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return default;
        }

        var target = rawTarget.Trim().Trim('"');
        if (Path.IsPathRooted(target))
        {
            return Probe(Path.GetFullPath(target));
        }

        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var found = Probe(Path.GetFullPath(target, root));
            if (found.Exists)
            {
                return found;
            }
        }

        var repoRoot = FindRepositoryRoot();
        return repoRoot is null ? default : ResolveUnderRepository(target, repoRoot);
    }

    private static ConsoleTargetResolution ResolveUnderRepository(string target, string repoRoot)
    {
        var normalized = target.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var found = Probe(Path.GetFullPath(normalized, repoRoot));
        if (found.Exists || target.Contains('/') || target.Contains('\\'))
        {
            return found;
        }

        var shortest = EnumerateFileNameMatches(repoRoot, target)
            .OrderBy(static path => path.Length)
            .FirstOrDefault();
        return shortest is null ? default : new ConsoleTargetResolution(shortest, false);
    }

    private static ConsoleTargetResolution Probe(string fullPath)
    {
        if (Directory.Exists(fullPath))
        {
            return new ConsoleTargetResolution(fullPath, true);
        }

        return File.Exists(fullPath) ? new ConsoleTargetResolution(fullPath, false) : default;
    }

    private static string? FindRepositoryRoot()
    {
        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var seed in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory, userDirectory })
        {
            var root = WalkUpToRepositoryRoot(seed);
            if (root is not null)
            {
                return root;
            }
        }

        return null;
    }

    private static string? WalkUpToRepositoryRoot(string? seed)
    {
        if (string.IsNullOrWhiteSpace(seed) || !Directory.Exists(seed))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(seed));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FreeformHelper.sln"))
                || Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string[] EnumerateFileNameMatches(string repoRoot, string fileName)
    {
        try
        {
            return Directory.EnumerateFiles(repoRoot, fileName, SearchOption.AllDirectories)
                .Take(MaxFileNameMatches)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
