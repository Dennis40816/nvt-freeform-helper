// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using SimulationTextExportRequest = FreeformHelper.UI.ViewModels.SimulationWorkspaceViewModel.SimulationTextExportRequest;

namespace FreeformHelper.UI.ViewModels.Simulation;

/// <summary>Owns copper inputs and replay history. UI thread only after view attachment.</summary>
internal sealed class SimulationCopperOwner
{
    internal readonly record struct Seed(
        SimulationContactModelOption SelectedCopperContactModelOption,
        double CopperCenterX,
        double CopperCenterY,
        double CopperDiameter,
        double CopperPeakValue,
        double CopperBaselineValue,
        decimal CopperPathStartX,
        decimal CopperPathStartY,
        decimal CopperPathEndX,
        decimal CopperPathEndY,
        decimal CopperPathStepCount,
        CopperPillarPathReplayArtifactRow? SelectedCopperPathReplayRow);

    private readonly Func<bool> _isCopperSource;
    private readonly Action _selectCopperSource;
    private readonly Func<NotchAlgorithmVersion> _readVersion;
    private readonly Func<NotchAlgorithmVersion, CopperPillarPathReplayRequest, CopperPillarPathReplayResult> _replay;
    private readonly Func<CopperPillarPathReplayResult, string> _buildReplaySummary;
    private readonly Action _refreshSnapshot;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly ObservableCollection<CopperPillarPathReplayArtifactRow> _copperPathReplayRows = new();
    private bool _isUpdatingCopperCenter;
    private CopperPillarPathReplayResult _copperPathReplayResult = CopperPillarPathReplayResult.Empty;
    private CopperPillarPathReplayArtifactSnapshot _copperPathReplayArtifactSnapshot = CopperPillarPathReplayArtifactSnapshot.Empty;
    private SimulationContactModelOption _selectedCopperContactModelOption;
    private double _copperCenterX;
    private double _copperCenterY;
    private double _copperDiameter;
    private double _copperPeakValue;
    private double _copperBaselineValue;
    private decimal _copperPathStartX;
    private decimal _copperPathStartY;
    private decimal _copperPathEndX;
    private decimal _copperPathEndY;
    private decimal _copperPathStepCount;
    private CopperPillarPathReplayArtifactRow? _selectedCopperPathReplayRow;

    internal SimulationCopperOwner(
        Seed seed, IReadOnlyList<SimulationContactModelOption> contactOptions,
        Func<bool> isCopperSource, Action selectCopperSource, Func<NotchAlgorithmVersion> readVersion,
        Func<NotchAlgorithmVersion, CopperPillarPathReplayRequest, CopperPillarPathReplayResult> replay,
        Func<CopperPillarPathReplayResult, string> buildReplaySummary,
        Action refreshSnapshot, Action<string> setStatus,
        Action<string> propertyChanging, Action<string> propertyChanged)
    {
        _selectedCopperContactModelOption = seed.SelectedCopperContactModelOption;
        _copperCenterX = seed.CopperCenterX;
        _copperCenterY = seed.CopperCenterY;
        _copperDiameter = seed.CopperDiameter;
        _copperPeakValue = seed.CopperPeakValue;
        _copperBaselineValue = seed.CopperBaselineValue;
        _copperPathStartX = seed.CopperPathStartX;
        _copperPathStartY = seed.CopperPathStartY;
        _copperPathEndX = seed.CopperPathEndX;
        _copperPathEndY = seed.CopperPathEndY;
        _copperPathStepCount = seed.CopperPathStepCount;
        _selectedCopperPathReplayRow = seed.SelectedCopperPathReplayRow;
        CopperContactModelOptions = contactOptions;
        _isCopperSource = isCopperSource;
        _selectCopperSource = selectCopperSource;
        _readVersion = readVersion;
        _replay = replay;
        _buildReplaySummary = buildReplaySummary;
        _refreshSnapshot = refreshSnapshot;
        _setStatus = setStatus;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        CopperPathReplayRows = new ReadOnlyObservableCollection<CopperPillarPathReplayArtifactRow>(_copperPathReplayRows);
        ReplayCopperPathCommand = new RelayCommand(ReplayConfiguredCopperPath, _isCopperSource);
        UseCurrentCopperAsPathStartCommand = new RelayCommand(
            () => CaptureCopperPathEndpoint(useStart: true),
            _isCopperSource);
        UseCurrentCopperAsPathEndCommand = new RelayCommand(
            () => CaptureCopperPathEndpoint(useStart: false),
            _isCopperSource);
        SelectCopperPathReplayRowCommand =
            new RelayCommand<CopperPillarPathReplayArtifactRow?>(SelectCopperPathReplayRow);
        CopyCopperPathReplayCommand = new AsyncRelayCommand(CopyCopperPathReplayAsync);
        ExportCopperPathReplayCsvCommand = new AsyncRelayCommand(ExportCopperPathReplayCsvAsync);
        ExportCopperPathReplayJsonCommand = new AsyncRelayCommand(ExportCopperPathReplayJsonAsync);
    }

