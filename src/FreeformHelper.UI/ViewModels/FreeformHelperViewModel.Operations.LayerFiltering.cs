using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    internal void FilterCadPadsByLayer()
    {
        if (_cad is null)
        {
            CadPads = new ObservableCollection<CadPad>();
            _cachedFilteredCadForBuild = null;
            _cadOutputFwDiffIndexByCadId.Clear();
            _cadDisplayIndexByCadId.Clear();
            SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
            RefreshDxfEditLayerOptions(Array.Empty<string>());
            SyncHiddenCadIdsToProject();
            ClearPadMatchResult(clearRegularAssignments: true);
            if (!_isLoadingSettings)
            {
                ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
            }
            return;
        }

        try
        {
            var result = LayerFilterUseCase.Filter(_cad, LayerToggles);
            var visible = result.VisiblePads
                .Where(p => !IsCadPadEffectivelyHidden(p.Id))
                .ToList();
            UpdateCadOutputFwDiffIndexing(visible);
            SyncHiddenCadIdsToProject();
            ClearPadMatchResult(clearRegularAssignments: true);
            if (!_isLoadingSettings)
            {
                ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
            }

            // If configured, trigger a grid rebuild when layer filters change (as bounds might change).
            // Suppress during bulk loads; callers will request an explicit rebuild.
            if (RecalcBoundsOnLayerFilter && !_isLoadingSettings)
            {
                _ = TriggerGridRebuildAsync(requestFit: false);
            }
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            SetStatusError("Layer filter failed", ex);
            _logger.Error(ex, "Layer filter failed.");
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether CAD bounds should be recalculated from visible layers
    /// when layer filters change and on grid rebuilds.
    /// </summary>
    [ObservableProperty]
    private bool _recalcBoundsOnLayerFilter;
}

