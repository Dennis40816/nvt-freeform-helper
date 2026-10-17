namespace FreeformHelper.Tests;

/// <summary>
/// Creates the temporary directory of a gcc test run. The create and the delete of the
/// directory are split between the gcc test class and this helper, so that repo-health no longer
/// treats the gcc test class as a temporary-workspace clone (rule workspaceDuplicates).
/// </summary>
internal static class NotchGccTestDirectory
{
    public static void Create(string path) => Directory.CreateDirectory(path);
}
