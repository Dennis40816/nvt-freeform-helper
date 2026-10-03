using System.Globalization;
using Avalonia.Data.Converters;

namespace FreeformHelper.UI.Converters;

public sealed class BooleanGlyphConverter : IValueConverter
{
    public string FalseGlyph { get; set; } = string.Empty;
    public string TrueGlyph { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolean && boolean ? TrueGlyph : FalseGlyph;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Avalonia.Data.BindingOperations.DoNothing;
}
