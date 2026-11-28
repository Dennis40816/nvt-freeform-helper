using Avalonia.Platform.Storage;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Builds the file picker options for the coordinate planner exports: the coordinate text files and the preview image.
/// The view only passes the options to the platform picker.
/// </summary>
internal static class CoordinatePlannerFilePickerOptions
{
    private static readonly string[] _pngFilePatterns = ["*.png"];
    private static readonly string[] _bmpFilePatterns = ["*.bmp"];
    private static readonly string[] _allFilesPatterns = ["*"];

    public static FilePickerSaveOptions SaveTextExport(
        string title,
        string defaultExtension,
        string suggestedFileName,
        string fileTypeName,
        IReadOnlyList<string> patterns)
    {
        return new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType(fileTypeName) { Patterns = patterns },
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }

    public static FilePickerSaveOptions SaveCornerCircleImage(int pixelWidth, int pixelHeight)
    {
        return new FilePickerSaveOptions
        {
            Title = "Export Corner Circle Image",
            DefaultExtension = "png",
            SuggestedFileName = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"corner_circles_{pixelWidth}x{pixelHeight}.png"),
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("PNG") { Patterns = _pngFilePatterns },
                new FilePickerFileType("BMP") { Patterns = _bmpFilePatterns },
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }

    public static FilePickerSaveOptions SavePreviewImage()
    {
        return new FilePickerSaveOptions
        {
            Title = "Export Coordinate Preview",
            DefaultExtension = "png",
            SuggestedFileName = "coordinate_planner.png",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("PNG") { Patterns = _pngFilePatterns },
                new FilePickerFileType("BMP") { Patterns = _bmpFilePatterns },
                new FilePickerFileType("All files") { Patterns = _allFilesPatterns },
            }
        };
    }
}
