using Avalonia.Platform.Storage;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Builds the file picker options (title, default extension, suggested name and file type filters)
/// for every open and save dialog of the main view. The view only passes the options to the platform picker.
/// </summary>
internal static class FilePickerOptionFactory
{
    private static readonly string[] _dxfFilePatterns = ["*.dxf"];
    private static readonly string[] _jsonFilePatterns = ["*.json"];
    private static readonly string[] _cFilePatterns = ["*.c"];
    private static readonly string[] _csvFilePatterns = ["*.csv"];
    private static readonly string[] _allFilesPatterns = ["*"];

    /// <summary>
    /// Builds <see cref="FilePickerOpenOptions"/> for opening a DXF file.
    /// </summary>
    public static FilePickerOpenOptions OpenDxf()
    {
        return new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open DXF",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("DXF") { Patterns = _dxfFilePatterns },
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerOpenOptions"/> for opening a project file.
    /// </summary>
    public static FilePickerOpenOptions OpenProject()
    {
        return new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open Project",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("FreeformHelper Project") { Patterns = _jsonFilePatterns }, // Custom project file extension.
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }

    public static FilePickerOpenOptions OpenRegularVisibilityMask()
    {
        return new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Import Regular Visibility Mask (SeeRegular.csv)",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("CSV") { Patterns = _csvFilePatterns },
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerSaveOptions"/> for saving a project file.
    /// </summary>
    public static FilePickerSaveOptions SaveProject()
    {
        return new FilePickerSaveOptions
        {
            Title = "Save Project",
            DefaultExtension = "json",
            SuggestedFileName = "freeform_helper_project.json",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("FreeformHelper Project") { Patterns = _jsonFilePatterns },
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }

    /// <summary>
    /// Builds <see cref="FilePickerSaveOptions"/> for notch table exports.
    /// </summary>
    /// <param name="baseName">The suggested base name for the file.</param>
    /// <param name="ext">The suggested file extension.</param>
    public static FilePickerSaveOptions SaveNotch(string baseName, string ext)
    {
        var normalizedExt = NormalizeNotchSaveExtension(ext);
        var primary = NotchFileTypeChoice(normalizedExt);
        var secondary = NotchFileTypeChoice(normalizedExt switch
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
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }

    private static FilePickerFileType NotchFileTypeChoice(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            "c" => new FilePickerFileType("C source (*.c)") { Patterns = _cFilePatterns },
            _ => new FilePickerFileType("CSV (*.csv)") { Patterns = _csvFilePatterns },
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
    public static FilePickerSaveOptions SaveExport(string baseName, string ext)
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
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
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
}
