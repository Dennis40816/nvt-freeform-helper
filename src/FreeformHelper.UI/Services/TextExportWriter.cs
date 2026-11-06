// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using Nvt.Core.IO;

namespace FreeformHelper.UI.Services;

internal static class TextExportWriter
{
    public static async Task WriteAsync(string path, string text, bool includeBom, CancellationToken ct)
    {
        // File.WriteAllText never created folders; AtomicOutput does. Keep the old contract: a missing folder fails.
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null && !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Could not find a part of the path '{path}'.");
        }

        var encoding = new UTF8Encoding(includeBom);
        byte[] content = encoding.GetBytes(text);
        byte[] bytes = includeBom ? [.. encoding.GetPreamble(), .. content] : content;

        // AtomicOutput names its temporary file in a failure; File.WriteAllText named the target. Status text shows the target.
        try
        {
            await AtomicOutput.WriteBytesAsync(path, bytes, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"{ex.Message} (export target '{path}')", ex);
        }
    }
}
