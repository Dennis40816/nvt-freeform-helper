// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using Nvt.Core.IO;

namespace FreeformHelper.UI.Services;

internal static class TextExportWriter
{
    public static Task WriteAsync(string path, string text, bool includeBom, CancellationToken ct)
    {
        var encoding = new UTF8Encoding(includeBom);
        byte[] content = encoding.GetBytes(text);
        byte[] bytes = includeBom ? [.. encoding.GetPreamble(), .. content] : content;
        return AtomicOutput.WriteBytesAsync(path, bytes, ct);
    }
}
