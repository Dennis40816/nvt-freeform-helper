using Avalonia;
using Avalonia.Controls;

namespace FreeformHelper.UI.Controls;

public class PadInfoSectionFrame : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PadInfoSectionFrame, string>(nameof(Title), string.Empty);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
