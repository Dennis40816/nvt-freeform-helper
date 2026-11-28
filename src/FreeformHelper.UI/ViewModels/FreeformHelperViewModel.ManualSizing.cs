using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Sizing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // UI thread only; construction is inert and identity lasts for the root lifetime.
    private readonly Lazy<ManualSizingOwner> _manualSizingOwner;
    private ManualSizingOwner ManualSizing => _manualSizingOwner.Value;

    private ManualSizingOwner CreateManualSizingOwner() => new(new ManualSizingPorts(
        () => _grid,
        () => XChannels,
        () => YChannels,
        () => ActiveAreaWidth,
        () => ActiveAreaHeight,
        new ManualInputPorts(
            OnPropertyChanging,
            OnPropertyChanged,
            () => _isLoadingSettings,
            () => _settingsSuppression.IsManualSizingApplyActive,
            TrackUndo,
            TrackUndo),
        new ManualRangePorts(
            TryParseRowRange,
            TryParseColRange,
            FormatIndexRanges,
            actualRow => ManualSizingService.ToDisplayRow(actualRow, _grid?.Rows ?? 0),
            EvaluateManualRange,
            () => _selectionCoordinator.ClearSelection(CanvasHost),
            SyncSelectionFromManualRange),
        new SizingEditPorts(
            () => ApplyUiToSettings(_projectFile.Settings, markUnsaved: false),
            ApplySizingMutation,
            ResetSizingMutation,
            () => _cadPadCustomValues,
            PushUndo,
            TriggerGridRebuildAsync,
            () => _selection.SuppressClearOnRebuild = true,
            MarkUnsaved,
            SetStatus,
            (message, args) => _logger.Debug(CultureInfo.InvariantCulture, message, args),
            (message, args) => _logger.Info(CultureInfo.InvariantCulture, message, args),
            (message, args) => _logger.Warn(CultureInfo.InvariantCulture, message, args))));

    private void UpdateManualRangeStatus() => ManualSizing.UpdateManualRangeStatus();

    private static ManualRangePlan EvaluateManualRange(
        RegularGrid grid, string rows, string cols, decimal width, decimal height)
    {
        var plan = ManualSizingUseCase.Evaluate(grid, rows, cols, width, height);
        return new ManualRangePlan(plan.HasError, plan.ErrorMessage,
            plan.Action == ManualSizingAction.SyncSelection, plan.Action == ManualSizingAction.ApplySizing,
            plan.Rows, plan.Cols, plan.Width, plan.Height);
    }

    private bool TryParseRowRange(string input, out List<int> rows, out string error) =>
        ManualSizingService.TryParseRowRange(input, _grid?.Rows ?? 0, out rows, out error);

    private bool TryParseColRange(string input, out List<int> cols, out string error) =>
        ManualSizingService.TryParseColRange(input, _grid?.Cols ?? 0, out cols, out error);

    private void SyncSelectionFromManualRange(IReadOnlyList<int> rows, IReadOnlyList<int> cols)
    {
        if (_grid is null)
        {
            return;
        }

        _selectionCoordinator.SelectRegularRange(_grid, rows, cols, CanvasHost);
    }
}
