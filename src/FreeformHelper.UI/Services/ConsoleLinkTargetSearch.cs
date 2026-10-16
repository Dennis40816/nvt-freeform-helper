using System.Security;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Lookups behind the console links: the link under a position and the files of a repository. The operating
/// system call is passed in as a delegate, so a test can make it fail. A failure that the called API documents
/// gives the empty result. Any other failure, out-of-memory included, reaches the caller.
/// </summary>
internal static class ConsoleLinkTargetSearch
{
    private const int MaxRepoMatches = 32;

    /// <summary>
    /// Finds the link at the offset that <paramref name="getOffset"/> returns. A position that is outside the
    /// document makes <see cref="ArgumentOutOfRangeException"/>, and then no link is found.
    /// </summary>
    public static ConsoleLinkSpan? FindLinkAt(IReadOnlyList<ConsoleLinkSpan> links, Func<int> getOffset)
    {
        ArgumentNullException.ThrowIfNull(getOffset);

        try
        {
            return ConsoleLinkParser.FindAtOffset(links, getOffset());
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Lists at most 32 files with the name under the repository root. A listing that fails gives none.</summary>
    public static string[] EnumerateRepoMatches(string repoRoot, string fileName) =>
        EnumerateRepoMatches(repoRoot, fileName, Directory.EnumerateFiles);

    // The failures that Directory.EnumerateFiles documents give no match. DirectoryNotFoundException and
    // PathTooLongException are IOException.
    public static string[] EnumerateRepoMatches(
        string repoRoot,
        string fileName,
        Func<string, string, SearchOption, IEnumerable<string>> enumerateFiles)
    {
        ArgumentNullException.ThrowIfNull(enumerateFiles);

        try
        {
            return enumerateFiles(repoRoot, fileName, SearchOption.AllDirectories)
                .Take(MaxRepoMatches)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return [];
        }
        catch (SecurityException)
        {
            return [];
        }
    }

    /// <summary>Walks up from the seed to the first directory that holds the solution file or a .git folder.</summary>
    public static string? WalkUpToRepoRoot(string? seed)
    {
        if (string.IsNullOrWhiteSpace(seed) || !Directory.Exists(seed))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(seed));
        while (directory is not null)
        {
            var hasSln = File.Exists(Path.Combine(directory.FullName, "FreeformHelper.sln"));
            var hasGit = Directory.Exists(Path.Combine(directory.FullName, ".git"));
            if (hasSln || hasGit)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
