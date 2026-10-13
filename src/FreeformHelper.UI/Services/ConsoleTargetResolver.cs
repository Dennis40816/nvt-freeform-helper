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
        if (IsNetworkOrDevicePath(target))
        {
            // A probe of a UNC path opens a network connection that sends the user's credentials to the named host.
            return default;
        }

        try
        {
            return ResolveLocal(target);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException
                                              or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return default;
        }
    }

    // Windows reads "/" and "\" as the same separator, so "/\host\share" is a network path as well.
    private static bool IsNetworkOrDevicePath(string path)
    {
        return path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]);
    }

    // After GetFullPath only a drive-letter path is probed. An NT or device prefix such as \??\UNC\host\share
    // or \\.\ can reach the network redirector, so every path that is not a drive-letter path is refused.
    private static bool IsLocalDrivePath(string fullPath)
    {
        return !OperatingSystem.IsWindows()
            || (fullPath.Length >= 3 && char.IsAsciiLetter(fullPath[0]) && fullPath[1] == ':' && IsSeparator(fullPath[2]));
    }

    private static bool IsSeparator(char value)
    {
        return value == '/' || value == '\\';
    }

    private static ConsoleTargetResolution ResolveLocal(string target)
    {
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
        if (found.Exists || target.Contains('/', StringComparison.Ordinal) || target.Contains('\\', StringComparison.Ordinal))
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
        // One choke point for every route: an existence check never reaches a network or device path.
        if (!IsLocalDrivePath(fullPath))
        {
            return default;
        }

        if (Directory.Exists(fullPath))
        {
            return new ConsoleTargetResolution(fullPath, true);
        }

        return File.Exists(fullPath) ? new ConsoleTargetResolution(fullPath, false) : default;
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var seed in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
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
