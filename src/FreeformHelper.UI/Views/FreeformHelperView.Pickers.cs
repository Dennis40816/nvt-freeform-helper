using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>
/// This partial class of <see cref="FreeformHelperView"/> handles the integration
/// of file picker dialogs and associated interactions for the ViewModel.
/// It bridges between the platform-specific file dialogs and the ViewModel's requests.
/// </summary>
public sealed partial class FreeformHelperView
{
    private static readonly string[] DxfFilePatterns = ["*.dxf"];
    private static readonly string[] JsonFilePatterns = ["*.json"];
    private static readonly string[] CFilePatterns = ["*.c"];
    private static readonly string[] CsvFilePatterns = ["*.csv"];
    private static readonly string[] AllFilesPatterns = ["*"];

    /// <summary>
    /// Attaches the <see cref="FreeformHelperViewModel"/> to this view and
    /// configures necessary UI-related delegates for file pickers and initial setup.
    /// </summary>
    /// <param name="viewModel">The <see cref="FreeformHelperViewModel"/> instance.</param>
    private void AttachViewModel(FreeformHelperViewModel viewModel)
    {
        var canvas = _canvas ?? FindCanvas(); // Ensure canvas is found.
        viewModel.CanvasHost = new CanvasHost(this, canvas); // Provide canvas host implementation to ViewModel.
        viewModel.InteractionState.SelectionChanged += OnInteractionSelectionChanged; // Subscribe to selection changes.
        viewModel.InteractionState.PadInfoChanged += OnPadInfoChanged; // Subscribe to pad info changes.

        var storage = GetStorageProvider(); // Get platform-specific storage provider.
        StartupPerfTracker.Mark("workspace.view-attach");
        ScheduleInitialGridBuild(viewModel); // Defer initial grid request to keep first paint responsive.
        ScheduleInitialFit(viewModel); // Ensure initial canvas fit after window is ready.

        ConfigureFilePickers(viewModel, storage); // Configure file picker delegates.
        viewModel.EnsureNoPendingEdits = EnsureNoPendingEdits;
        viewModel.OpenNotchDetailAsync = ShowNotchDetailWindowAsync;
        viewModel.OpenNotchExportSelectionAsync = ShowNotchExportSelectionWindowAsync;
        viewModel.GetCurrentSimulationSafetyAudit = ResolveCurrentSimulationSafetyAudit;
        viewModel.OpenDxfEditChangeListAsync = ShowDxfEditChangeListWindowAsync;
        viewModel.OpenIndexMappingReportAsync = ShowIndexMappingReportWindowAsync;
        viewModel.OpenDxfOverlapReportAsync = ShowDxfOverlapReportWindowAsync;
        viewModel.OpenDxfLayerImageExportAsync = ShowDxfLayerImageExportWindowAsync;
        viewModel.ShowWarningAsync = ShowWarningDialogAsync;
        AttachCadLoadSpinnerWindow(viewModel);
    }

    private static void ScheduleInitialGridBuild(FreeformHelperViewModel viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        StartupPerfTracker.Mark("workspace.initial-grid-queued", "priority=Immediate");
        StartupPerfTracker.Mark("workspace.initial-grid-dispatch");
        viewModel.EnsureInitialGrid();
        StartupPerfTracker.Mark("workspace.initial-grid-requested");
    }

    /// <summary>
    /// Detaches the <see cref="FreeformHelperViewModel"/> from this view,
    /// unsubscribing from events and clearing delegates to prevent memory leaks.
    /// </summary>
    /// <param name="viewModel">The <see cref="FreeformHelperViewModel"/> instance.</param>
    private void DetachViewModel(FreeformHelperViewModel viewModel)
    {
        viewModel.CanvasHost = null;
        viewModel.InteractionState.SelectionChanged -= OnInteractionSelectionChanged;
        viewModel.InteractionState.PadInfoChanged -= OnPadInfoChanged;
        // Clear all delegate references to prevent memory leaks.
        viewModel.PickOpenDxfPathAsync = null;
        viewModel.PickOpenDiffCsvPathsAsync = null;
        viewModel.PickOpenRegularVisibilityMaskPathAsync = null;
        viewModel.PickLoadProjectPathAsync = null;
        viewModel.PickSaveProjectPathAsync = null;
        viewModel.PickSaveNotchPathAsync = null;
        viewModel.PickSaveExportPathAsync = null;
        viewModel.ConfirmEmbedDxfAsync = null;
        viewModel.EnsureNoPendingEdits = null;
        viewModel.OpenNotchDetailAsync = null;
        viewModel.OpenNotchExportSelectionAsync = null;
        viewModel.GetCurrentSimulationSafetyAudit = null;
        viewModel.OpenDxfEditChangeListAsync = null;
        viewModel.OpenIndexMappingReportAsync = null;
        viewModel.OpenDxfOverlapReportAsync = null;
        viewModel.OpenDxfLayerImageExportAsync = null;
        viewModel.ShowWarningAsync = null;
        DetachCadLoadSpinnerWindow(viewModel);
    }

