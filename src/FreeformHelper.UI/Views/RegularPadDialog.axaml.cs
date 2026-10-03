using System.Globalization; // Required for CultureInfo.InvariantCulture
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Interaction logic for the RegularPadDialog.axaml window.
/// This dialog is used to inspect and optionally edit properties of a regular grid pad.
/// </summary>
public partial class RegularPadDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegularPadDialog"/> class.
    /// </summary>
    public RegularPadDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegularPadDialog"/> class with specific regular pad data.
    /// This constructor sets up the ViewModel and wires up the dialog close request.
    /// </summary>
    /// <param name="displayRow">The display row index of the pad.</param>
    /// <param name="col">The column index of the pad.</param>
    /// <param name="index">The unique index of the pad.</param>
    /// <param name="widthMm">The current width of the pad in millimeters.</param>
    /// <param name="heightMm">The current height of the pad in millimeters.</param>
    public RegularPadDialog(int displayRow, int col, int index, double widthMm, double heightMm) : this()
    {
        // Create the ViewModel for the dialog.
        var vm = new RegularPadDialogViewModel(displayRow, col, index, widthMm, heightMm);
        // Wire up the ViewModel's request to close the dialog.
        vm.RequestClose += () => Close(vm.DialogResult);
        // Set the ViewModel as the DataContext for the window.
        DataContext = vm;
    }
}

/// <summary>
/// Represents the result of the <see cref="RegularPadDialog"/>.
/// </summary>
public sealed class RegularPadDialogResult
{
    /// <summary>
    /// Gets or initializes a value indicating whether the changes were saved (true) or cancelled (false).
    /// </summary>
    public bool Saved { get; init; }
    /// <summary>
    /// Gets or initializes the width in millimeters entered by the user.
    /// </summary>
    public double WidthMm { get; init; }
    /// <summary>
    /// Gets or initializes the height in millimeters entered by the user.
    /// </summary>
    public double HeightMm { get; init; }
}

/// <summary>
/// ViewModel for the <see cref="RegularPadDialog"/>.
/// Manages the data and logic presented in the regular pad inspection dialog.
/// </summary>
public sealed class RegularPadDialogViewModel
{
    /// <summary>
    /// Event that requests the dialog to close, typically after an action like Save or Cancel.
    /// </summary>
    public event Action? RequestClose;

    // --- Display Properties ---
    public string Title { get; }
    public string Description { get; }
    public int DisplayRow { get; }
    public int Col { get; }
    public int Index { get; }
    /// <summary>
    /// Gets or sets the text input for the pad's width.
    /// </summary>
    public string WidthText { get; set; }
    /// <summary>
    /// Gets or sets the text input for the pad's height.
    /// </summary>
    public string HeightText { get; set; }
    public string AreaText { get; }
    /// <summary>
    /// Gets or sets the result to be returned when the dialog closes.
    /// </summary>
    public RegularPadDialogResult DialogResult { get; private set; } = new() { Saved = false };

    // --- Commands ---
    /// <summary>
    /// Gets the command to save the changes and close the dialog.
    /// </summary>
    public IRelayCommand SaveCommand { get; }
    /// <summary>
    /// Gets the command to cancel the dialog without saving changes.
    /// </summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegularPadDialogViewModel"/> class.
    /// </summary>
    /// <param name="displayRow">The display row index of the pad.</param>
    /// <param name="col">The column index of the pad.</param>
    /// <param name="index">The unique index of the pad.</param>
    /// <param name="widthMm">The current width of the pad in millimeters.</param>
    /// <param name="heightMm">The current height of the pad in millimeters.</param>
    public RegularPadDialogViewModel(int displayRow, int col, int index, double widthMm, double heightMm)
    {
        DisplayRow = displayRow;
        Col = col;
        Index = index;
        WidthText = widthMm.ToString("0.###", CultureInfo.InvariantCulture); // Format width to 3 decimal places.
        HeightText = heightMm.ToString("0.###", CultureInfo.InvariantCulture); // Format height to 3 decimal places.
        AreaText = $"Area: {(widthMm * heightMm).ToString("0.###", CultureInfo.InvariantCulture)} mm²"; // Calculate and format area.
        Title = $"Regular Pad ({displayRow},{col})";
        Description = "Inspect / customize this pad. AA size is stored per pad.";

        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(() =>
        {
            // Set dialog result to cancelled and preserve original width/height.
            DialogResult = new RegularPadDialogResult { Saved = false, WidthMm = widthMm, HeightMm = heightMm };
            RequestClose?.Invoke(); // Request dialog closure.
        });
    }

    /// <summary>
    /// Handles the Save action. Parses the WidthText and HeightText and sets the dialog result.
    /// </summary>
    private void OnSave()
    {
        // Attempt to parse width. If invalid, default to 0.0.
        if (!double.TryParse(WidthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
        {
            w = 0.0;
        }

        // Attempt to parse height. If invalid, default to 0.0.
        if (!double.TryParse(HeightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
        {
            h = 0.0;
        }

        // Set dialog result to saved with the parsed width and height.
        DialogResult = new RegularPadDialogResult { Saved = true, WidthMm = w, HeightMm = h };
        RequestClose?.Invoke(); // Request dialog closure.
    }
}
