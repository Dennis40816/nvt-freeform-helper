using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Contracts;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Grid;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // Each operation snapshot is owned and accessed only on the UI thread.
    private readonly UiOperationProgressState _matchState = new();
    private readonly UiOperationProgressState _indexMappingState = new();

    private void SetOperationBusy(UiOperationProgressState state, bool value, string propertyName)
    {
        if (state.Current.IsBusy == value)
        {
            return;
        }

        OnPropertyChanging(propertyName);
        state.SetBusy(value);
        OnPropertyChanged(propertyName);
    }

    private void SetOperationProgress(UiOperationProgressState state, double value, string propertyName)
    {
        if (EqualityComparer<double>.Default.Equals(state.Current.Progress, value))
        {
            return;
        }

        OnPropertyChanging(propertyName);
        state.SetProgress(value);
        OnPropertyChanged(propertyName);
    }

    // UI thread only; construction does not publish state or start work.
    private readonly Lazy<GridMatchingOwner> _gridMatchingOwner;
    private GridMatchingOwner GridMatching => _gridMatchingOwner.Value;

    private GridMatchingOwner CreateGridMatchingOwner() => new(
        new GridMatchingPorts(CreateGridStatePorts(), CreateGridEffectPorts(), CreateGridWorkflowPorts(),
            new GridMatchPorts(
                GetFilteredMatchInputs,
                GetActiveMatchInputs,
                CreateGridStatusPorts,
                RunGridMatchOperationAsync,
                RunFreeformOperationAsync,
                MatchPadsInBackgroundAsync,
                () => _projectFile.Settings.Matching,
                () => AutoReplayStep2AfterProjectLoad,
                BuildStep2FreeformAssignmentsAsync,
                FreeformTaggingUseCase.ApplyAutoDetectAssignments,
                grid => PadOverrideService.ApplyFreeformOverrides(_projectFile, grid),
                FreeformTaggingUseCase.ApplyManual,
                SyncManualFreeformOverrides,
                RebuildVisibleDxfIndexMap,
                links => MatchLinksForCanvas = links,
                result => FreeformAutoDetectResult = result,
                () => FreeformAutoDetectSummary,
                ex => _logger.Error(ex, "Match failed."))),
        CreateGridRebuildOwner());

    private Task MatchAsync() => GridMatching.MatchAsync();

    private Task<bool> RestoreStep1MatchAfterProjectLoadAsync() => GridMatching.RestoreStep1MatchAfterProjectLoadAsync();

    private Task<bool> RestoreStep2FreeformAfterProjectLoadAsync() => GridMatching.RestoreStep2FreeformAfterProjectLoadAsync();

    private Task AutoDetectFreeformsAsync() => GridMatching.AutoDetectFreeformsAsync();

    private bool GetFilteredMatchInputs(string missingStatus, out CadPadSet cad, out RegularGrid grid) =>
        TryGetOperationCadAndGrid(BuildFilteredCadPadSet, missingStatus, out cad, out grid);

    private bool GetActiveMatchInputs(string missingStatus, out CadPadSet cad, out RegularGrid grid) =>
        TryGetOperationCadAndGrid(CadDocument.BuildActiveCad, missingStatus, out cad, out grid);

    private GridStatusPorts CreateGridStatusPorts(string operationName)
    {
        var status = CreateStatusScope(operationName);
        return new GridStatusPorts(status.ReportProgress, status.ReportSuccess, status.ReportError);
    }

    private Task RunGridMatchOperationAsync(
        Action onStart, Func<Action<double>, Task> operationAsync, Action<Exception> onError) =>
        RunUiProgressOperationAsync("Match task", busy => IsMatchingPads = busy,
            progress => MatchProgress = progress, operationAsync, onError, onStart);

    private async Task RunFreeformOperationAsync(Func<Task> operationAsync)
    {
        try
        {
            await operationAsync();
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            SetStatusError("Auto detect failed", ex);
        }
    }

    private static Task<PadMatchResult> MatchPadsInBackgroundAsync(
        CadPadSet cad, RegularGrid grid, Action<double> reportProgress) =>
        Task.Run(() => PadMatchService.Match(cad, grid, reportProgress));

    private async Task<IReadOnlyDictionary<int, FreeformType>> BuildStep2FreeformAssignmentsAsync(
        CadPadSet cad,
        RegularGrid grid,
        MatchingSettings matchingSettings)
    {
        return await Task.Run(() => FreeformTaggingUseCase.BuildAutoDetectAssignments(
            cad,
            grid,
            matchingSettings,
            _workflowData.LatestPadMatch));
    }

    /// <summary>
    /// Gets or sets a value indicating whether pad matching is currently running.
    /// </summary>
    public bool IsMatchingPads
    {
        get => _matchState.Current.IsBusy;
        set => SetOperationBusy(_matchState, value, nameof(IsMatchingPads));
    }

    /// <summary>
    /// Gets or sets the current progress value (0-1) for pad matching.
    /// </summary>
    public double MatchProgress
    {
        get => _matchState.Current.Progress;
        set => SetOperationProgress(_matchState, value, nameof(MatchProgress));
    }
}
