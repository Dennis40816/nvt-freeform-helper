using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FreeformHelper.UI.Views.PadInfoSections;

public sealed partial class PadInfoRelationCardListView : UserControl
{
    public static readonly StyledProperty<IEnumerable?> CardsProperty =
        AvaloniaProperty.Register<PadInfoRelationCardListView, IEnumerable?>(nameof(Cards));

    public PadInfoRelationCardListView()
    {
        InitializeComponent();
    }

    public IEnumerable? Cards
    {
        get => GetValue(CardsProperty);
        set => SetValue(CardsProperty, value);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