    /// <summary>
    /// Retrieves the platform-specific <see cref="IStorageProvider"/>.
    /// </summary>
    /// <returns>The <see cref="IStorageProvider"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the storage provider is not available.</exception>
    private IStorageProvider GetStorageProvider()
        => TopLevel.GetTopLevel(this)?.StorageProvider // Get storage provider from the top-level window.
            ?? throw new InvalidOperationException("StorageProvider is not available.");

    /// <summary>
    /// Configures the ViewModel's file picker delegates using the provided <see cref="IStorageProvider"/>.
    /// </summary>
    /// <param name="viewModel">The <see cref="FreeformHelperViewModel"/> instance.</param>
    /// <param name="storage">The <see cref="IStorageProvider"/> for file operations.</param>
    private void ConfigureFilePickers(FreeformHelperViewModel viewModel, IStorageProvider storage)
    {
        // Delegate for opening a DXF file.
        viewModel.PickOpenDxfPathAsync = async () =>
        {
            var res = await storage.OpenFilePickerAsync(BuildOpenDxfOptions());
            return res is { Count: > 0 } ? res[0].Path.LocalPath : null; // Return local path of selected file.
        };

        viewModel.PickOpenDiffCsvPathsAsync = async () =>
        {
            var res = await storage.OpenFilePickerAsync(BuildOpenDiffCsvOptions());
            return res is { Count: > 0 }
                ? res.Select(static file => file.Path.LocalPath).ToArray()
                : Array.Empty<string>();
        };

        viewModel.PickOpenRegularVisibilityMaskPathAsync = async () =>
        {
            var res = await storage.OpenFilePickerAsync(BuildOpenRegularVisibilityMaskOptions());
            return res is { Count: > 0 } ? res[0].Path.LocalPath : null;
        };

        // Delegate for loading a project file.
        viewModel.PickLoadProjectPathAsync = async () =>
        {
            var res = await storage.OpenFilePickerAsync(BuildOpenProjectOptions());
            return res is { Count: > 0 } ? res[0].Path.LocalPath : null;
        };

        // Delegate for saving a project file.
        viewModel.PickSaveProjectPathAsync = async () =>
        {
            var res = await storage.SaveFilePickerAsync(BuildSaveProjectOptions());
            return res?.Path.LocalPath;
        };

        // Delegate for saving notch table files.
        viewModel.PickSaveNotchPathAsync = async (baseName, ext) =>
        {
            var res = await storage.SaveFilePickerAsync(BuildSaveNotchOptions(baseName, ext));
            return res?.Path.LocalPath;
        };

        // Delegate for saving generic export files (DXF, image, etc.).
        viewModel.PickSaveExportPathAsync = async (baseName, ext) =>
        {
            var res = await storage.SaveFilePickerAsync(BuildSaveExportOptions(baseName, ext));
            return res?.Path.LocalPath;
        };

        // Delegate for confirming whether to embed DXF data.
        viewModel.ConfirmEmbedDxfAsync = async () =>
        {
            var owner = TopLevel.GetTopLevel(this) as Window; // Get parent window for dialog ownership.
            if (owner is null)
            {
                return false; // Cannot show dialog without a parent window.
            }

            // Create and show a custom confirmation dialog.
            var dialog = new ConfirmDialog(
                "Embed DXF",
                "Embed the current DXF into the project file?\nThis increases file size but keeps the project self-contained.",
                "Embed",
                "Skip");

            return await dialog.ShowDialog<bool>(owner); // Return the boolean result from the dialog.
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerOpenOptions"/> for opening a DXF file.
    /// </summary>
    private static FilePickerOpenOptions BuildOpenDxfOptions()
    {
        return new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open DXF",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("DXF") { Patterns = DxfFilePatterns },
                new FilePickerFileType("All files") { Patterns = AllFilesPatterns },
            }
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerOpenOptions"/> for opening a project file.
    /// </summary>
    private static FilePickerOpenOptions BuildOpenProjectOptions()
    {
        return new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open Project",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("FreeformHelper Project") { Patterns = JsonFilePatterns }, // Custom project file extension.
                new FilePickerFileType("All files") { Patterns = AllFilesPatterns },
            }
        };
    }

