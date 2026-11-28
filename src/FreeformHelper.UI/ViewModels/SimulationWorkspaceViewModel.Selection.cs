// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    public string? BuildHoverTipText(int regularPadId) => _selection.BuildHoverTipText(regularPadId);

    internal bool TryGetRegularQuerySnapshot(int regularPadId, out SimulationRegularQuerySnapshot snapshot) =>
        _selection.TryGetRegularQuerySnapshot(regularPadId, out snapshot);

    public void SelectRegularPad(int regularPadId) => _selection.SelectRegularPad(regularPadId);

    private void RefreshSelectionPresentation() => _selection.RefreshSelectionPresentation();

    public void RefreshExternalBindings()
    {
        RefreshCommandState();
    }

    private void RefreshCommandState()
    {
        ImportCsvCommand.NotifyCanExecuteChanged();
        ClearCsvCommand.NotifyCanExecuteChanged();
        RandomizeVisibleValuesCommand.NotifyCanExecuteChanged();
        ApplyManualPresetCommand.NotifyCanExecuteChanged();
        TogglePlaybackCommand.NotifyCanExecuteChanged();
        PreviousFrameCommand.NotifyCanExecuteChanged();
        NextFrameCommand.NotifyCanExecuteChanged();
        ApplySelectedOverrideCommand.NotifyCanExecuteChanged();
        ClearSelectedOverrideCommand.NotifyCanExecuteChanged();
        ClearAllManualOverridesCommand.NotifyCanExecuteChanged();
        ReplayCopperPathCommand.NotifyCanExecuteChanged();
        UseCurrentCopperAsPathStartCommand.NotifyCanExecuteChanged();
        UseCurrentCopperAsPathEndCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanImportCsv));
        OnPropertyChanged(nameof(HasCsvFiles));
        OnPropertyChanged(nameof(HasCsvData));
        OnPropertyChanged(nameof(IsCsvSource));
        OnPropertyChanged(nameof(IsCopperSource));
        OnPropertyChanged(nameof(IsManualSource));
        OnPropertyChanged(nameof(ShowSimulationDiagnostics));
        OnPropertyChanged(nameof(HasUserDerivedState));
        OnPropertyChanged(nameof(CanAutoRefreshFromSourceChange));
        OnPropertyChanged(nameof(CanPlayFrames));
        OnPropertyChanged(nameof(CanStepFrames));
        OnPropertyChanged(nameof(CanRandomizeVisibleValues));
        OnPropertyChanged(nameof(IsThresholdMode));
        OnPropertyChanged(nameof(IsSignedCanvasView));
        OnPropertyChanged(nameof(HasSelectedRegular));
        OnPropertyChanged(nameof(HasManualOverrides));
        OnPropertyChanged(nameof(HasSelectedOverride));
        OnPropertyChanged(nameof(InputPrimarySummaryText));
        OnPropertyChanged(nameof(InputSecondarySummaryText));
        OnPropertyChanged(nameof(InputContractTitleText));
        OnPropertyChanged(nameof(InputContractDetailText));
        OnPropertyChanged(nameof(CsvProjectionContractText));
        OnPropertyChanged(nameof(HasCsvInspectorData));
        OnPropertyChanged(nameof(CsvInspectorSummaryText));
        OnPropertyChanged(nameof(CsvInspectorShapeSummaryText));
        OnPropertyChanged(nameof(SourceModeSummaryText));
        OnPropertyChanged(nameof(FrameSummaryText));
        OnPropertyChanged(nameof(VersionSummaryText));
        OnPropertyChanged(nameof(ViewModeSummaryText));
        OnPropertyChanged(nameof(ColorModeSummaryText));
        OnPropertyChanged(nameof(CanvasStatusSummaryText));
        OnPropertyChanged(nameof(PlaybackButtonText));
        OnPropertyChanged(nameof(PlaybackButtonGlyph));
        OnPropertyChanged(nameof(PlaybackLoopText));
        OnPropertyChanged(nameof(SelectedFrameSliderMaximum));
        OnPropertyChanged(nameof(SelectedFrameDisplayText));
        OnPropertyChanged(nameof(ShowSelectedOverrideEditor));
        OnPropertyChanged(nameof(ColorLegendTitleText));
        OnPropertyChanged(nameof(ColorLegendMeaningText));
        OnPropertyChanged(nameof(ColorLegendStartText));
        OnPropertyChanged(nameof(ColorLegendCenterText));
        OnPropertyChanged(nameof(ColorLegendEndText));
        OnPropertyChanged(nameof(ShowLegendCenterLabel));
        OnPropertyChanged(nameof(ShowSequentialLegend));
        OnPropertyChanged(nameof(HasDuplicateDiffResolutions));
        OnPropertyChanged(nameof(DuplicateDiffResolutionCount));
        OnPropertyChanged(nameof(DuplicateDiffResolutionStrategyText));
        OnPropertyChanged(nameof(DuplicateDiffResolutionSummaryText));
        OnPropertyChanged(nameof(DuplicateDiffResolutionSampleText));
        OnPropertyChanged(nameof(SelectedImpactSummaryText));
        OnPropertyChanged(nameof(HasSelectedImpactItems));
        OnPropertyChanged(nameof(CopperPositionText));
        OnPropertyChanged(nameof(CopperSummaryText));
        OnPropertyChanged(nameof(CopperProjectionModelText));
        OnPropertyChanged(nameof(CopperContactModelSummaryText));
        OnPropertyChanged(nameof(CopperInstructionText));
        OnPropertyChanged(nameof(CopperPathReplayArtifactSummaryText));
    }

    internal static string FormatWholeNumberDisplay(double value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    }
}
