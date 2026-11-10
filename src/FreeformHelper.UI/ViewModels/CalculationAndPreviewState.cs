namespace FreeformHelper.UI.ViewModels;

/// <summary>Stores the calculation and preview settings for one view model.</summary>
internal sealed class CalculationAndPreviewState
{
    public GridAndMatchSettings GridAndMatch { get; set; } = GridAndMatchSettings.Initial;

    public IndexMappingTuning IndexMappingTuning { get; set; } = IndexMappingTuning.Initial;

    public NotchAlgorithmSettings NotchAlgorithm { get; set; } = NotchAlgorithmSettings.Initial;

    public NotchPreviewSettings NotchPreview { get; set; } = NotchPreviewSettings.Initial;
}
