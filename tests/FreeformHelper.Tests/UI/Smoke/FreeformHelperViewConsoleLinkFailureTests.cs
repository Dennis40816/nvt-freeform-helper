using System.Runtime.CompilerServices;
using System.Security;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the console link helpers do when the operating system call fails: a failure that the API documents
/// ends in the fallback or the empty result, and any other failure, out-of-memory included, reaches the caller.
/// </summary>
public sealed class FreeformHelperViewConsoleLinkFailureTests
{
    private static OutOfMemoryException CreateOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

    private static Exception ListingFailure(string kind) => kind switch
    {
        "io" => new DirectoryNotFoundException("gone"),
        "access" => new UnauthorizedAccessException("denied"),
        "argument" => new ArgumentException("bad pattern"),
        "security" => new SecurityException("blocked"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [AvaloniaFact]
    public void OpenWithShell_WhenTheFileNameIsEmpty_ReturnsFalse()
    {
        // Process.Start throws InvalidOperationException before it starts anything.
        Assert.False(FreeformHelperView.TryOpenWithShell(string.Empty));
    }

    [AvaloniaFact]
    public void OpenWithShell_WhenThePathDoesNotExist_ReturnsFalse()
    {
        // The shell reports the missing file as Win32Exception and starts no process.
        var missing = "nfh-missing-" + Guid.NewGuid().ToString("N") + ".nfhmissing";

        Assert.False(FreeformHelperView.TryOpenWithShell(missing));
    }

    [AvaloniaTheory]
    [InlineData("io")]
    [InlineData("access")]
    [InlineData("argument")]
    [InlineData("security")]
    public void RepoMatches_WhenListingFailsWithADocumentedException_ReturnsNone(string kind)
    {
        var matches = ConsoleLinkTargetSearch.EnumerateRepoMatches(
            "repo",
            "log.txt",
            (_, _, _) => throw ListingFailure(kind));

        Assert.Empty(matches);
    }

    [AvaloniaFact]
    public void RepoMatches_ListsAtMostThirtyTwoFilesFromTheWholeTree()
    {
        SearchOption? requested = null;

        var matches = ConsoleLinkTargetSearch.EnumerateRepoMatches(
            "repo",
            "log.txt",
            (_, _, option) =>
            {
                requested = option;
                return Enumerable.Range(0, 100).Select(index => "repo/" + index + "/log.txt");
            });

        Assert.Equal(32, matches.Length);
        Assert.Equal(SearchOption.AllDirectories, requested);
    }

    [AvaloniaFact]
    public void RepoMatches_WhenListingRunsOutOfMemory_Propagates()
    {
        Assert.Throws<OutOfMemoryException>(() => ConsoleLinkTargetSearch.EnumerateRepoMatches(
            "repo",
            "log.txt",
            (_, _, _) => throw CreateOutOfMemory()));
    }

    [AvaloniaFact]
    public void RepoMatches_WhenListingFailsWithAnUndocumentedException_Propagates()
    {
        Assert.Throws<InvalidOperationException>(() => ConsoleLinkTargetSearch.EnumerateRepoMatches(
            "repo",
            "log.txt",
            (_, _, _) => throw new InvalidOperationException("unexpected")));
    }

    [AvaloniaFact]
    public void FindLinkAt_WhenTheOffsetIsInsideALink_ReturnsIt()
    {
        var link = new ConsoleLinkSpan(10, 5, "log.txt", false);

        var found = ConsoleLinkTargetSearch.FindLinkAt([link], () => 12);

        Assert.Same(link, found);
    }

    [AvaloniaFact]
    public void FindLinkAt_WhenThePositionIsOutsideTheDocument_ReturnsNull()
    {
        var link = new ConsoleLinkSpan(10, 5, "log.txt", false);

        var found = ConsoleLinkTargetSearch.FindLinkAt([link], () => throw new ArgumentOutOfRangeException(null, "outside the document"));

        Assert.Null(found);
    }

    [AvaloniaFact]
    public void FindLinkAt_WhenTheOffsetRunsOutOfMemory_Propagates()
    {
        Assert.Throws<OutOfMemoryException>(() =>
            ConsoleLinkTargetSearch.FindLinkAt([], () => throw CreateOutOfMemory()));
    }

    [AvaloniaFact]
    public void FindLinkAt_WhenTheOffsetFailsWithAnUndocumentedException_Propagates()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ConsoleLinkTargetSearch.FindLinkAt([], () => throw new InvalidOperationException("unexpected")));
    }
}
