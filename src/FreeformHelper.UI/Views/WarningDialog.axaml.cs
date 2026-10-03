using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FreeformHelper.UI.Views;

public sealed partial class WarningDialog : Window
{
    private SelectableTextBlock? _titleText;
    private SelectableTextBlock? _messageText;

    public WarningDialog()
    {
        InitializeComponent();
    }

    public WarningDialog(string title, string message) : this()
    {
        if (_titleText is not null)
        {
            _titleText.Text = title;
        }

        if (_messageText is not null)
        {
            _messageText.Text = message;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _titleText = this.FindControl<SelectableTextBlock>("TitleText");
        _messageText = this.FindControl<SelectableTextBlock>("MessageText");
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
