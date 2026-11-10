// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.UI.ViewModels.Coordinate;

/// <summary>Owns artifact selection, sorting, and host actions. UI thread only.</summary>
internal sealed class CoordinateArtifactOwner
{
    internal readonly record struct Seed(
        CoordinateArtifactRow? SelectedArtifactRow,
        string ArtifactSortKey,
        bool ArtifactSortDescending);

    private readonly CoordinatePlannerHostCallbacks _hostCallbacks = new();
    private readonly Func<CoordinatePlannerRenderState> _readRender;
    private readonly Action<CoordinateArtifactRow[]> _replaceRows;
    private readonly Action<string> _setStatus;
    private readonly Func<Func<Task>, Task> _runDetail;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private CoordinateArtifactRow? _selectedArtifactRow;
    private string _artifactSortKey;
    private bool _artifactSortDescending;

    internal CoordinateArtifactOwner(
        Seed seed,
        Func<CoordinatePlannerRenderState> readRender,
        Action<CoordinateArtifactRow[]> replaceRows,
        Action<string> setStatus,
        Func<Func<Task>, Task> runDetail,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _readRender = readRender;
        _replaceRows = replaceRows;
        _setStatus = setStatus;
        _runDetail = runDetail;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _selectedArtifactRow = seed.SelectedArtifactRow;
        _artifactSortKey = seed.ArtifactSortKey;
        _artifactSortDescending = seed.ArtifactSortDescending;
        ExportPreviewPngCommand = new AsyncRelayCommand(ExportPreviewPngAsync);
        CopySelectedArtifactCommand = new AsyncRelayCommand(CopySelectedArtifactAsync);
        CopyAllArtifactsCommand = new AsyncRelayCommand(CopyAllArtifactsAsync);
        ExportArtifactsCsvCommand = new AsyncRelayCommand(ExportArtifactsCsvAsync);
        ExportArtifactsJsonCommand = new AsyncRelayCommand(ExportArtifactsJsonAsync);
        SortArtifactRowsCommand = new RelayCommand<string?>(SortArtifactRows);
        ShowArtifactDetailCommand = new AsyncRelayCommand<CoordinateArtifactRow?>(ShowArtifactDetailAsync);
    }

    public IAsyncRelayCommand ExportPreviewPngCommand { get; }
    public IAsyncRelayCommand CopySelectedArtifactCommand { get; }
    public IAsyncRelayCommand CopyAllArtifactsCommand { get; }
    public IAsyncRelayCommand ExportArtifactsCsvCommand { get; }
    public IAsyncRelayCommand ExportArtifactsJsonCommand { get; }
    public IRelayCommand<string?> SortArtifactRowsCommand { get; }
    public IAsyncRelayCommand<CoordinateArtifactRow?> ShowArtifactDetailCommand { get; }
    public IReadOnlyList<CoordinateArtifactRow> ArtifactRows => _readRender().ArtifactRows;
    public bool HasSelectedArtifact => SelectedArtifactRow is not null;

    public Func<CoordinateArtifactRow, Task>? RequestArtifactDetailAsync
    {
        get => _hostCallbacks.RequestArtifactDetailAsync;
        set => _hostCallbacks.RequestArtifactDetailAsync = value;
    }

    public Func<Task<bool>>? RequestExportPreviewPngAsync
    {
        get => _hostCallbacks.RequestExportPreviewPngAsync;
        set => _hostCallbacks.RequestExportPreviewPngAsync = value;
    }

    public Func<string, Task<bool>>? RequestSetClipboardTextAsync
    {
        get => _hostCallbacks.RequestSetClipboardTextAsync;
        set => _hostCallbacks.RequestSetClipboardTextAsync = value;
    }

    public Func<CoordinatePlannerTextExportRequest, Task<bool>>? RequestSaveTextFileAsync
    {
        get => _hostCallbacks.RequestSaveTextFileAsync;
        set => _hostCallbacks.RequestSaveTextFileAsync = value;
    }

    public CoordinateArtifactRow? SelectedArtifactRow
    {
        get => _selectedArtifactRow;
        set => SetValue(ref _selectedArtifactRow, value, nameof(SelectedArtifactRow), () => _propertyChanged(nameof(HasSelectedArtifact)));
    }

    public string ArtifactSortKey
    {
        get => _artifactSortKey;
        set => SetValue(ref _artifactSortKey, value, nameof(ArtifactSortKey));
    }

    public bool ArtifactSortDescending
    {
        get => _artifactSortDescending;
        set => SetValue(ref _artifactSortDescending, value, nameof(ArtifactSortDescending));
    }

