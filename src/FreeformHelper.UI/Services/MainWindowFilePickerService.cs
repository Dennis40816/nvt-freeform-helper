// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Platform.Storage;

namespace FreeformHelper.UI.Services;

/// <summary>Runs the main view's six file pickers and returns their selected local paths.</summary>
internal sealed class MainWindowFilePickerService(IStorageProvider storage)
{
    public async Task<string?> OpenDxfAsync()
    {
        var res = await storage.OpenFilePickerAsync(FilePickerOptionFactory.OpenDxf());
        return StoragePickerPaths.FirstLocalPath(res); // Return local path of selected file.
    }

    public async Task<string?> OpenRegularVisibilityMaskAsync()
    {
        var res = await storage.OpenFilePickerAsync(FilePickerOptionFactory.OpenRegularVisibilityMask());
        return StoragePickerPaths.FirstLocalPath(res);
    }

    public async Task<string?> OpenProjectAsync()
    {
        var res = await storage.OpenFilePickerAsync(FilePickerOptionFactory.OpenProject());
        return StoragePickerPaths.FirstLocalPath(res);
    }

    public async Task<string?> SaveProjectAsync()
    {
        var res = await storage.SaveFilePickerAsync(FilePickerOptionFactory.SaveProject());
        return StoragePickerPaths.LocalPath(res);
    }

    public async Task<string?> SaveNotchAsync(string baseName, string ext)
    {
        var res = await storage.SaveFilePickerAsync(FilePickerOptionFactory.SaveNotch(baseName, ext));
        return StoragePickerPaths.LocalPath(res);
    }

    public async Task<string?> SaveExportAsync(string baseName, string ext)
    {
        var res = await storage.SaveFilePickerAsync(FilePickerOptionFactory.SaveExport(baseName, ext));
        return StoragePickerPaths.LocalPath(res);
    }
}