    public IRelayCommand ReplayCopperPathCommand { get; }
    public IRelayCommand UseCurrentCopperAsPathStartCommand { get; }
    public IRelayCommand UseCurrentCopperAsPathEndCommand { get; }
    public IRelayCommand<CopperPillarPathReplayArtifactRow?> SelectCopperPathReplayRowCommand { get; }
    public IAsyncRelayCommand CopyCopperPathReplayCommand { get; }
    public IAsyncRelayCommand ExportCopperPathReplayCsvCommand { get; }
    public IAsyncRelayCommand ExportCopperPathReplayJsonCommand { get; }
    public IReadOnlyList<SimulationContactModelOption> CopperContactModelOptions { get; }
    public ReadOnlyObservableCollection<CopperPillarPathReplayArtifactRow> CopperPathReplayRows { get; }
    public Func<string, Task<bool>>? RequestSetClipboardTextAsync { get; set; }
    public Func<SimulationTextExportRequest, Task<bool>>? RequestSaveTextFileAsync { get; set; }
    private bool IsCopperSource => _isCopperSource();

    public SimulationContactModelOption SelectedCopperContactModelOption
    {
        get => _selectedCopperContactModelOption;
        set => SetValue(ref _selectedCopperContactModelOption, value, nameof(SelectedCopperContactModelOption), OnSelectedCopperContactModelOptionChanged);
    }

    public double CopperCenterX
    {
        get => _copperCenterX;
        set => SetValue(ref _copperCenterX, value, nameof(CopperCenterX), _ => RefreshCopperSnapshotIfActive());
    }

    public double CopperCenterY
    {
        get => _copperCenterY;
        set => SetValue(ref _copperCenterY, value, nameof(CopperCenterY), _ => RefreshCopperSnapshotIfActive());
    }

    public double CopperDiameter
    {
        get => _copperDiameter;
        set => SetValue(ref _copperDiameter, value, nameof(CopperDiameter), OnCopperDiameterChanged);
    }

    public double CopperPeakValue
    {
        get => _copperPeakValue;
        set => SetValue(ref _copperPeakValue, value, nameof(CopperPeakValue), _ => OnCopperPeakValueChanged());
    }

    public double CopperBaselineValue
    {
        get => _copperBaselineValue;
        set => SetValue(ref _copperBaselineValue, value, nameof(CopperBaselineValue), _ => RefreshCopperSnapshotIfActive());
    }

    public decimal CopperPathStartX
    {
        get => _copperPathStartX;
        set => SetValue(ref _copperPathStartX, value, nameof(CopperPathStartX));
    }

    public decimal CopperPathStartY
    {
        get => _copperPathStartY;
        set => SetValue(ref _copperPathStartY, value, nameof(CopperPathStartY));
    }

