// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace FreeformHelper.UI.Services;

/// <summary>
/// Names and removes the single backup file that holds unsaved project changes before another project loads.
/// The backup sits next to the app general settings file, so tests that redirect that file also redirect the backup.
/// </summary>
internal static class UnsavedProjectBackup
{
    public const string FileName = "unsaved-project-backup.json";

    public static string PathFor(string appGeneralSettingsPath)
    {
        var directory = Path.GetDirectoryName(appGeneralSettingsPath);
        return Path.Combine(string.IsNullOrEmpty(directory) ? "." : directory, FileName);
    }

    public static void TryDelete(string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return;
        }

        try
        {
            File.Delete(backupPath);
        }
        catch (IOException)
        {
            // A leftover backup is harmless: the next backup replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
