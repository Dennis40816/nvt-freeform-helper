using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.ViewModels;

internal static class NotchExportFileTypeMetadata
{
    public static bool TryParseStoredValue(
        string? rawValue,
        out FreeformHelperViewModel.NotchExportFileType fileType)
    {
        if (!string.IsNullOrWhiteSpace(rawValue) &&
            Enum.TryParse(rawValue, ignoreCase: true, out fileType))
        {
            return true;
        }

        fileType = default;
        return false;
    }

    public static bool IsCExportType(FreeformHelperViewModel.NotchExportFileType type)
    {
        return type is
            FreeformHelperViewModel.NotchExportFileType.Cv21 or
            FreeformHelperViewModel.NotchExportFileType.Cv22;
    }

    public static string GetExportKindLabel(FreeformHelperViewModel.NotchExportFileTypeOption option)
    {
        return option.Value switch
        {
            FreeformHelperViewModel.NotchExportFileType.Csv => "CSV review",
            FreeformHelperViewModel.NotchExportFileType.Cv21 => "C v2.1",
            FreeformHelperViewModel.NotchExportFileType.Cv22 => "C v2.2",
            _ => string.IsNullOrWhiteSpace(option.Display) ? "Unknown" : option.Display,
        };
    }

    public static bool TryGetPinnedVersion(
        FreeformHelperViewModel.NotchExportFileTypeOption option,
        out NotchAlgorithmVersion version)
    {
        if (option.PinnedVersion.HasValue)
        {
            version = option.PinnedVersion.Value;
            return true;
        }

        version = default;
        return false;
    }
}
