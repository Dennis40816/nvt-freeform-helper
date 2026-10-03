using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    private void RebuildOverlayItems(IReadOnlyList<NotchApplySimulationDiffCell> cells)
    {
        var projection = SimulationOverlayProjectionBuilder.Build(
            cells,
            SelectedAreaOption.IcIndex,
            SelectedCanvasViewOption.Value);

        _cellsByRegularPadId = projection.CellsByRegularPadId;
        _simulationOverlayItems = projection.OverlayItems;
        _legendMinimumValue = projection.LegendRange.Minimum;
        _legendMaximumValue = projection.LegendRange.Maximum;
        _legendMaximumAbsDelta = projection.LegendRange.MaximumAbsDelta;
        _legendAutoNegativeClampAbs = projection.LegendRange.AutoNegativeClampAbs;
        _legendAutoPositiveClamp = projection.LegendRange.AutoPositiveClamp;

        EnsureSelectedRegularWithinArea();
        OnPropertyChanged(nameof(SimulationOverlayItems));
        OnPropertyChanged(nameof(HasSimulationData));
        OnPropertyChanged(nameof(CanvasHintText));
        OnPropertyChanged(nameof(HasCanvasHint));
        OnPropertyChanged(nameof(ColorLegendTitleText));
        OnPropertyChanged(nameof(ColorLegendMeaningText));
        OnPropertyChanged(nameof(ColorLegendStartText));
        OnPropertyChanged(nameof(ColorLegendCenterText));
        OnPropertyChanged(nameof(ColorLegendEndText));
        OnPropertyChanged(nameof(ShowLegendCenterLabel));
        OnPropertyChanged(nameof(ShowSequentialLegend));
    }

    private bool IsCellInSelectedArea(NotchApplySimulationDiffCell cell)
    {
        return !SelectedAreaOption.IcIndex.HasValue || cell.IcIndex == SelectedAreaOption.IcIndex.Value;
    }

    private bool IsPadInSelectedArea(RegularPad pad)
    {
        return !SelectedAreaOption.IcIndex.HasValue || pad.IcIndex == SelectedAreaOption.IcIndex.Value;
    }

    private bool IsPadInSelectedAreaAndActiveSurface(RegularPad pad)
    {
        return IsPadInSelectedArea(pad) && _session.ActiveRegularPadIds.Contains(pad.RegularPadId);
    }

    private void EnsureSelectedRegularWithinArea()
    {
        if (SelectedRegularPadId < 0)
        {
            return;
        }

        if (_cellsByRegularPadId.ContainsKey(SelectedRegularPadId))
        {
            return;
        }

        SelectedRegularPadId = -1;
    }

    partial void OnSelectedCanvasViewOptionChanged(NotchApplySimulationCanvasViewOption value)
    {
        OnPropertyChanged(nameof(SelectedCanvasViewMode));
        OnPropertyChanged(nameof(ViewModeSummaryText));
        OnPropertyChanged(nameof(IsSignedCanvasView));
        if (_snapshot is not null)
        {
            RebuildOverlayItems(_snapshot.Result.Cells);
        }
    }

    partial void OnSelectedCanvasColorOptionChanged(NotchApplySimulationCanvasColorOption value)
    {
        OnPropertyChanged(nameof(SelectedCanvasColorMode));
        OnPropertyChanged(nameof(ColorModeSummaryText));
        RefreshCommandState();
    }

    partial void OnSelectedAreaOptionChanged(NotchApplySimulationAreaOption value)
    {
        OnPropertyChanged(nameof(AreaSummaryText));
        OnPropertyChanged(nameof(CanvasStatusSummaryText));
        if (_snapshot is not null)
        {
            RebuildOverlayItems(_snapshot.Result.Cells);
            RefreshDiffViolationState();
        }

        RefreshSelectionPresentation();
        RefreshCommandState();
    }

    partial void OnColorThresholdValueChanged(double value)
    {
        if (value < 0d)
        {
            ColorThresholdValue = 0d;
            return;
        }

        OnPropertyChanged(nameof(ColorModeSummaryText));
        OnPropertyChanged(nameof(ColorLegendEndText));
        OnPropertyChanged(nameof(ColorLegendStartText));
    }

    private double GetLegendSignedRange()
    {
        if (IsThresholdMode)
        {
            return Math.Max(0d, ColorThresholdValue);
        }

        if (SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Auto)
        {
            return Math.Max(_legendAutoNegativeClampAbs, _legendAutoPositiveClamp);
        }

        return IsSignedCanvasView
            ? _legendMaximumAbsDelta
            : Math.Max(Math.Abs(_legendMinimumValue), Math.Abs(_legendMaximumValue));
    }

    private static string FormatLegendValue(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatSignedLegendValue(double value)
    {
        return value.ToString("+0.###;-0.###;0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
