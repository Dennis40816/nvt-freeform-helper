using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> handles functionality
/// related to checking the quality of imported DXF data, specifically for overlaps and duplicates.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Performs a quality check on the loaded DXF data to detect duplicate and overlapping pads.
    /// Updates UI properties with a summary of findings and detailed issues.
    /// </summary>
    private async Task CheckDxfQualityAsync()
    {
        var isLoaded = _cad is not null;
        var pads = CadPads.ToList();
        if (!isLoaded)
        {
            SetDxfOverlapEmptyResult("DXF check: no DXF loaded.");
            SetStatus(DxfOverlapSummary);
            _logger.Debug(CultureInfo.InvariantCulture, "DXF overlap check skipped: no DXF loaded.");
            return;
        }
        if (pads.Count == 0)
        {
            SetDxfOverlapEmptyResult("DXF check: no CAD output pads (all layers off).");
            SetStatus(DxfOverlapSummary);
            _logger.Debug(CultureInfo.InvariantCulture, "DXF overlap check skipped: no CAD output pads.");
            return;
        }
        await RunUiProgressOperationAsync(
            operationName: "DXF overlap check",
            setBusy: busy => IsCheckingDxfOverlap = busy,
            setProgress: progress => DxfOverlapProgress = progress,
            onStart: () =>
            {
                DxfOverlapResult = DxfOverlapResult with { Summary = "DXF check: running..." };
                SetStatus(DxfOverlapSummary);
                _logger.Info(CultureInfo.InvariantCulture, "DXF overlap check started (visible pads={0}).", pads.Count);
            },
            operationAsync: CheckOverlapAsync,
            onError: ex =>
            {
                SetDxfOverlapEmptyResult("DXF check failed.");
                SetStatusError("DXF overlap check failed", ex);
                _logger.Error(ex, "DXF overlap check failed.");
            });
        async Task CheckOverlapAsync(Action<double> reportProgress)
        {
            var cad = new CadPadSet(pads);
            var report = await Task.Run(() =>
                Application.Services.DxfOverlapAnalyzer.Analyze(
                    cad,
                    maxSamples: 300,
                    reportProgress: reportProgress));

            var highlightedIds = report.SampleIssues
                .SelectMany(issue => new[] { issue.PadAId, issue.PadBId })
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            DxfOverlapResult = new DxfOverlapResult(
                DxfOverlapSummary,
                new ObservableCollection<string>(report.SampleIssues.Select(issue => issue.BuildMessage())),
                new ObservableCollection<int>(highlightedIds),
                highlightedIds);

            if (!report.HasIssues)
            {
                DxfOverlapResult = DxfOverlapResult with { Summary = "DXF check: no overlaps detected." };
                SetStatus(DxfOverlapSummary);
                _logger.Info(CultureInfo.InvariantCulture, "DXF check: no overlaps detected (pads={0}).", report.PadCount);
                return;
            }

            DxfOverlapResult = DxfOverlapResult with { Summary = $"DXF warnings: {report.Summary}." };
            SetStatus(DxfOverlapSummary);
            _logger.Warn(CultureInfo.InvariantCulture, "DXF quality warnings: {0}", report.Summary);
            foreach (var issue in report.SampleIssues)
            {
                _logger.Warn(CultureInfo.InvariantCulture, "DXF issue: {0}", issue.BuildMessage());
            }
        }
    }

    private void SelectDxfOverlapPads()
    {
        var ids = DxfOverlapHighlightedCadIds.Count > 0
            ? DxfOverlapHighlightedCadIds.ToList()
            : DxfOverlapResult.LastCadIds.ToList();

        if (ids.Count == 0)
        {
            SetStatus("DXF check: no overlap result.");
            return;
        }

        if (DxfOverlapHighlightedCadIds.Count == 0)
        {
            DxfOverlapResult = DxfOverlapResult with { HighlightedCadIds = new ObservableCollection<int>(ids) };
        }

        _selectionCoordinator.ApplyProgrammaticSelection(
            ids,
            Array.Empty<int>(),
            CanvasHost);
        SetStatus($"DXF check: highlight + select ({ids.Count} pads).");
        _logger.Debug(CultureInfo.InvariantCulture, "DXF overlap highlight + select applied (count={0}).", ids.Count);
    }

    private void ClearDxfOverlapHighlights()
    {
        _selectionCoordinator.ClearSelection(CanvasHost);
        DxfOverlapResult = DxfOverlapResult with { HighlightedCadIds = new ObservableCollection<int>() };
        SetStatus("DXF check: highlight cleared.");
        _logger.Debug(CultureInfo.InvariantCulture, "DXF overlap highlight + selection cleared.");
    }

    private async Task ShowDxfOverlapDetailsAsync()
    {
        if (OpenDxfOverlapReportAsync is null)
        {
            SetStatus("DXF overlap details: view handler not wired.");
            _logger.Warn(CultureInfo.InvariantCulture, "DXF overlap details requested but view handler is not wired.");
            return;
        }

        var detailLines = DxfOverlapIssues.ToList();
        if (detailLines.Count == 0)
        {
            detailLines = new List<string> { "No overlap issues." };
        }

        _logger.Debug(CultureInfo.InvariantCulture, "DXF overlap details opened (issues={0}).", detailLines.Count);
        await OpenDxfOverlapReportAsync(new DxfOverlapReportViewModel(DxfOverlapSummary, detailLines));
    }

    private void SetDxfOverlapEmptyResult(string summary)
    {
        DxfOverlapResult = DxfOverlapResult.CreateNotRun() with { Summary = summary };
    }

    partial void OnDxfOverlapResultChanged(DxfOverlapResult? oldValue, DxfOverlapResult newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.Summary, newValue.Summary, nameof(DxfOverlapSummary));
        RaiseWhenChanged(oldValue.Issues, newValue.Issues, nameof(DxfOverlapIssues));
        RaiseWhenChanged(oldValue.Issues.Count, newValue.Issues.Count, nameof(DxfOverlapIssueCount));
        RaiseWhenChanged(oldValue.Issues.Count > 0, newValue.Issues.Count > 0, nameof(HasDxfOverlapIssues));
        RaiseWhenChanged(oldValue.HighlightedCadIds, newValue.HighlightedCadIds, nameof(DxfOverlapHighlightedCadIds));
    }

    /// <summary>
    /// Gets or sets the observable collection of <see cref="LayerToggle"/> objects
    /// used to control the visibility of CAD layers.
    /// </summary>
    [ObservableProperty] private ObservableCollection<LayerToggle> _layerToggles = new();

    /// <summary>
    /// Gets or sets the available layer options for bounding the grid.
    /// </summary>
    [ObservableProperty] private ObservableCollection<BoundLayerOption> _boundLayerOptions = new();

    /// <summary>
    /// Gets or sets the available layer options for DXF regular source mode.
    /// </summary>
    [ObservableProperty] private ObservableCollection<RegularSourceLayerOption> _regularSourceLayerOptions = new();

    // --- DXF Quality Check Properties ---

    /// <summary>
    /// Gets or sets the latest DXF overlap check result. The view model replaces the whole value.
    /// The properties below forward to it and raise their change notification when their own
    /// value changed.
    /// </summary>
    [ObservableProperty] private DxfOverlapResult _dxfOverlapResult = DxfOverlapResult.CreateNotRun();
    /// <summary>
    /// Gets the summary message for DXF overlap issues (e.g., "no overlaps detected").
    /// </summary>
    public string DxfOverlapSummary => DxfOverlapResult.Summary;

    /// <summary>
    /// Gets the observable collection of detailed DXF overlap issue messages.
    /// </summary>
    public ObservableCollection<string> DxfOverlapIssues => DxfOverlapResult.Issues;

    /// <summary>
    /// Gets the number of overlap issues currently loaded in details.
    /// </summary>
    public int DxfOverlapIssueCount => DxfOverlapResult.Issues.Count;

    /// <summary>
    /// Gets a value indicating whether any DXF overlap issues were found.
    /// </summary>
    public bool HasDxfOverlapIssues => DxfOverlapResult.Issues.Count > 0;

    /// <summary>
    /// Gets or sets a value indicating whether DXF overlap checking is currently running.
    /// </summary>
    public bool IsCheckingDxfOverlap
    {
        get => _dxfOverlapState.Current.IsBusy;
        set => SetOperationBusy(_dxfOverlapState, value, nameof(IsCheckingDxfOverlap));
    }

    /// <summary>
    /// Gets or sets the current progress value (0-1) for DXF overlap checking.
    /// </summary>
    public double DxfOverlapProgress
    {
        get => _dxfOverlapState.Current.Progress;
        set => SetOperationProgress(_dxfOverlapState, value, nameof(DxfOverlapProgress));
    }

    /// <summary>
    /// Gets the CAD pad IDs highlighted by overlap checking.
    /// </summary>
    public ObservableCollection<int> DxfOverlapHighlightedCadIds => DxfOverlapResult.HighlightedCadIds;
}

