using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // --- Manual Sizing Input Fields ---

    // The manual sizing inputs are stored once. The properties below forward to the record and raise
    // the same notifications and callbacks as the stored properties they replaced.
    private ManualSizingInput _manualSizingInput = ManualSizingInput.Initial;

    /// <summary>
    /// Gets or sets the pending width value for manual column/pad sizing.
    /// </summary>
    public decimal PendingColumnWidth
    {
        get => _manualSizingInput.PendingColumnWidth;
        set
        {
            if (_manualSizingInput.PendingColumnWidth == value)
            {
                return;
            }

            OnPendingColumnWidthChanging(value);
            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { PendingColumnWidth = value };
            OnPendingColumnWidthChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnPendingColumnWidthChanging(decimal value);

    partial void OnPendingColumnWidthChanged(decimal value);

    /// <summary>
    /// Gets or sets the pending height value for manual row/pad sizing.
    /// </summary>
    public decimal PendingRowHeight
    {
        get => _manualSizingInput.PendingRowHeight;
        set
        {
            if (_manualSizingInput.PendingRowHeight == value)
            {
                return;
            }

            OnPendingRowHeightChanging(value);
            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { PendingRowHeight = value };
            OnPendingRowHeightChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnPendingRowHeightChanging(decimal value);

    partial void OnPendingRowHeightChanged(decimal value);

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="PendingColumnWidth"/> represents mixed values in a selection.
    /// </summary>
    public bool IsPendingColumnWidthMixed
    {
        get => _manualSizingInput.IsPendingColumnWidthMixed;
        set
        {
            if (_manualSizingInput.IsPendingColumnWidthMixed == value)
            {
                return;
            }

            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { IsPendingColumnWidthMixed = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="PendingRowHeight"/> represents mixed values in a selection.
    /// </summary>
    public bool IsPendingRowHeightMixed
    {
        get => _manualSizingInput.IsPendingRowHeightMixed;
        set
        {
            if (_manualSizingInput.IsPendingRowHeightMixed == value)
            {
                return;
            }

            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { IsPendingRowHeightMixed = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the string input for manual row range selection (e.g., "1-3,5").
    /// </summary>
    public string ManualRowsRange
    {
        get => _manualSizingInput.ManualRowsRange;
        set
        {
            if (_manualSizingInput.ManualRowsRange == value)
            {
                return;
            }

            OnManualRowsRangeChanging(value);
            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { ManualRowsRange = value };
            OnManualRowsRangeChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnManualRowsRangeChanging(string value);

    partial void OnManualRowsRangeChanged(string value);

    /// <summary>
    /// Gets or sets the string input for manual column range selection (e.g., "A-C,E").
    /// </summary>
    public string ManualColsRange
    {
        get => _manualSizingInput.ManualColsRange;
        set
        {
            if (_manualSizingInput.ManualColsRange == value)
            {
                return;
            }

            OnManualColsRangeChanging(value);
            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { ManualColsRange = value };
            OnManualColsRangeChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnManualColsRangeChanging(string value);

    partial void OnManualColsRangeChanged(string value);

    /// <summary>
    /// Gets or sets the status message for the manual range input (e.g., "No manual range.", "Invalid input.").
    /// </summary>
    public string ManualRangeStatus
    {
        get => _manualSizingInput.ManualRangeStatus;
        set
        {
            if (_manualSizingInput.ManualRangeStatus == value)
            {
                return;
            }

            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { ManualRangeStatus = value };
            OnPropertyChanged();
        }
    }

    private string _manualRangeError = string.Empty;

    /// <summary>
    /// Gets or sets an error message for invalid manual range input.
    /// </summary>
    public string ManualRangeError
    {
        get => _manualRangeError;
        set
        {
            var hadError = HasManualRangeError;
            if (SetProperty(ref _manualRangeError, value) && hadError != HasManualRangeError)
            {
                OnPropertyChanged(nameof(HasManualRangeError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether there is an error in the manual range input.
    /// The value follows <see cref="ManualRangeError"/>.
    /// </summary>
    public bool HasManualRangeError => !string.IsNullOrEmpty(_manualRangeError);

    /// <summary>
    /// Gets or sets the display text for pending column width, handling mixed state.
    /// </summary>
    public string PendingColumnWidthDisplay
    {
        get => _manualSizingInput.PendingColumnWidthDisplay;
        set
        {
            if (_manualSizingInput.PendingColumnWidthDisplay == value)
            {
                return;
            }

            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { PendingColumnWidthDisplay = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the display text for pending row height, handling mixed state.
    /// </summary>
    public string PendingRowHeightDisplay
    {
        get => _manualSizingInput.PendingRowHeightDisplay;
        set
        {
            if (_manualSizingInput.PendingRowHeightDisplay == value)
            {
                return;
            }

            OnPropertyChanging();
            _manualSizingInput = _manualSizingInput with { PendingRowHeightDisplay = value };
            OnPropertyChanged();
        }
    }

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