    public decimal CopperPathEndX
    {
        get => _copperPathEndX;
        set => SetValue(ref _copperPathEndX, value, nameof(CopperPathEndX));
    }

    public decimal CopperPathEndY
    {
        get => _copperPathEndY;
        set => SetValue(ref _copperPathEndY, value, nameof(CopperPathEndY));
    }

    public decimal CopperPathStepCount
    {
        get => _copperPathStepCount;
        set => SetValue(ref _copperPathStepCount, value, nameof(CopperPathStepCount));
    }

    public CopperPillarPathReplayArtifactRow? SelectedCopperPathReplayRow
    {
        get => _selectedCopperPathReplayRow;
        set => SetValue(ref _selectedCopperPathReplayRow, value, nameof(SelectedCopperPathReplayRow));
    }

    public string CopperPositionText =>
        $"X {CopperCenterX.ToString("0.###", CultureInfo.InvariantCulture)} · Y {CopperCenterY.ToString("0.###", CultureInfo.InvariantCulture)} · Ø {CopperDiameter.ToString("0.###", CultureInfo.InvariantCulture)}";
    public string CopperSummaryText =>
        $"{SelectedCopperContactModelOption.Display} · {CopperPositionText}";
    public string CopperContactModelSummaryText =>
        $"{SelectedCopperContactModelOption.Description} Peak {SimulationWorkspaceViewModel.FormatWholeNumberDisplay(CopperPeakValue)}.";
    public string CopperProjectionModelText =>
        $"Input path: {SelectedCopperContactModelOption.Display} CAD overlap -> CAD Output FW Diff -> regular/FW Before -> notch After.";
    public string CopperInstructionText =>
        IsCopperSource
            ? "Move the mouse over the AA canvas to update signal in real time. Copper mode runs EMS, net-flow, and geometry audit through the same simulation gate."
            : "Select Copper to drive Before by movable CAD overlap area.";
    public bool HasCopperPathReplay => _copperPathReplayResult.HasSteps;
    public string CopperPathReplaySummaryText => _buildReplaySummary(_copperPathReplayResult);
    public string CopperPathReplayArtifactSummaryText => HasCopperPathReplay
        ? $"Replay rows {_copperPathReplayArtifactSnapshot.StepCount.ToString(CultureInfo.InvariantCulture)} · EMS {_copperPathReplayArtifactSnapshot.TotalEmsViolationCount.ToString(CultureInfo.InvariantCulture)} · net-flow {_copperPathReplayArtifactSnapshot.TotalNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)} · coverage {_copperPathReplayArtifactSnapshot.TotalTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)}"
        : "Replay rows: not run.";

