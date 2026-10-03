using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FreeformHelper.UI.Views.PadInfoSections;

public sealed partial class RegularPadInfoContentView : UserControl
{
    public RegularPadInfoContentView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
