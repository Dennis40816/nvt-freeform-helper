using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    private void ReplayConfiguredCopperPath()
    {
        if (!IsCopperSource)
        {
            SelectedInputSourceOption = SourceOptions.First(option => option.Value == SimulationInputSourceMode.Copper);
        }

        ReplayCopperPath(
            new Point2((double)CopperPathStartX, (double)CopperPathStartY),
            new Point2((double)CopperPathEndX, (double)CopperPathEndY),
            ClampReplayStepCount(CopperPathStepCount));
        SelectedCopperPathReplayRow = CopperPathReplayRows.FirstOrDefault();
    }

    private void CaptureCopperPathEndpoint(bool useStart)
    {
        if (useStart)
        {
            CopperPathStartX = (decimal)CopperCenterX;
            CopperPathStartY = (decimal)CopperCenterY;
            StatusText = "Copper path start captured from current center.";
            return;
        }

        CopperPathEndX = (decimal)CopperCenterX;
        CopperPathEndY = (decimal)CopperCenterY;
        StatusText = "Copper path end captured from current center.";
    }

    private async Task CopyCopperPathReplayAsync()
    {
        if (RequestSetClipboardTextAsync is null)
        {
            StatusText = "Copper path replay clipboard copy is unavailable in the current host.";
            return;
        }

        if (CopperPathReplayRows.Count == 0)
        {
            StatusText = "Run copper path replay before copying rows.";
            return;
        }

        var copied = await RequestSetClipboardTextAsync(
            CopperPillarPathReplayArtifactService.ExportClipboardTable(CopperPathReplayRows));
        StatusText = copied
            ? $"Copied {CopperPathReplayRows.Count.ToString(CultureInfo.InvariantCulture)} copper path replay row(s)."
            : "Copper path replay clipboard copy failed.";
    }

    private Task ExportCopperPathReplayCsvAsync()
    {
        return SaveCopperPathReplayTextAsync(
            "Export Copper Path Replay CSV",
            "copper_path_replay.csv",
            "csv",
            "CSV",
            ["*.csv"],
            CopperPillarPathReplayArtifactService.ExportCsv(_copperPathReplayArtifactSnapshot));
    }

    private Task ExportCopperPathReplayJsonAsync()
    {
        return SaveCopperPathReplayTextAsync(
            "Export Copper Path Replay JSON",
            "copper_path_replay.json",
            "json",
            "JSON",
            ["*.json"],
            CopperPillarPathReplayArtifactService.ExportJson(_copperPathReplayArtifactSnapshot));
    }

    private async Task SaveCopperPathReplayTextAsync(
        string title,
        string suggestedFileName,
        string defaultExtension,
        string fileTypeName,
        IReadOnlyList<string> patterns,
        string content)
    {
        if (RequestSaveTextFileAsync is null)
        {
            StatusText = "Copper path replay text export is unavailable in the current host.";
            return;
        }

        if (CopperPathReplayRows.Count == 0)
        {
            StatusText = $"Run copper path replay before exporting {fileTypeName}.";
            return;
        }

        var exported = await RequestSaveTextFileAsync(new SimulationTextExportRequest(
            title,
            suggestedFileName,
            defaultExtension,
            fileTypeName,
            patterns,
            content));
        StatusText = exported
            ? $"Exported {fileTypeName} copper path replay row(s): {CopperPathReplayRows.Count.ToString(CultureInfo.InvariantCulture)}."
            : $"Copper path replay {fileTypeName} export cancelled.";
    }

    private void SelectCopperPathReplayRow(CopperPillarPathReplayArtifactRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedCopperPathReplayRow = row;
        if (!IsCopperSource)
        {
            SelectedInputSourceOption = SourceOptions.First(option => option.Value == SimulationInputSourceMode.Copper);
        }

        MoveCopperToWorldPoint(new Point2(row.CenterX, row.CenterY));
        StatusText = $"Focused copper replay {row.StepText} at {row.CenterText}.";
    }

    private void RebuildCopperPathReplayRows()
    {
        _copperPathReplayRows.Clear();
        foreach (var row in _copperPathReplayArtifactSnapshot.Rows)
        {
            _copperPathReplayRows.Add(row);
        }

        if (SelectedCopperPathReplayRow is not null &&
            !_copperPathReplayRows.Any(row => row.StepIndex == SelectedCopperPathReplayRow.StepIndex))
        {
            SelectedCopperPathReplayRow = null;
        }

        OnPropertyChanged(nameof(CopperPathReplayRows));
        OnPropertyChanged(nameof(HasSelectedCopperPathReplayRow));
    }

    private static int ClampReplayStepCount(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    partial void OnSelectedCopperPathReplayRowChanged(CopperPillarPathReplayArtifactRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedCopperPathReplayRow));
    }
}