    private void OnSelectedCopperContactModelOptionChanged(SimulationContactModelOption value)
    {
        CopperPeakValue = value.PeakValue;
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperContactModelSummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperSummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.InputPrimarySummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.SelectedFrameDisplayText));
    }

    private void OnCopperDiameterChanged(double value)
    {
        if (value < 0d)
        {
            CopperDiameter = 0d;
            return;
        }

        RefreshCopperSnapshotIfActive();
    }

    private void OnCopperPeakValueChanged()
    {
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperContactModelSummaryText));
        RefreshCopperSnapshotIfActive();
    }

    private void RefreshCopperSnapshotIfActive()
    {
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperPositionText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperSummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperProjectionModelText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperContactModelSummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.InputPrimarySummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.InputSecondarySummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.SelectedFrameDisplayText));

        if (!IsCopperSource || _isUpdatingCopperCenter)
        {
            return;
        }

        _refreshSnapshot();
    }

    public void MoveCopperToWorldPoint(Point2 worldPoint)
    {
        if (!IsCopperSource)
        {
            return;
        }

        if (Math.Abs(CopperCenterX - worldPoint.X) <= 1e-6 &&
            Math.Abs(CopperCenterY - worldPoint.Y) <= 1e-6)
        {
            return;
        }

        _isUpdatingCopperCenter = true;
        CopperCenterX = worldPoint.X;
        CopperCenterY = worldPoint.Y;
        _isUpdatingCopperCenter = false;
        RefreshCopperSnapshotIfActive();
    }

    public CopperPillarPathReplayResult ReplayCopperPath(Point2 start, Point2 end, int stepCount)
    {
        var result = _replay(
            _readVersion(),
            new CopperPillarPathReplayRequest(
                start,
                end,
                stepCount,
                CopperDiameter,
                CopperPeakValue,
                CopperBaselineValue));
        _copperPathReplayResult = result;
        _copperPathReplayArtifactSnapshot = CopperPillarPathReplayArtifactService.BuildSnapshot(result);
        RebuildCopperPathReplayRows();
        _setStatus(_buildReplaySummary(result));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.HasCopperPathReplay));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperPathReplaySummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperPathReplayArtifactSummaryText));
        return result;
    }

    private void ReplayConfiguredCopperPath()
    {
        if (!IsCopperSource)
        {
            _selectCopperSource();
        }

        ReplayCopperPath(
            new Point2((double)CopperPathStartX, (double)CopperPathStartY),
            new Point2((double)CopperPathEndX, (double)CopperPathEndY),
            ClampReplayStepCount(CopperPathStepCount));
        SelectedCopperPathReplayRow = CopperPathReplayRows.Count > 0 ? CopperPathReplayRows[0] : null;
    }

    private void CaptureCopperPathEndpoint(bool useStart)
    {
        if (useStart)
        {
            CopperPathStartX = (decimal)CopperCenterX;
            CopperPathStartY = (decimal)CopperCenterY;
            _setStatus("Copper path start captured from current center.");
            return;
        }

        CopperPathEndX = (decimal)CopperCenterX;
        CopperPathEndY = (decimal)CopperCenterY;
        _setStatus("Copper path end captured from current center.");
    }

    private async Task CopyCopperPathReplayAsync()
    {
        if (RequestSetClipboardTextAsync is null)
        {
            _setStatus("Copper path replay clipboard copy is unavailable in the current host.");
            return;
        }

        if (CopperPathReplayRows.Count == 0)
        {
            _setStatus("Run copper path replay before copying rows.");
            return;
        }

        var copied = await RequestSetClipboardTextAsync(
            CopperPillarPathReplayArtifactService.ExportClipboardTable(CopperPathReplayRows));
        _setStatus(copied
            ? $"Copied {CopperPathReplayRows.Count.ToString(CultureInfo.InvariantCulture)} copper path replay row(s)."
            : "Copper path replay clipboard copy failed.");
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
            _setStatus("Copper path replay text export is unavailable in the current host.");
            return;
        }

        if (CopperPathReplayRows.Count == 0)
        {
            _setStatus($"Run copper path replay before exporting {fileTypeName}.");
            return;
        }

        var exported = await RequestSaveTextFileAsync(new SimulationTextExportRequest(
            title,
            suggestedFileName,
            defaultExtension,
            fileTypeName,
            patterns,
            content));
        _setStatus(exported
            ? $"Exported {fileTypeName} copper path replay row(s): {CopperPathReplayRows.Count.ToString(CultureInfo.InvariantCulture)}."
            : $"Copper path replay {fileTypeName} export cancelled.");
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
            _selectCopperSource();
        }

        MoveCopperToWorldPoint(new Point2(row.CenterX, row.CenterY));
        _setStatus($"Focused copper replay {row.StepText} at {row.CenterText}.");
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

        _propertyChanged(nameof(SimulationWorkspaceViewModel.CopperPathReplayRows));
    }

    private static int ClampReplayStepCount(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    public CopperPillarSimulationRequest CaptureRequest() =>
        new(CopperCenterX, CopperCenterY, CopperDiameter, CopperPeakValue, CopperBaselineValue);

    private void SetValue<T>(ref T field, T value, string name, Action<T>? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(name);
        field = value;
        changed?.Invoke(value);
        _propertyChanged(name);
    }
}
