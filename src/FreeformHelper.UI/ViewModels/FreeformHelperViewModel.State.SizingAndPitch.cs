using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // --- Manual Sizing Input Fields ---

    /// <summary>
    /// Gets or sets the pending width value for manual column/pad sizing.
    /// </summary>
    [ObservableProperty] private decimal _pendingColumnWidth;
    /// <summary>
    /// Gets or sets the pending height value for manual row/pad sizing.
    /// </summary>
    [ObservableProperty] private decimal _pendingRowHeight;
    /// <summary>
    /// Gets or sets a value indicating whether <see cref="PendingColumnWidth"/> represents mixed values in a selection.
    /// </summary>
    [ObservableProperty] private bool _isPendingColumnWidthMixed;
    /// <summary>
    /// Gets or sets a value indicating whether <see cref="PendingRowHeight"/> represents mixed values in a selection.
    /// </summary>
    [ObservableProperty] private bool _isPendingRowHeightMixed;
    /// <summary>
    /// Gets or sets the string input for manual row range selection (e.g., "1-3,5").
    /// </summary>
    [ObservableProperty] private string _manualRowsRange = string.Empty;
    /// <summary>
    /// Gets or sets the string input for manual column range selection (e.g., "A-C,E").
    /// </summary>
    [ObservableProperty] private string _manualColsRange = string.Empty;
    /// <summary>
    /// Gets or sets the status message for the manual range input (e.g., "No manual range.", "Invalid input.").
    /// </summary>
    [ObservableProperty] private string _manualRangeStatus = "No manual range.";
    /// <summary>
    /// Gets or sets an error message for invalid manual range input.
    /// </summary>
    [ObservableProperty] private string _manualRangeError = string.Empty;
    /// <summary>
    /// Gets or sets a value indicating whether there is an error in the manual range input.
    /// </summary>
    [ObservableProperty] private bool _hasManualRangeError;

    /// <summary>
    /// Gets or sets the display text for pending column width, handling mixed state.
    /// </summary>
    [ObservableProperty] private string _pendingColumnWidthDisplay = "0";
    /// <summary>
    /// Gets or sets the display text for pending row height, handling mixed state.
    /// </summary>
    [ObservableProperty] private string _pendingRowHeightDisplay = "0";

    // --- Derived Read-Only Properties ---

    /// <summary>
    /// Calculates the total active area of the regular sensor (width * height).
    /// </summary>
    public double RegularSensorArea => (double)(ActiveAreaWidth * ActiveAreaHeight);
    /// <summary>
    /// Calculates the pitch size in the X-direction (width / XChannels).
    /// Returns 0.0 if XChannels is non-positive.
    /// </summary>
    public double PitchSizeX => XChannels <= 0 ? 0.0 : (double)ActiveAreaWidth / (double)XChannels;
    /// <summary>
    /// Calculates the pitch size in the Y-direction (height / YChannels).
    /// Returns 0.0 if YChannels is non-positive.
    /// </summary>
    public double PitchSizeY => YChannels <= 0 ? 0.0 : (double)ActiveAreaHeight / (double)YChannels;

    /// <summary>
    /// Gets a summary string of distinct pitch sizes in X direction from current regular pads.
    /// Falls back to a single value when regular grid is unavailable.
    /// </summary>
    public string PitchSizeXSummary => BuildPitchSizeSummary("Pitch X", "X", PitchSizeX, useWidth: true);
    /// <summary>
    /// Gets a summary string of distinct pitch sizes in Y direction from current regular pads.
    /// Falls back to a single value when regular grid is unavailable.
    /// </summary>
    public string PitchSizeYSummary => BuildPitchSizeSummary("Pitch Y", "Y", PitchSizeY, useWidth: false);

    private const double PitchDistinctToleranceMm = 0.0001;

    private string BuildPitchSizeSummary(string title, string indexPrefix, double fallbackValue, bool useWidth)
    {
        var values = GetDistinctPitchValues(useWidth);
        if (values.Count == 0)
        {
            return $"{title}: {indexPrefix}1={fallbackValue:0.###} mm";
        }

        var entries = values
            .Select((value, index) => $"{indexPrefix}{index + 1}={value:0.###} mm");
        return $"{title}: {string.Join(", ", entries)}";
    }

    private List<double> GetDistinctPitchValues(bool useWidth)
    {
        if (_grid is null || _grid.Pads.Count == 0)
        {
            return new List<double>();
        }

        var orderedValues = _grid.Pads
            .Select(pad => useWidth ? pad.Bounds.Width : pad.Bounds.Height)
            .Where(value => value > 0)
            .OrderBy(value => value)
            .ToList();

        if (orderedValues.Count == 0)
        {
            return new List<double>();
        }

        var distinct = new List<double>();
        foreach (var value in orderedValues)
        {
            if (distinct.Count == 0 || Math.Abs(value - distinct[^1]) > PitchDistinctToleranceMm)
            {
                distinct.Add(value);
            }
        }

        return distinct;
    }

    /// <summary>
    /// Gets or sets the observable collection of <see cref="CascadeIcSetting"/> objects,
    /// used to display and edit individual IC channel settings.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CascadeIcSetting> _cascadeIcSettings = new();

    /// <summary>
    /// Gets a value indicating whether there is more than one cascaded IC.
    /// </summary>
    public bool IsCascadeMulti => CascadeNum > 1;
}
