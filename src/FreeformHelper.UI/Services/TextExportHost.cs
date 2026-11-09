using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace FreeformHelper.UI.Services;

/// <summary>The result of a text export request.</summary>
internal enum TextExportOutcome
{
    /// <summary>The window has no storage provider.</summary>
    Unavailable,

    /// <summary>The user closed the save dialog without choosing a file.</summary>
    Cancelled,

    /// <summary>The file was written.</summary>
    Saved,
}

/// <summary>
/// Copies text to the clipboard and saves text files for the workspace views. The views decide which message to show
/// for each result. This class holds the platform calls, so the coordinate planner and the simulation workspace
/// do not each keep their own copy.
/// </summary>
internal static class TextExportHost
{
    public static async Task<bool> TrySetClipboardTextAsync(TopLevel? topLevel, string text)
    {
        var clipboard = topLevel?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        await clipboard.SetTextAsync(text);
        return true;
    }

    public static async Task<TextExportOutcome> SaveTextFileAsync(
        TopLevel? topLevel,
        string title,
        string defaultExtension,
        string suggestedFileName,
        string fileTypeName,
        IReadOnlyList<string> patterns,
        string content)
    {
        var storage = topLevel?.StorageProvider;
        if (storage is null)
        {
            return TextExportOutcome.Unavailable;
        }

        var file = await storage.SaveFilePickerAsync(
            CoordinatePlannerFilePickerOptions.SaveTextExport(title, defaultExtension, suggestedFileName, fileTypeName, patterns));
        if (file is null)
        {
            return TextExportOutcome.Cancelled;
        }

        await TextExportWriter.WriteAsync(file.Path.LocalPath, content, includeBom: true, CancellationToken.None);
        return TextExportOutcome.Saved;
    }
}
