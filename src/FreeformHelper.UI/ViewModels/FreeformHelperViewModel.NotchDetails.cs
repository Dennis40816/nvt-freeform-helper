using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void RefreshNotchCanvasPreview(bool showStatus)
    {
        if (!ShowNotchCanvasPreview)
        {
            ClearNotchCanvasPreview();
            return;
        }

        if (_grid is null)
        {
            ClearNotchCanvasPreview();
            if (showStatus)
            {
                SetStatus("Notch 2.2 preview: build grid first.");
            }
            return;
        }

        var selectedCadIds = GetSelectedCadIdsForPreview();
        if (selectedCadIds.Count == 0)
        {
            ClearNotchCanvasPreview();
            if (showStatus)
            {
                SetStatus("Notch 2.2 preview: select at least one CAD pad.");
            }
            return;
        }

        if (!TryEnsureWorkflowStep(WorkflowStepId.Step3NotchPreview, suppressStatus: !showStatus))
        {
            ClearNotchCanvasPreview();
            return;
        }

        var items = BuildSelectedCadNotchPreviewItems(selectedCadIds);
        if (items.Count == 0)
        {
            ClearNotchCanvasPreview();
            if (showStatus)
            {
                SetStatus("Notch 2.2 preview: selected CAD has no effective To Full result.");
            }
            return;
        }

        NotchCanvasPreviewItems = new System.Collections.ObjectModel.ObservableCollection<NotchCanvasPreviewItem>(items);
        CanvasHost?.Invalidate();

        if (showStatus)
        {
            var enabledCount = items.Count(static item => item.IsToFullEnabled);
            var disabledCount = items.Count - enabledCount;
            var scopeText = items.Count == 1
                ? $"CAD {items[0].CadPadId}"
                : $"{items.Count} selected CAD pads";
            var toFullState = disabledCount == 0
                ? "To Full effective ON"
                : $"To Full mixed ({enabledCount} ON, {disabledCount} OFF)";
            SetStatus($"Notch 2.2 preview updated: {scopeText}, {toFullState}.");
            OnWorkflowStepCompleted(
                WorkflowStepId.Step3NotchPreview,
                invalidateDownstream: false,
                moveToNextStep: true);
        }
    }

    private List<int> GetSelectedCadIdsForPreview()
    {
        if (_selectedCadIds.Count == 0)
        {
            return [];
        }

        var orderedIds = new List<int>();
        foreach (var pad in CadPads)
        {
            if (_selectedCadIds.Contains(pad.Id))
            {
                orderedIds.Add(pad.Id);
            }
        }

        if (orderedIds.Count > 0)
        {
            return orderedIds;
        }

        return _selectedCadIds.OrderBy(static id => id).ToList();
    }

    private List<NotchCanvasPreviewItem> BuildSelectedCadNotchPreviewItems(List<int> selectedCadIds)
    {
        var workflowSnapshot = BuildWorkflowDataSnapshot();
        var items = new List<NotchCanvasPreviewItem>(selectedCadIds.Count);
        foreach (var cadId in selectedCadIds)
        {
            if (!TryResolveNotchComputationInputs(cadId, out var targetCad, out var sourceCad, out var activeRegularPadIds))
            {
                continue;
            }

            var resolved = GetOrBuildCadV22ResolvedResultCached(
                targetCad,
                sourceCad,
                activeRegularPadIds,
                workflowSnapshot)!;
            var compensation = resolved.Compensation;
            if (!compensation.IsToFullEnabled)
            {
                continue;
            }

            items.Add(
                new NotchCanvasPreviewItem(
                    CadPadId: targetCad.Id,
                    ToRegularRatio: compensation.ToRegularRatio,
                    ToFullRatio: compensation.ToFullRatio,
                    IsToFullEnabled: compensation.IsToFullEnabled,
                    ToFullSeedPolygons: resolved.Stage1SeedPolygons,
                    ToFullCandidatePolygons: resolved.Stage2CandidatePolygons,
                    ToFullPolygons: compensation.ToFullPolygons,
                    ToFullFinalOutlinePolygons: resolved.Stage3FinalOutlinePolygons));
        }

        return items;
    }

    private void ClearNotchCanvasPreview()
    {
        if (NotchCanvasPreviewItems.Count == 0)
        {
            return;
        }

        NotchCanvasPreviewItems = new System.Collections.ObjectModel.ObservableCollection<NotchCanvasPreviewItem>();
        CanvasHost?.Invalidate();
    }

    private async Task ShowNotchDetailAsync()
    {
        if (OpenNotchDetailAsync is null)
        {
            SetStatus("Notch detail: dialog handler not wired.");
            _logger.Warn(CultureInfo.InvariantCulture, "Notch detail requested but dialog handler is not wired.");
            return;
        }

        if (_cad is null || _grid is null)
        {
            SetStatus("Notch detail: import DXF and build grid first.");
            _logger.Debug(CultureInfo.InvariantCulture, "Notch detail skipped: cad/grid not ready.");
            return;
        }

        if (_selectedCadIds.Count == 0)
        {
            SetStatus("Notch detail: select a CAD pad first.");
            _logger.Debug(CultureInfo.InvariantCulture, "Notch detail skipped: no CAD selected.");
            return;
        }

        var cadId = _selectedCadIds.First();
        var cad = _cad.Pads.FirstOrDefault(p => p.Id == cadId)
            ?? CadPads.FirstOrDefault(p => p.Id == cadId);

        if (cad is null)
        {
            SetStatus("Notch detail: selected CAD pad not found.");
            _logger.Warn(CultureInfo.InvariantCulture, "Notch detail skipped: selected CAD pad not found (id={0}).", cadId);
            return;
        }

        var workflowSnapshot = BuildWorkflowDataSnapshot();
        int? dxfIndex = workflowSnapshot.TryGetCadOutputFwDiffIndex(cad.Id, out var idx) ? idx : null;
        var resolved = GetOrBuildCadV22ResolvedResultCached(
            cad,
            GetCadPadsForNotchComputation(cad.Id),
            GetActiveRegularPadIdsForNotchComputation(),
            workflowSnapshot)!;

        var detail = NotchDetailUseCase.BuildFromResolvedResult(
            cad,
            dxfIndex,
            _grid,
            _projectFile.Settings,
            resolved);
        _logger.Info(
            CultureInfo.InvariantCulture,
            "Notch detail opened: cadId={0}, dxfIndex={1}, allocations={2}.",
            cad.Id,
            dxfIndex?.ToString(CultureInfo.InvariantCulture) ?? "-",
            detail.Allocations.Count);
        await OpenNotchDetailAsync(detail);
    }

    /// <summary>
    /// Gets the command to export the notch table in the currently selected format.
    /// </summary>
    public IAsyncRelayCommand ExportNotchCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to open the notch detail view for the selected CAD pad.
    /// </summary>
    public IAsyncRelayCommand ShowNotchDetailCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to set notch validation target REG from current selection.
    /// </summary>
    public IRelayCommand UseSelectedRegularForNotchValidationCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to analyze notch rows/flow for current validation REG.
    /// </summary>
    public IRelayCommand AnalyzeNotchValidationCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to clear notch validation result.
    /// </summary>
    public IRelayCommand ClearNotchValidationCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to focus canvas selection based on one validation row.
    /// </summary>
    public IRelayCommand<NotchValidationDisplayItem> FocusNotchValidationItemCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to move Notch Stage preview to previous stage.
    /// </summary>
    public IRelayCommand PreviousNotchPreviewStageCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to move Notch Stage preview to next stage.
    /// </summary>
    public IRelayCommand NextNotchPreviewStageCommand { get; private set; } = null!;

    /// <summary>Creates the Notch commands. <see cref="InitializeCommands"/> calls it once during construction.</summary>
    private void InitializeNotchCommands()
    {
        ExportNotchCommand = CreateEditingAsyncCommand(ExportNotchAsync);
        ShowNotchDetailCommand = CreateEditingAsyncCommand(ShowNotchDetailAsync);
        UseSelectedRegularForNotchValidationCommand = CreateEditingCommand(UseSelectedRegularForNotchValidation);
        AnalyzeNotchValidationCommand = CreateEditingCommand(AnalyzeNotchValidation);
        ClearNotchValidationCommand = CreateEditingCommand(ClearNotchValidation);
        FocusNotchValidationItemCommand = CreateEditingCommand<NotchValidationDisplayItem>(FocusNotchValidationItem);
        PreviousNotchPreviewStageCommand = CreateEditingCommand(() => ShiftNotchPreviewStage(-1));
        NextNotchPreviewStageCommand = CreateEditingCommand(() => ShiftNotchPreviewStage(1));
    }

    private readonly NotchExportGenerationCacheService _notchExportGenerationCacheService = new();

    private readonly NotchComputationSession _notchComputationSession = new();

    private readonly NotchValidationSession _notchValidationSession = new();

    private readonly NotchExportFingerprintRevisions _notchExportFingerprintRevisions = new();

    private readonly NotchResultCacheState _notchCache = new(); // Holds the notch validation cache, last generated table and playback visibility.

    /// <summary>Gets the notch result cache holder for tests.</summary>
    internal NotchResultCacheState NotchCacheForTests => _notchCache;

    /// <summary>
    /// Holds the last generated notch table and the last preview visibility seen by playback, for the main view model.
    /// </summary>
    internal sealed class NotchResultCacheState
    {
        /// <summary>Gets or sets the table of the last notch generation.</summary>
        internal NotchTable? LastGeneratedTable { get; set; }

        /// <summary>Gets or sets the preview visibility that the last playback sync saw.</summary>
        internal bool LastPreviewVisibleForPlayback { get; set; }
    }
}
