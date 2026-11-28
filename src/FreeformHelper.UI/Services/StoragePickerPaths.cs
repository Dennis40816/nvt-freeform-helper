// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Platform.Storage;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Extracts local paths from open and save file picker results.
/// </summary>
internal static class StoragePickerPaths
{
    public static string? FirstLocalPath(IReadOnlyList<IStorageFile> files)
        => files is { Count: > 0 } ? files[0].Path.LocalPath : null;

    public static string? LocalPath(IStorageFile? file)
        => file?.Path.LocalPath;
}
