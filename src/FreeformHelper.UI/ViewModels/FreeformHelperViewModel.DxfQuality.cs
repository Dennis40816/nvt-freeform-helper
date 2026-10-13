using System.Collections.ObjectModel;
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
            Logger.Debug(CultureInfo.InvariantCulture, "DXF overlap check skipped: no DXF loaded.");
            return;
        }
        if (pads.Count == 0)
        {
            SetDxfOverlapEmptyResult("DXF check: no CAD output pads (all layers off).");
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
                DxfOverlapResult = DxfOverlapResult with { Summary = "DXF check: running..." };
                SetStatus(DxfOverlapSummary);
                Logger.Info(CultureInfo.InvariantCulture, "DXF overlap check started (visible pads={0}).", pads.Count);
            },
            operationAsync: CheckOverlapAsync,
            onError: ex =>
            {
                SetDxfOverlapEmptyResult("DXF check failed.");
                SetStatusError("DXF overlap check failed", ex);
                Logger.Error(ex, "DXF overlap check failed.");
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
                Logger.Info(CultureInfo.InvariantCulture, "DXF check: no overlaps detected (pads={0}).", report.PadCount);
                return;
            }

            DxfOverlapResult = DxfOverlapResult with { Summary = $"DXF warnings: {report.Summary}." };
            SetStatus(DxfOverlapSummary);
            Logger.Warn(CultureInfo.InvariantCulture, "DXF quality warnings: {0}", report.Summary);
            foreach (var issue in report.SampleIssues)
            {
                Logger.Warn(CultureInfo.InvariantCulture, "DXF issue: {0}", issue.BuildMessage());
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
        Logger.Debug(CultureInfo.InvariantCulture, "DXF overlap highlight + select applied (count={0}).", ids.Count);
    }

    private void ClearDxfOverlapHighlights()
    {
        _selectionCoordinator.ClearSelection(CanvasHost);
        DxfOverlapResult = DxfOverlapResult with { HighlightedCadIds = new ObservableCollection<int>() };
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

    private void SetDxfOverlapEmptyResult(string summary)
    {
        DxfOverlapResult = new DxfOverlapResult(
            summary,
            new ObservableCollection<string>(),
            new ObservableCollection<int>(),
            Array.Empty<int>());
    }

    partial void OnDxfOverlapResultChanged(DxfOverlapResult? oldValue, DxfOverlapResult newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.Summary, newValue.Summary, nameof(DxfOverlapSummary));
        RaiseWhenChanged(oldValue.Issues, newValue.Issues, nameof(DxfOverlapIssues));
        RaiseWhenChanged(oldValue.Issues.Count, newValue.Issues.Count, nameof(DxfOverlapIssueCount));
        RaiseWhenChanged(oldValue.Issues.Count > 0, newValue.Issues.Count > 0, nameof(HasDxfOverlapIssues));
        RaiseWhenChanged(oldValue.HighlightedCadIds, newValue.HighlightedCadIds, nameof(DxfOverlapHighlightedCadIds));
        RaiseWhenChanged(
            oldValue.HighlightedCadIds.Count > 0,
            newValue.HighlightedCadIds.Count > 0,
            nameof(HasDxfOverlapHighlights));
    }
}

