using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.CadImport;
using NLog;

namespace FreeformHelper.UI.Services.CadImport;

/// <summary>Runs overlap checks and selection effects against the accepted CAD session.</summary>
internal sealed class DxfQualityWorkflow(
    CadDocumentSession document, IDxfQualityPresentation state, DxfQualityEffects effects, Logger logger) : IDxfQualityWorkflow
{
    public async Task CheckAsync()
    {
        var isLoaded = document.Cad is not null;
        var pads = effects.VisiblePads().ToList();
        if (!isLoaded)
        {
            SetEmptyResult("DXF check: no DXF loaded.");
            effects.SetStatus(state.DxfOverlapResult.Summary);
            logger.Debug(CultureInfo.InvariantCulture, "DXF overlap check skipped: no DXF loaded.");
            return;
        }
        if (pads.Count == 0)
        {
            SetEmptyResult("DXF check: no CAD output pads (all layers off).");
            effects.SetStatus(state.DxfOverlapResult.Summary);
            logger.Debug(CultureInfo.InvariantCulture, "DXF overlap check skipped: no CAD output pads.");
            return;
        }
        await effects.RunProgress("DXF overlap check", busy => state.IsCheckingDxfOverlap = busy,
            progress => state.DxfOverlapProgress = progress,
            report => CheckOverlapAsync(pads, report), ReportError, () => ReportStart(pads.Count));
    }

    private void ReportStart(int count)
    {
        state.DxfOverlapResult = state.DxfOverlapResult with { Summary = "DXF check: running..." };
        effects.SetStatus(state.DxfOverlapResult.Summary);
        logger.Info(CultureInfo.InvariantCulture, "DXF overlap check started (visible pads={0}).", count);
    }

    private void ReportError(Exception ex)
    {
        SetEmptyResult("DXF check failed.");
        effects.SetError("DXF overlap check failed", ex);
        logger.Error(ex, "DXF overlap check failed.");
    }

    private async Task CheckOverlapAsync(List<CadPad> pads, Action<double> reportProgress)
    {
        var cad = new CadPadSet(pads);
        var report = await Task.Run(() => DxfOverlapAnalyzer.Analyze(cad, maxSamples: 300, reportProgress: reportProgress));
        var ids = report.SampleIssues.SelectMany(issue => new[] { issue.PadAId, issue.PadBId })
            .Distinct().OrderBy(id => id).ToList();
        // Keep the intermediate result and stale-completion acceptance characterized before this move.
        state.DxfOverlapResult = new DxfOverlapResult(state.DxfOverlapResult.Summary,
            new ObservableCollection<string>(report.SampleIssues.Select(issue => issue.BuildMessage())),
            new ObservableCollection<int>(ids), ids);
        PublishSummary(report);
    }

    private void PublishSummary(DxfOverlapReport report)
    {
        if (!report.HasIssues)
        {
            state.DxfOverlapResult = state.DxfOverlapResult with { Summary = "DXF check: no overlaps detected." };
            effects.SetStatus(state.DxfOverlapResult.Summary);
            logger.Info(CultureInfo.InvariantCulture, "DXF check: no overlaps detected (pads={0}).", report.PadCount);
            return;
        }
        state.DxfOverlapResult = state.DxfOverlapResult with { Summary = $"DXF warnings: {report.Summary}." };
        effects.SetStatus(state.DxfOverlapResult.Summary);
        logger.Warn(CultureInfo.InvariantCulture, "DXF quality warnings: {0}", report.Summary);
        foreach (var issue in report.SampleIssues)
            logger.Warn(CultureInfo.InvariantCulture, "DXF issue: {0}", issue.BuildMessage());
    }

    public void SelectOverlapPads()
    {
        var ids = state.DxfOverlapResult.HighlightedCadIds.Count > 0
            ? state.DxfOverlapResult.HighlightedCadIds.ToList() : state.DxfOverlapResult.LastCadIds.ToList();
        if (ids.Count == 0)
        {
            effects.SetStatus("DXF check: no overlap result.");
            return;
        }
        if (state.DxfOverlapResult.HighlightedCadIds.Count == 0)
            state.DxfOverlapResult = state.DxfOverlapResult with { HighlightedCadIds = new ObservableCollection<int>(ids) };
        effects.SelectPads(ids);
        effects.SetStatus($"DXF check: highlight + select ({ids.Count} pads).");
        logger.Debug(CultureInfo.InvariantCulture, "DXF overlap highlight + select applied (count={0}).", ids.Count);
    }

    public void ClearHighlights()
    {
        effects.ClearSelection();
        state.DxfOverlapResult = state.DxfOverlapResult with { HighlightedCadIds = new ObservableCollection<int>() };
        effects.SetStatus("DXF check: highlight cleared.");
        logger.Debug(CultureInfo.InvariantCulture, "DXF overlap highlight + selection cleared.");
    }

    public async Task ShowDetailsAsync()
    {
        if (!state.HasDetailsHandler)
        {
            effects.SetStatus("DXF overlap details: view handler not wired.");
            logger.Warn(CultureInfo.InvariantCulture, "DXF overlap details requested but view handler is not wired.");
            return;
        }
        var lines = state.DxfOverlapResult.Issues.ToList();
        if (lines.Count == 0) lines = new List<string> { "No overlap issues." };
        logger.Debug(CultureInfo.InvariantCulture, "DXF overlap details opened (issues={0}).", lines.Count);
        await state.OpenDetailsAsync(state.DxfOverlapResult.Summary, lines);
    }

    private void SetEmptyResult(string summary) => state.DxfOverlapResult = DxfOverlapResult.CreateNotRun() with { Summary = summary };
}
