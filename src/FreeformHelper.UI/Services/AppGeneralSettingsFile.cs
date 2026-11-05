// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using System.Text;
using System.Text.Json;
using FreeformHelper.Application.Services;
using NLog;

namespace FreeformHelper.UI.Services;

internal enum CorruptBackupResult
{
    /// <summary>The failure is not a damaged settings file, so nothing was moved.</summary>
    NotCorrupt,

    /// <summary>The damaged file was moved to a backup that nothing else owns.</summary>
    Preserved,

    /// <summary>The damaged file could not be moved. It is still at its original path.</summary>
    Failed,
}

internal static class AppGeneralSettingsFile
{
    private const int MaxBackupAttempts = 20;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static string? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var reader = new StreamReader(path);
        return reader.ReadToEnd();
    }

    public static CorruptBackupResult TryBackUpCorrupt(string path, Exception parseException, bool isFileRead)
    {
        if (!isFileRead || parseException is not (JsonException or ArgumentException or FormatException))
        {
            return CorruptBackupResult.NotCorrupt;
        }

        var backupPath = MoveToUnusedBackup(path);
        if (backupPath is null)
        {
            _logger.Error(parseException, "Invalid app general settings in '{0}' could not be backed up. The file stays in place.", path);
            return CorruptBackupResult.Failed;
        }

        _logger.Error(parseException, "Invalid app general settings from '{0}' moved to backup '{1}'.", path, backupPath);
        return CorruptBackupResult.Preserved;
    }

    // Never overwrites an older backup. The first backup is "<path>.corrupt"; later ones get a unique suffix.
    private static string? MoveToUnusedBackup(string path)
    {
        var file = new FileInfo(path);
        for (var attempt = 0; attempt < MaxBackupAttempts; attempt++)
        {
            var backupPath = attempt == 0
                ? path + ".corrupt"
                : FormattableString.Invariant($"{path}.corrupt.{Guid.NewGuid():N}");
            try
            {
                file.MoveTo(backupPath, overwrite: false);
                return backupPath;
            }
            catch (IOException) when (File.Exists(backupPath) || Directory.Exists(backupPath))
            {
                // Another backup already uses this name. Try the next name.
            }
            catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
            {
                _logger.Error(ex, "Failed to move invalid app general settings to backup '{0}'.", backupPath);
                return null;
            }
        }

        return null;
    }

    public static void WriteAtomically(string path, string json)
    {
        var targetPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{id}.tmp");
        try
        {
            WriteTemporaryFile(temporaryPath, json);
            if (File.Exists(targetPath))
            {
                File.Replace(temporaryPath, targetPath, destinationBackupFileName: null);
            }
            else
            {
                new FileInfo(temporaryPath).MoveTo(targetPath, overwrite: true);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void WriteTemporaryFile(string path, string json)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(json);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}
