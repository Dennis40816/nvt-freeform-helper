// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PadHeader, string>(nameof(Title), string.Empty);
    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<PadHeader, string>(nameof(Subtitle), string.Empty);
    public static readonly StyledProperty<object?> KindMarkProperty =
        AvaloniaProperty.Register<PadHeader, object?>(nameof(KindMark));
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PadHeader, object?>(nameof(Actions));
    public static readonly StyledProperty<bool> ShowActionLabelsProperty =
        AvaloniaProperty.Register<PadHeader, bool>(nameof(ShowActionLabels), true);

    public PadHeader() => AvaloniaXamlLoader.Load(this);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? KindMark
    {
        get => GetValue(KindMarkProperty);
        set => SetValue(KindMarkProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public bool ShowActionLabels
    {
        get => GetValue(ShowActionLabelsProperty);
        private set => SetValue(ShowActionLabelsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty &&
            this.TryFindResource("PadHeaderActionLabelsMinWidth", out var resource) && resource is double minimumWidth)
        {
            ShowActionLabels = Bounds.Width >= minimumWidth;
        }
    }
}