    private static FilePickerOpenOptions BuildOpenDiffCsvOptions()
    {
        return new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Import Diff CSV",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("CSV") { Patterns = CsvFilePatterns },
                new FilePickerFileType("All files") { Patterns = AllFilesPatterns },
            }
        };
    }

    private static FilePickerOpenOptions BuildOpenRegularVisibilityMaskOptions()
    {
        return new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Import Regular Visibility Mask (SeeRegular.csv)",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("CSV") { Patterns = CsvFilePatterns },
                new FilePickerFileType("All files") { Patterns = AllFilesPatterns },
            }
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerSaveOptions"/> for saving a project file.
    /// </summary>
    private static FilePickerSaveOptions BuildSaveProjectOptions()
    {
        return new FilePickerSaveOptions
        {
            Title = "Save Project",
            DefaultExtension = "json",
            SuggestedFileName = "freeform_helper_project.json",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("FreeformHelper Project") { Patterns = JsonFilePatterns },
                new FilePickerFileType("All files") { Patterns = AllFilesPatterns },
            }
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerSaveOptions"/> for notch table exports.
    /// </summary>
    /// <param name="baseName">The suggested base name for the file.</param>
    /// <param name="ext">The suggested file extension.</param>
    private static FilePickerSaveOptions BuildSaveNotchOptions(string baseName, string ext)
    {
        var normalizedExt = NormalizeNotchSaveExtension(ext);
        var primary = BuildNotchFileTypeChoice(normalizedExt);
        var secondary = BuildNotchFileTypeChoice(normalizedExt switch
        {
            "csv" => "c",
            "c" => "csv",
            _ => "csv",
        });

        return new FilePickerSaveOptions
        {
            Title = "Save Notch Table",
            DefaultExtension = normalizedExt,
            SuggestedFileName = $"{baseName}.{normalizedExt}",
            FileTypeChoices = new List<FilePickerFileType>
            {
                primary,
                secondary,
                new FilePickerFileType("All files") { Patterns = AllFilesPatterns },
            }
        };
    }

    private static FilePickerFileType BuildNotchFileTypeChoice(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            "c" => new FilePickerFileType("C source (*.c)") { Patterns = CFilePatterns },
            _ => new FilePickerFileType("CSV (*.csv)") { Patterns = CsvFilePatterns },
        };
    }

    private static string NormalizeNotchSaveExtension(string ext)
    {
        var normalized = NormalizeExtension(ext, "csv");
        return normalized switch
        {
            "c" => "c",
            _ => "csv",
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerSaveOptions"/> for generic exports such as DXF and images.
    /// </summary>
    /// <param name="baseName">The suggested base name for the file.</param>
    /// <param name="ext">The suggested file extension.</param>
    private static FilePickerSaveOptions BuildSaveExportOptions(string baseName, string ext)
    {
        var normalizedExt = NormalizeExtension(ext, "dat");
        var pattern = $"*.{normalizedExt}";
        var title = ResolveExportDialogTitle(normalizedExt);
        var typeDisplay = ResolveExportFileTypeDisplay(normalizedExt);

        return new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = normalizedExt,
            SuggestedFileName = $"{baseName}.{normalizedExt}",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType(typeDisplay) { Patterns = new[] { pattern } },
                new FilePickerFileType("All files") { Patterns = AllFilesPatterns },
            }
        };
    }

    private static string ResolveExportDialogTitle(string ext)
    {
        return ext switch
        {
            "png" or "bmp" or "jpg" or "jpeg" => "Save Image",
            "dxf" => "Save DXF",
            _ => "Save Export File",
        };
    }

    private static string ResolveExportFileTypeDisplay(string ext)
    {
        return ext switch
        {
            "png" => "PNG image",
            "bmp" => "BMP image",
            "jpg" or "jpeg" => "JPG image",
            "dxf" => "DXF file",
            _ => $"{ext.ToUpperInvariant()} file",
        };
    }

    private static string NormalizeExtension(string ext, string fallback)
    {
        var trimmed = (ext ?? string.Empty).Trim();
        if (trimmed.StartsWith('.'))
        {
            trimmed = trimmed[1..];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed.ToLowerInvariant();
    }

    private async Task ShowWarningDialogAsync(string title, string message)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var dialog = new WarningDialog(title, message);
        if (owner is null)
        {
            dialog.Show();
            return;
        }

        await dialog.ShowDialog(owner);
    }
}
