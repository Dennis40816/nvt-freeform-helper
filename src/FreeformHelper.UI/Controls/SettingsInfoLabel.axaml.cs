using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace FreeformHelper.UI.Controls;

public sealed partial class SettingsInfoLabel : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SettingsInfoLabel, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> TipProperty =
        AvaloniaProperty.Register<SettingsInfoLabel, string>(nameof(Tip), string.Empty);

    public SettingsInfoLabel()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => ApplyTipToFieldTile();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Tip
    {
        get => GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TipProperty)
        {
            ApplyTipToFieldTile();
        }
    }

    private void ApplyTipToFieldTile()
    {
        var tip = Tip;
        if (string.IsNullOrWhiteSpace(tip))
        {
            return;
        }

        ToolTip.SetTip(this, tip);

        var parent = this.GetVisualParent();
        while (parent is not null)
        {
            if (parent is Control control && control.Classes.Contains("settingsFieldTile"))
            {
                ToolTip.SetTip(control, tip);
                return;
            }

            parent = parent.GetVisualParent();
        }
    }
}
