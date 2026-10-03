using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FreeformHelper.UI.Views.PadInfoSections;

public sealed partial class CadPadInfoContentView : UserControl
{
    public CadPadInfoContentView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
