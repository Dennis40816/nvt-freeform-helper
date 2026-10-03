using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal readonly record struct SimulationLegendRange(
    double Minimum,
    double Maximum,
    double MaximumAbsDelta,
    double AutoNegativeClampAbs,
    double AutoPositiveClamp);

internal sealed class SimulationOverlayProjection
{
    public SimulationOverlayProjection(
        IReadOnlyList<NotchApplySimulationAaDisplayCell> overlayItems,
        IReadOnlyDictionary<int, NotchApplySimulationDiffCell> cellsByRegularPadId,
        SimulationLegendRange legendRange)
    {
        OverlayItems = overlayItems;
        CellsByRegularPadId = cellsByRegularPadId;
        LegendRange = legendRange;
    }

    public IReadOnlyList<NotchApplySimulationAaDisplayCell> OverlayItems { get; }
    public IReadOnlyDictionary<int, NotchApplySimulationDiffCell> CellsByRegularPadId { get; }
    public SimulationLegendRange LegendRange { get; }
}

internal static class SimulationOverlayProjectionBuilder
{
    private const double SignificantDeltaEpsilon = 1e-9;

    public static SimulationOverlayProjection Build(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        int? selectedAreaIcIndex,
        NotchApplySimulationCanvasViewMode viewMode)
    {
        var cellsByRegularPadId = new Dictionary<int, NotchApplySimulationDiffCell>();
        var overlayItems = new List<NotchApplySimulationAaDisplayCell>(cells.Count);

        // Cells are produced in row/col order by NotchApplySimulationService.
        foreach (var cell in cells)
        {
            if (selectedAreaIcIndex.HasValue && cell.IcIndex != selectedAreaIcIndex.Value)
            {
                continue;
            }

            cellsByRegularPadId[cell.RegularPadId] = cell;
            var displayValue = viewMode switch
            {
                NotchApplySimulationCanvasViewMode.Before => cell.BeforeValue,
                NotchApplySimulationCanvasViewMode.After => cell.AfterValue,
                _ => cell.DeltaValue,
            };
            var isChanged = Math.Abs(cell.DeltaValue) > SignificantDeltaEpsilon;
            var valueText = viewMode switch
            {
                NotchApplySimulationCanvasViewMode.Delta or NotchApplySimulationCanvasViewMode.ChangedOnly
                    => displayValue.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture),
                NotchApplySimulationCanvasViewMode.Before or NotchApplySimulationCanvasViewMode.After
                    => SimulationWorkspaceViewModel.FormatWholeNumberDisplay(displayValue),
                _ => displayValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            };
            if (viewMode == NotchApplySimulationCanvasViewMode.ChangedOnly && !isChanged)
            {
                valueText = SimulationWorkspaceViewModel.FormatWholeNumberDisplay(cell.BeforeValue);
            }

            overlayItems.Add(new NotchApplySimulationAaDisplayCell(
                cell.RegularPadId,
                cell.RegularRow,
                cell.RegularCol,
                cell.RegularRow,
                cell.RegularCol,
                cell.IcIndex,
                cell.DiffIndex,
                cell.BeforeValue,
                cell.AfterValue,
                cell.DeltaValue,
                displayValue,
                isChanged,
                valueText));
        }

        return new SimulationOverlayProjection(
            overlayItems,
            cellsByRegularPadId,
            BuildLegendRange(overlayItems));
    }

    private static SimulationLegendRange BuildLegendRange(List<NotchApplySimulationAaDisplayCell> overlayItems)
    {
        if (overlayItems.Count == 0)
        {
            return new SimulationLegendRange(0d, 0d, 0d, 0d, 0d);
        }

        var autoScaleRange = Controls.SimulationColorScaleResolver.ComputeAutoScaleRange(
            overlayItems.Select(static item => item.DisplayValue));
        return new SimulationLegendRange(
            autoScaleRange.Minimum,
            autoScaleRange.Maximum,
            overlayItems.Max(static item => Math.Abs(item.DeltaValue)),
            autoScaleRange.NegativeClampAbs,
            autoScaleRange.PositiveClamp);
    }
}
