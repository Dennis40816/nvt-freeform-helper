// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    public static string FormatSelectionCount(int count) => count > 1
        ? $"Selected pads: {count.ToString(CultureInfo.InvariantCulture)}; inspecting the first regular pad."
        : string.Empty;

    public string InputPurposeText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Csv => "Play frames from imported CSV files.",
        SimulationInputSourceMode.Copper => "Move a copper contact over the panel.",
        _ => "Type values per pad or for the whole panel.",
    };

    public string ColorScaleCaptionText => _canvas.ColorScaleCaptionText;

    public string RandomScopeText => $"Random fills {AreaSummaryText} only.";

    public string SelectedImpactCountText => _selection.SelectedImpactCountText;

    public string CsvCompatibilityText => !HasCsvFiles
        ? "Import CSV files to check compatibility."
        : HasCsvData
            ? "Compatible frames are available. See Playback below the canvas."
            : "No compatible frames. Check file dimensions in Format details.";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(IsManualSource):
                OnPropertyChanged(nameof(InputPurposeText));
                break;
            case nameof(HasCsvData):
                OnPropertyChanged(nameof(CsvCompatibilityText));
                break;
            case nameof(HasSelectedImpactItems):
                OnPropertyChanged(nameof(SelectedImpactCountText));
                break;
            case nameof(SelectedCanvasViewMode):
                OnPropertyChanged(nameof(ColorScaleCaptionText));
                break;
            case nameof(AreaSummaryText):
                OnPropertyChanged(nameof(RandomScopeText));
                break;
        }
    }
}