    public string ArtifactSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0} coordinate row(s)",
            ArtifactRows.Count);

    public string ArtifactSortStatusText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Sort: {0} {1}",
            GetArtifactSortDisplay(ArtifactSortKey),
            ArtifactSortDescending ? "desc" : "asc");

    public void RestoreSelection(string? key)
    {
        SelectedArtifactRow = ArtifactRows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.Ordinal));
    }

    public void SelectByKey(string key)
    {
        var row = ArtifactRows.FirstOrDefault(artifact =>
            string.Equals(artifact.Key, key, StringComparison.Ordinal));
        if (row is not null)
        {
            SelectedArtifactRow = row;
        }
    }

    private async Task ExportPreviewPngAsync()
    {
        if (RequestExportPreviewPngAsync is null)
        {
            _setStatus("Coordinate image export is unavailable in the current host.");
            return;
        }

        var exported = await RequestExportPreviewPngAsync();
        _setStatus(exported
            ? "Coordinate preview exported."
            : "Coordinate preview export cancelled.");
    }

    private async Task CopySelectedArtifactAsync()
    {
        if (SelectedArtifactRow is null)
        {
            _setStatus("Select a coordinate row before copying a selected artifact.");
            return;
        }

        await CopyArtifactRowsAsync([SelectedArtifactRow], selectedOnly: true);
    }

    private Task CopyAllArtifactsAsync()
    {
        return CopyArtifactRowsAsync(ArtifactRows.ToArray(), selectedOnly: false);
    }

    private async Task CopyArtifactRowsAsync(
        CoordinateArtifactRow[] rows,
        bool selectedOnly)
    {
        if (RequestSetClipboardTextAsync is null)
        {
            _setStatus("Coordinate clipboard copy is unavailable in the current host.");
            return;
        }

        if (rows.Length == 0)
        {
            _setStatus("No coordinate rows are available to copy.");
            return;
        }

        var copied = await RequestSetClipboardTextAsync(CoordinateArtifactService.ExportClipboardTable(rows));
        _setStatus(copied
            ? BuildExportStatus("Copied", rows.Length, selectedOnly)
            : "Coordinate clipboard copy failed.");
    }

    private Task ExportArtifactsCsvAsync()
    {
        var rows = ArtifactRows.ToArray();
        return SaveArtifactsTextAsync(
            "Export Coordinate Artifacts CSV",
            "coordinate_artifacts.csv",
            "csv",
            "CSV",
            ["*.csv"],
            CoordinateArtifactService.ExportCsv(rows),
            rows.Length);
    }

    private Task ExportArtifactsJsonAsync()
    {
        var rows = ArtifactRows.ToArray();
        return SaveArtifactsTextAsync(
            "Export Coordinate Artifacts JSON",
            "coordinate_artifacts.json",
            "json",
            "JSON",
            ["*.json"],
            CoordinateArtifactService.ExportJson(_readRender().ArtifactSnapshot with { Rows = rows }),
            rows.Length);
    }

    private async Task SaveArtifactsTextAsync(
        string title,
        string suggestedFileName,
        string defaultExtension,
        string fileTypeName,
        IReadOnlyList<string> patterns,
        string content,
        int rowCount)
    {
        if (RequestSaveTextFileAsync is null)
        {
            _setStatus("Coordinate text export is unavailable in the current host.");
            return;
        }

        if (rowCount == 0)
        {
            _setStatus("No coordinate rows are available to export.");
            return;
        }

        var request = new CoordinatePlannerTextExportRequest(
            title,
            suggestedFileName,
            defaultExtension,
            fileTypeName,
            patterns,
            content);
        var exported = await RequestSaveTextFileAsync(request);
        _setStatus(exported
            ? BuildExportStatus($"Exported {fileTypeName}", rowCount, selectedOnly: false)
            : $"Coordinate {fileTypeName} export cancelled.");
    }

    private Task ShowArtifactDetailAsync(CoordinateArtifactRow? row)
    {
        if (row is null)
        {
            return Task.CompletedTask;
        }

        SelectedArtifactRow = row;
        var request = RequestArtifactDetailAsync;
        if (request is null) return Task.CompletedTask;
        return _runDetail(() => request(row));
    }

    private void SortArtifactRows(string? key)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? "Default" : key.Trim();
        if (string.Equals(ArtifactSortKey, normalizedKey, StringComparison.Ordinal))
        {
            ArtifactSortDescending = !ArtifactSortDescending;
        }
        else
        {
            ArtifactSortKey = normalizedKey;
            ArtifactSortDescending = false;
        }

        _replaceRows(SortRows(_readRender().ArtifactRows));
        _propertyChanged(nameof(ArtifactRows));
        _propertyChanged(nameof(ArtifactSortStatusText));
    }

    public CoordinateArtifactRow[] SortRows(
        IEnumerable<CoordinateArtifactRow> rows)
    {
        var sortedRows = ArtifactSortKey switch
        {
            "Kind" => rows.OrderBy(static row => row.Kind, StringComparer.Ordinal)
                .ThenBy(static row => row.Recipe, StringComparer.Ordinal)
                .ThenBy(static row => row.Label, StringComparer.Ordinal),
            "Label" => rows.OrderBy(static row => row.Label, StringComparer.Ordinal),
            "Pixel" => rows.OrderBy(static row => row.SortPixelX)
                .ThenBy(static row => row.SortPixelY),
            "Machine" => rows.OrderBy(static row => row.SortMachineX)
                .ThenBy(static row => row.SortMachineY),
            "Safe" => rows.OrderBy(static row => row.SortSafeMachineX)
                .ThenBy(static row => row.SortSafeMachineY),
            _ => rows.OrderBy(static row => row.SortOrder),
        };

        return (ArtifactSortDescending ? sortedRows.Reverse() : sortedRows).ToArray();
    }

    private static string GetArtifactSortDisplay(string sortKey) =>
        sortKey switch
        {
            "Kind" => "Kind",
            "Label" => "Label",
            "Pixel" => "Pixel",
            "Machine" => "Machine",
            "Safe" => "Safe",
            _ => "Source",
        };

    private static string BuildExportStatus(string verb, int rowCount, bool selectedOnly)
    {
        var scope = selectedOnly ? "selected coordinate row" : "coordinate row(s)";
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}.", verb, rowCount, scope);
    }

    private void SetValue<T>(ref T field, T value, string name, Action? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        _propertyChanging(name);
        field = value;
        changed?.Invoke();
        _propertyChanged(name);
    }
}
