using Avalonia;

namespace FreeformHelper.UI.Controls;

public class ReviewWorkspaceShell : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, string>(nameof(Subtitle), string.Empty);

    public static readonly StyledProperty<object?> HeaderRightProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, object?>(nameof(HeaderRight));

    public static readonly StyledProperty<object?> SummaryContentProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, object?>(nameof(SummaryContent));

    public static readonly StyledProperty<object?> ToolbarContentProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, object?>(nameof(ToolbarContent));

    public static readonly StyledProperty<object?> LeftContentProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, object?>(nameof(LeftContent));

    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, object?>(nameof(RightContent));

    public static readonly StyledProperty<object?> FooterContentProperty =
        AvaloniaProperty.Register<ReviewWorkspaceShell, object?>(nameof(FooterContent));

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

    public object? HeaderRight
    {
        get => GetValue(HeaderRightProperty);
        set => SetValue(HeaderRightProperty, value);
    }

    public object? SummaryContent
    {
        get => GetValue(SummaryContentProperty);
        set => SetValue(SummaryContentProperty, value);
    }

    public object? ToolbarContent
    {
        get => GetValue(ToolbarContentProperty);
        set => SetValue(ToolbarContentProperty, value);
    }

    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public object? FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }
}
