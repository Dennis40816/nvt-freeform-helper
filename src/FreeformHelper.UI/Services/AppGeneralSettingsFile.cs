// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using System.Text;
using System.Text.Json;
using FreeformHelper.Application.Services;
using NLog;

namespace FreeformHelper.UI.Services;

internal static class AppGeneralSettingsFile
{
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

    public static bool TryBackUpCorrupt(string path, Exception parseException, bool isFileRead)
    {
        if (!isFileRead || parseException is not (JsonException or ArgumentException or FormatException))
        {
            return false;
        }

        var backupPath = path + ".corrupt";
        try
        {
            new FileInfo(path).MoveTo(backupPath, overwrite: true);
            _logger.Error(parseException, "Invalid app general settings from '{0}' moved to backup '{1}'.", path, backupPath);
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            _logger.Error(parseException, "Invalid app general settings from '{0}'; backup path is '{1}'.", path, backupPath);
            _logger.Error(ex, "Failed to move invalid app general settings to backup '{0}'.", backupPath);
        }

        return true;
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
