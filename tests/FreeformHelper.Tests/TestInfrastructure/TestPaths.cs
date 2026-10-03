namespace FreeformHelper.Tests.TestInfrastructure;

internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string FromRepo(params string[] segments)
    {
        return Path.Combine([RepoRoot, .. segments]);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "FreeformHelper.sln");
            if (File.Exists(solution))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Cannot locate repo root (FreeformHelper.sln).");
    }
}
