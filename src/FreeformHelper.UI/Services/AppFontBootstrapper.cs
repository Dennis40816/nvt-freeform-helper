using Avalonia.Media;

namespace FreeformHelper.UI.Services;

internal static class AppFontBootstrapper
{
    private const string DefaultFontFamily = "Inter";
    public static readonly Uri InterSystemFontSourceUri = new("avares://Avalonia.Fonts.Inter/Assets");

    public static FontManagerOptions CreateFontManagerOptions()
    {
        var interFamily = new FontFamily(DefaultFontFamily);
        return new FontManagerOptions
        {
            DefaultFamilyName = DefaultFontFamily,
            FontFamilyMappings = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase)
            {
                ["Segoe UI Variable Text"] = interFamily,
                ["Segoe UI"] = interFamily,
            },
        };
    }
}
