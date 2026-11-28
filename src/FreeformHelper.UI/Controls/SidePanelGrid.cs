using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// A workspace grid whose last column is a side panel with one width for wide viewports and one for narrow ones.
/// <see cref="KeepWide"/> keeps the wide width in a narrow viewport. The width changes only when the choice
/// between the two changes, so a splitter drag is kept until then. <see cref="SidePanelSwitchedCommand"/>
/// runs after a switch, not on the first layout.
/// </summary>
public sealed class SidePanelGrid : Grid
{
    public static readonly StyledProperty<double> NarrowBelowWidthProperty =
        AvaloniaProperty.Register<SidePanelGrid, double>(nameof(NarrowBelowWidth));

    public static readonly StyledProperty<GridLength> WideSidePanelWidthProperty =
        AvaloniaProperty.Register<SidePanelGrid, GridLength>(nameof(WideSidePanelWidth));

    public static readonly StyledProperty<GridLength> NarrowSidePanelWidthProperty =
        AvaloniaProperty.Register<SidePanelGrid, GridLength>(nameof(NarrowSidePanelWidth));

    public static readonly StyledProperty<bool> KeepWideProperty =
        AvaloniaProperty.Register<SidePanelGrid, bool>(nameof(KeepWide));

    public static readonly StyledProperty<ICommand?> SidePanelSwitchedCommandProperty =
        AvaloniaProperty.Register<SidePanelGrid, ICommand?>(nameof(SidePanelSwitchedCommand));

    private bool? _isNarrow;

    protected override Type StyleKeyOverride => typeof(Grid);

    public double NarrowBelowWidth
    {
        get => GetValue(NarrowBelowWidthProperty);
        set => SetValue(NarrowBelowWidthProperty, value);
    }

    public GridLength WideSidePanelWidth
    {
        get => GetValue(WideSidePanelWidthProperty);
        set => SetValue(WideSidePanelWidthProperty, value);
    }

    public GridLength NarrowSidePanelWidth
    {
        get => GetValue(NarrowSidePanelWidthProperty);
        set => SetValue(NarrowSidePanelWidthProperty, value);
    }

    public bool KeepWide
    {
        get => GetValue(KeepWideProperty);
        set => SetValue(KeepWideProperty, value);
    }

    public ICommand? SidePanelSwitchedCommand
    {
        get => GetValue(SidePanelSwitchedCommandProperty);
        set => SetValue(SidePanelSwitchedCommandProperty, value);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplySidePanelWidth(e.NewSize.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeepWideProperty && Bounds.Width > 0)
        {
            ApplySidePanelWidth(Bounds.Width);
        }
    }

    private void ApplySidePanelWidth(double width)
    {
        var isNarrow = !KeepWide && width < NarrowBelowWidth;
        if (_isNarrow == isNarrow || ColumnDefinitions.Count == 0)
        {
            return;
        }

        var isSwitch = _isNarrow is not null;
        _isNarrow = isNarrow;
        ColumnDefinitions[^1].Width = isNarrow ? NarrowSidePanelWidth : WideSidePanelWidth;
        if (isSwitch && SidePanelSwitchedCommand?.CanExecute(null) == true)
        {
            SidePanelSwitchedCommand.Execute(null);
        }
    }
}
