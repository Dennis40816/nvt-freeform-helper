using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Interaction logic for the ConfirmDialog.axaml window.
/// This dialog provides a customizable confirmation prompt to the user.
/// </summary>
public sealed partial class ConfirmDialog : Window
{
    // References to UI controls, initialized after InitializeComponent.
    private SelectableTextBlock? _titleText;
    private SelectableTextBlock? _messageText;
    private Button? _confirmButton;
    private Button? _cancelButton;
    private SelectableTextBlock? _confirmButtonText;
    private SelectableTextBlock? _cancelButtonText;
    private FontIcon? _cancelIcon;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmDialog"/> class.
    /// Default constructor required for Avalonia's XAML loader.
    /// </summary>
    public ConfirmDialog()
    {
        InitializeComponent(); // Loads the XAML definition and initializes controls.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmDialog"/> class with custom content.
    /// </summary>
    /// <param name="title">The title text for the dialog.</param>
    /// <param name="message">The main message text displayed in the dialog.</param>
    /// <param name="confirmText">The text for the affirmative button.</param>
    /// <param name="cancelText">The text for the negative/cancel button.</param>
    /// <param name="emphasizeCancel">If true, the cancel button will have a distinct, often red, styling.</param>
    public ConfirmDialog(string title, string message, string confirmText, string cancelText, bool emphasizeCancel = false) : this()
    {
        // Set the text content of the dialog controls.
        if (_titleText is not null) _titleText.Text = title;
        if (_messageText is not null) _messageText.Text = message;
        if (_confirmButtonText is not null)
        {
            _confirmButtonText.Text = confirmText;
        }
        else if (_confirmButton is not null)
        {
            _confirmButton.Content = confirmText;
        }
        if (_cancelButton is not null)
        {
            if (_cancelButtonText is not null)
            {
                _cancelButtonText.Text = cancelText;
            }
            else
            {
                _cancelButton.Content = cancelText;
            }
            if (emphasizeCancel)
            {
                _cancelButton.Classes.Add("danger");
                if (_cancelIcon is not null)
                {
                    _cancelIcon.IsVisible = true;
                }
            }
        }
    }

    /// <summary>
    /// Loads the XAML UI definition for this control and finds references to its internal controls.
    /// </summary>
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        // Find controls by their x:Name attribute defined in XAML.
        _titleText = this.FindControl<SelectableTextBlock>("TitleText");
        _messageText = this.FindControl<SelectableTextBlock>("MessageText");
        _confirmButton = this.FindControl<Button>("ConfirmButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
        _confirmButtonText = this.FindControl<SelectableTextBlock>("ConfirmButtonText");
        _cancelButtonText = this.FindControl<SelectableTextBlock>("CancelButtonText");
        _cancelIcon = this.FindControl<FontIcon>("CancelIcon");
    }

    /// <summary>
    /// Event handler for the Confirm button click. Closes the dialog with a result of true.
    /// </summary>
    private void ConfirmButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true); // Return true (confirmed) when the confirm button is clicked.
    }

    /// <summary>
    /// Event handler for the Cancel button click. Closes the dialog with a result of false.
    /// </summary>
    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false); // Return false (cancelled) when the cancel button is clicked.
    }
}
