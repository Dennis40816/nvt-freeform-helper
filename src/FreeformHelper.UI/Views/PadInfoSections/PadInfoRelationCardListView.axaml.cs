using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FreeformHelper.UI.Views.PadInfoSections;

public sealed partial class PadInfoRelationCardListView : UserControl
{
    public static readonly StyledProperty<IEnumerable?> CardsProperty =
        AvaloniaProperty.Register<PadInfoRelationCardListView, IEnumerable?>(nameof(Cards));
    public static readonly StyledProperty<bool> CompactProperty =
        AvaloniaProperty.Register<PadInfoRelationCardListView, bool>(nameof(Compact));

    public PadInfoRelationCardListView()
    {
        InitializeComponent();
    }

    public IEnumerable? Cards
    {
        get => GetValue(CardsProperty);
        set => SetValue(CardsProperty, value);
    }

    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
