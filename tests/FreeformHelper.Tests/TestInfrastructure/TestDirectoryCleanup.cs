namespace FreeformHelper.Tests.TestInfrastructure;

internal static class TestDirectoryCleanup
{
    /// <summary>Deletes a temporary test directory and ignores the failures that a busy or locked folder causes.</summary>
    public static void DeleteBestEffort(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // A process still holds a file in the folder.
        }
        catch (UnauthorizedAccessException)
        {
            // The folder or a file in it is read-only or locked.
        }
    }
}
