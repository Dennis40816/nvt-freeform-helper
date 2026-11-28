using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public decimal PendingColumnWidth
    {
        get => ManualSizing.PendingColumnWidth;
        set => ManualSizing.PendingColumnWidth = value;
    }

    public decimal PendingRowHeight
    {
        get => ManualSizing.PendingRowHeight;
        set => ManualSizing.PendingRowHeight = value;
    }

    public bool IsPendingColumnWidthMixed
    {
        get => ManualSizing.IsPendingColumnWidthMixed;
        set => ManualSizing.IsPendingColumnWidthMixed = value;
    }

    public bool IsPendingRowHeightMixed
    {
        get => ManualSizing.IsPendingRowHeightMixed;
        set => ManualSizing.IsPendingRowHeightMixed = value;
    }

    public string ManualRowsRange
    {
        get => ManualSizing.ManualRowsRange;
        set => ManualSizing.ManualRowsRange = value;
    }

    public string ManualColsRange
    {
        get => ManualSizing.ManualColsRange;
        set => ManualSizing.ManualColsRange = value;
    }

    public string ManualRangeStatus
    {
        get => ManualSizing.ManualRangeStatus;
        set => ManualSizing.ManualRangeStatus = value;
    }

    public string ManualRangeError
    {
        get => ManualSizing.ManualRangeError;
        set => ManualSizing.ManualRangeError = value;
    }

    public bool HasManualRangeError => ManualSizing.HasManualRangeError;

    public double PitchSizeX => ManualSizing.PitchSizeX;

    public double PitchSizeY => ManualSizing.PitchSizeY;

    public string PitchSizeXSummary => ManualSizing.PitchSizeXSummary;

    public string PitchSizeYSummary => ManualSizing.PitchSizeYSummary;

    /// <summary>
    /// Gets or sets the observable collection of <see cref="CascadeIcSetting"/> objects,
    /// used to display and edit individual IC channel settings.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CascadeIcSetting> _cascadeIcSettings = new();
}
