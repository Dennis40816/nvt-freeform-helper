using System.Globalization;
using FreeformHelper.Domain.Pads;
using NLog;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> handles functionality
/// related to checking the quality of imported DXF data, specifically for overlaps and duplicates.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private List<int> _lastDxfOverlapCadIds = new();

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
            DxfOverlapSummary = "DXF check: no DXF loaded.";
            ClearDxfOverlapResult();
            SetStatus(DxfOverlapSummary);
            Logger.Debug(CultureInfo.InvariantCulture, "DXF overlap check skipped: no DXF loaded.");
            return;
        }
        if (pads.Count == 0)
        {
            DxfOverlapSummary = "DXF check: no CAD output pads (all layers off).";
            ClearDxfOverlapResult();
            SetStatus(DxfOverlapSummary);
            Logger.Debug(CultureInfo.InvariantCulture, "DXF overlap check skipped: no CAD output pads.");
            return;
        }

        await RunUiProgressOperationAsync(
            operationName: "DXF overlap check",
            setBusy: busy => IsCheckingDxfOverlap = busy,
            setProgress: progress => DxfOverlapProgress = progress,
            onStart: () =>
            {
                DxfOverlapSummary = "DXF check: running...";
                SetStatus(DxfOverlapSummary);
                Logger.Info(CultureInfo.InvariantCulture, "DXF overlap check started (visible pads={0}).", pads.Count);
            },
            operationAsync: async reportProgress =>
            {
                var cad = new CadPadSet(pads);
                var report = await Task.Run(() =>
                    Application.Services.DxfOverlapAnalyzer.Analyze(
                        cad,
                        maxSamples: 300,
                        reportProgress: reportProgress));

                DxfOverlapIssues.Clear();
                foreach (var issue in report.SampleIssues)
                {
                    DxfOverlapIssues.Add(issue.BuildMessage());
                }

                DxfOverlapIssueCount = DxfOverlapIssues.Count;
                HasDxfOverlapIssues = DxfOverlapIssues.Count > 0;

                var highlightedIds = report.SampleIssues
                    .SelectMany(issue => new[] { issue.PadAId, issue.PadBId })
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();
                _lastDxfOverlapCadIds = highlightedIds;
                DxfOverlapHighlightedCadIds = new System.Collections.ObjectModel.ObservableCollection<int>(highlightedIds);
                HasDxfOverlapHighlights = highlightedIds.Count > 0;

                if (!report.HasIssues)
                {
                    DxfOverlapSummary = "DXF check: no overlaps detected.";
                    SetStatus(DxfOverlapSummary);
                    Logger.Info(CultureInfo.InvariantCulture, "DXF check: no overlaps detected (pads={0}).", report.PadCount);
                    return;
                }

                DxfOverlapSummary = $"DXF warnings: {report.Summary}.";
                SetStatus(DxfOverlapSummary);
                Logger.Warn(CultureInfo.InvariantCulture, "DXF quality warnings: {0}", report.Summary);
                foreach (var issue in report.SampleIssues)
                {
                    Logger.Warn(CultureInfo.InvariantCulture, "DXF issue: {0}", issue.BuildMessage());
                }
            },
            onError: ex =>
            {
                ClearDxfOverlapResult();
                DxfOverlapSummary = "DXF check failed.";
                SetStatusError("DXF overlap check failed", ex);
                Logger.Error(ex, "DXF overlap check failed.");
            });
    }

    private void SelectDxfOverlapPads()
    {
        var ids = DxfOverlapHighlightedCadIds.Count > 0
            ? DxfOverlapHighlightedCadIds.ToList()
            : _lastDxfOverlapCadIds.ToList();

        if (ids.Count == 0)
        {
            SetStatus("DXF check: no overlap result.");
            return;
        }

        if (DxfOverlapHighlightedCadIds.Count == 0)
        {
            DxfOverlapHighlightedCadIds = new System.Collections.ObjectModel.ObservableCollection<int>(ids);
            HasDxfOverlapHighlights = true;
        }

        _selectionCoordinator.ApplyProgrammaticSelection(
            ids,
            Array.Empty<int>(),
            CanvasHost);
        SetStatus($"DXF check: highlight + select ({ids.Count} pads).");
        Logger.Debug(CultureInfo.InvariantCulture, "DXF overlap highlight + select applied (count={0}).", ids.Count);
    }

    private void ClearDxfOverlapHighlights()
    {
        _selectionCoordinator.ClearSelection(CanvasHost);
        DxfOverlapHighlightedCadIds = new System.Collections.ObjectModel.ObservableCollection<int>();
        HasDxfOverlapHighlights = false;
        SetStatus("DXF check: highlight cleared.");
        Logger.Debug(CultureInfo.InvariantCulture, "DXF overlap highlight + selection cleared.");
    }

    private async Task ShowDxfOverlapDetailsAsync()
    {
        if (OpenDxfOverlapReportAsync is null)
        {
            SetStatus("DXF overlap details: view handler not wired.");
            Logger.Warn(CultureInfo.InvariantCulture, "DXF overlap details requested but view handler is not wired.");
            return;
        }

        var detailLines = DxfOverlapIssues.ToList();
        if (detailLines.Count == 0)
        {
            detailLines = new List<string> { "No overlap issues." };
        }

        Logger.Debug(CultureInfo.InvariantCulture, "DXF overlap details opened (issues={0}).", detailLines.Count);
        await OpenDxfOverlapReportAsync(new DxfOverlapReportViewModel(DxfOverlapSummary, detailLines));
    }

    private void ClearDxfOverlapResult()
    {
        DxfOverlapIssues.Clear();
        DxfOverlapIssueCount = 0;
        HasDxfOverlapIssues = false;
        _lastDxfOverlapCadIds.Clear();
        DxfOverlapHighlightedCadIds = new System.Collections.ObjectModel.ObservableCollection<int>();
        HasDxfOverlapHighlights = false;
    }
}

