using System.Globalization; // Required for CultureInfo.InvariantCulture
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel; // Required for ObservableObject
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Interaction logic for the CadPadDialog.axaml window.
/// This dialog is used to inspect and optionally edit properties of a CAD pad.
/// </summary>
public partial class CadPadDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadPadDialog"/> class.
    /// </summary>
    public CadPadDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CadPadDialog"/> class with specific CAD pad data.
    /// This constructor sets up the ViewModel and wires up the dialog close request.
    /// </summary>
    /// <param name="pad">The <see cref="CadPad"/> to display information for.</param>
    /// <param name="currentCustomValue">The current custom numeric value associated with the pad.</param>
    public CadPadDialog(CadPad pad, double currentCustomValue) : this()
    {
        // Create the ViewModel for the dialog.
        var vm = new CadPadDialogViewModel(pad, currentCustomValue);
        // Wire up the ViewModel's request to close the dialog.
        vm.RequestClose += () => Close(vm.DialogResult);
        // Set the ViewModel as the DataContext for the window.
        DataContext = vm;
    }
}

/// <summary>
/// Represents the result of the <see cref="CadPadDialog"/>.
/// </summary>
public sealed class CadPadDialogResult
{
    /// <summary>
    /// Gets or initializes a value indicating whether the changes were saved (true) or cancelled (false).
    /// </summary>
    public bool Saved { get; init; }
    /// <summary>
    /// Gets or initializes the custom numeric value entered by the user.
    /// </summary>
    public double CustomValue { get; init; }
}

/// <summary>
/// ViewModel for the <see cref="CadPadDialog"/>.
/// Manages the data and logic presented in the CAD pad inspection dialog.
/// </summary>
public sealed class CadPadDialogViewModel : ObservableObject
{
    /// <summary>
    /// Event that requests the dialog to close, typically after an action like Save or Cancel.
    /// </summary>
    public event Action? RequestClose;

    // --- Display Properties ---
    public string Title { get; }
    public string Description { get; }
    public int Id { get; }
    public string Name { get; }
    public string Layer { get; }
    public string AreaText { get; }
    public string CentroidText { get; }
    /// <summary>
    /// Gets or sets the text input for the custom numeric value.
    /// </summary>
    public string CustomValueText { get; set; }
    /// <summary>
    /// Gets or sets the result to be returned when the dialog closes.
    /// </summary>
    public CadPadDialogResult DialogResult { get; private set; } = new() { Saved = false };

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
    /// Initializes a new instance of the <see cref="CadPadDialogViewModel"/> class.
    /// </summary>
    /// <param name="pad">The <see cref="CadPad"/> to display information for.</param>
    /// <param name="currentCustomValue">The current custom numeric value associated with the pad.</param>
    public CadPadDialogViewModel(CadPad pad, double currentCustomValue)
    {
        Id = pad.Id;
        Name = pad.Name;
        Layer = pad.Layer;
        AreaText = pad.Area.ToString("0.###", CultureInfo.InvariantCulture); // Format area to 3 decimal places.
        CentroidText = $"{pad.Centroid.X:0.###}, {pad.Centroid.Y:0.###}"; // Format centroid coordinates.
        CustomValueText = currentCustomValue.ToString(CultureInfo.InvariantCulture); // Set initial custom value text.
        Title = $"CAD Pad {Name} (#{Id})";
        Description = "Inspect CAD pad properties. Custom value is stored per DXF pad.";

        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(() =>
        {
            // Set dialog result to cancelled and preserve original custom value.
            DialogResult = new CadPadDialogResult { Saved = false, CustomValue = currentCustomValue };
            RequestClose?.Invoke(); // Request dialog closure.
        });
    }

    /// <summary>
    /// Handles the Save action. Parses the CustomValueText and sets the dialog result.
    /// </summary>
    private void OnSave()
    {
        // Attempt to parse the custom value from the text box. If invalid, default to 0.0.
        if (!double.TryParse(CustomValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            val = 0.0;
        }

        // Set dialog result to saved with the parsed custom value.
        DialogResult = new CadPadDialogResult { Saved = true, CustomValue = val };
        RequestClose?.Invoke(); // Request dialog closure.
    }
}
