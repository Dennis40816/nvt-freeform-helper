// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class SimulationWorkspaceView
{
    private async Task<IReadOnlyList<string>> PickOpenDiffCsvPathsAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return Array.Empty<string>();
        }

        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Import Diff CSV",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("CSV") { Patterns = _csvFilePatterns },
                new("All files") { Patterns = _allFilesPatterns },
            },
        });

        return result is { Count: > 0 }
            ? result.Select(static file => file.Path.LocalPath).ToArray()
            : Array.Empty<string>();
    }

    private async Task<bool> SetClipboardTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            ShowSimulationToast("Clipboard is unavailable.");
            return false;
        }

        await clipboard.SetTextAsync(text);
        ShowSimulationToast("Copied copper path replay.");
        return true;
    }

    private async Task<bool> SaveTextFileAsync(SimulationWorkspaceViewModel.SimulationTextExportRequest request)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            ShowSimulationToast("File saving is unavailable.");
            return false;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = request.Title,
            DefaultExtension = request.DefaultExtension,
            SuggestedFileName = request.SuggestedFileName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new(request.FileTypeName) { Patterns = request.Patterns },
                new("All files") { Patterns = _allFilesPatterns },
            },
        });
        if (file is null)
        {
            ShowSimulationToast("Copper path replay export cancelled.");
            return false;
        }

        await TextExportWriter.WriteAsync(file.Path.LocalPath, request.Content, includeBom: true, CancellationToken.None);
        ShowSimulationToast($"Exported copper path replay to {request.FileTypeName}.");
        return true;
    }

    private void ShowSimulationToast(string message)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.ShowTopToast(message);
        }
    }
}
