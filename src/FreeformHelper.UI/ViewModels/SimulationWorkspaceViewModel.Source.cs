using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    public async Task ImportCsvAsync()
    {
        if (PickOpenDiffCsvPathsAsync is null)
        {
            return;
        }

        var picked = await PickOpenDiffCsvPathsAsync();
        if (picked.Count == 0)
        {
            return;
        }

        _csvSourcePaths = _csvSourcePaths
            .Concat(picked)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _csvDataset = _useCase.ImportCsvSources(_csvSourcePaths, _session.FwDiffGrid);
        _csvSnapshotCache.Clear();
        RebuildCsvFiles();
        RefreshFrameOptions();
        if (SelectedInputSourceOption.Value != SimulationInputSourceMode.Csv)
        {
            SelectedInputSourceOption = SourceOptions.First(option => option.Value == SimulationInputSourceMode.Csv);
        }
        else
        {
            RefreshSnapshot();
        }
    }

    private void ClearCsv()
    {
        StopPlayback();
        _csvSourcePaths = Array.Empty<string>();
        _csvDataset = NotchApplySimulationImportedDataset.Empty;
        _csvSnapshotCache.Clear();
        _csvFiles.Clear();
        RefreshFrameOptions();
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        SimulationScenarioSnapshot snapshot;
        if (IsCsvSource && HasCsvData)
        {
            var frameIndex = Math.Clamp(SelectedFrameSliderIndex, 0, Math.Max(FrameOptions.Count - 1, 0));
            var cacheKey = new SimulationSnapshotCacheKey(SelectedVersionOption.Value, frameIndex);
            if (!_csvSnapshotCache.TryGetValue(cacheKey, out snapshot!))
            {
                var scenario = SimulationWorkspaceUseCase.BuildCsvScenario(
                    _csvDataset,
                    SelectedVersionOption.Value,
                    NotchApplySimulationAggregationMode.SingleFrame,
                    frameIndex);
                snapshot = SimulationWorkspaceUseCase.BuildScenarioSnapshot(_session, scenario);
                _csvSnapshotCache[cacheKey] = snapshot;
            }
        }
        else if (IsCopperSource)
        {
            var scenario = SimulationWorkspaceUseCase.BuildCopperScenario(
                _session,
                SelectedVersionOption.Value,
                new CopperPillarSimulationRequest(
                    CopperCenterX,
                    CopperCenterY,
                    CopperDiameter,
                    CopperPeakValue,
                    CopperBaselineValue));
            snapshot = SimulationWorkspaceUseCase.BuildScenarioSnapshot(_session, scenario);
        }
        else
        {
            var scenario = SimulationWorkspaceUseCase.BuildManualScenario(
                _session,
                SelectedVersionOption.Value,
                GlobalValue,
                _manualOverridesByRegularPadId);
            snapshot = SimulationWorkspaceUseCase.BuildScenarioSnapshot(_session, scenario);
        }

        _snapshot = snapshot;
        _snapshotCellsByRegularPadId = snapshot.Result.Cells.ToDictionary(
            static cell => cell.RegularPadId,
            static cell => cell);
        RebuildImpactItemIndex(snapshot.Result.Actions);

        RebuildOverlayItems(snapshot.Result.Cells);
        RefreshDiffViolationState();
        RefreshSimulationSafetyState();

        UpdateStatus(snapshot);
        RefreshSelectionPresentation();
        RefreshCommandState();
    }

    internal SimulationScenarioSnapshot? GetSimulationScenarioSnapshot()
    {
        return _snapshot;
    }

    private void RebuildCsvFiles()
    {
        _csvFiles.Clear();
        foreach (var file in _csvDataset.Files)
        {
            _csvFiles.Add(new NotchApplySimulationSourceFileItemViewModel(
                file.FileName,
                file.SummaryText,
                file.ShapeText,
                file.Diagnostics.Count > 0));
        }
    }

    private void RefreshFrameOptions()
    {
        _frameOptions.Clear();
        foreach (var frame in _csvDataset.Frames.Where(static frame => frame.Projection.IsCompatible))
        {
            _frameOptions.Add(new NotchApplySimulationFrameOption(frame.GlobalIndex, frame.DisplayText));
        }

        _isSyncingFrameSelection = true;
        SelectedFrameSliderIndex = Math.Clamp(SelectedFrameSliderIndex, 0, Math.Max(FrameOptions.Count - 1, 0));
        _isSyncingFrameSelection = false;
        OnPropertyChanged(nameof(SelectedFrameSliderMaximum));
        OnPropertyChanged(nameof(FrameSummaryText));
    }

    private void UpdateStatus(SimulationScenarioSnapshot snapshot)
    {
        if (!snapshot.Result.IsSupported)
        {
            StatusText = snapshot.Result.PrimaryDiagnostic ?? "Simulation contract not supported under current version.";
            return;
        }

        var delta = snapshot.Result.DeltaTotal.ToString("+0.###;-0.###;0", System.Globalization.CultureInfo.InvariantCulture);
        StatusText = IsCopperSource
            ? $"{CopperProjectionModelText} · total Δ {delta}"
            : $"{snapshot.Result.ContractText} · frames {snapshot.Result.ConsumedFrameCount} · total Δ {delta}";
    }

    private static IReadOnlyList<NotchApplySimulationVersionOption> BuildVersionOptions(NotchTable table)
    {
        var versions = table.Rows
            .Select(static row => row.Version)
            .Distinct()
            .OrderBy(static version => (int)version)
            .Select(static version => new NotchApplySimulationVersionOption(version, version.ToDisplayLabel()))
            .ToList();

        return versions.Count > 0
            ? versions
            : new[] { new NotchApplySimulationVersionOption(NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V22.ToDisplayLabel()) };
    }

    private static List<NotchApplySimulationAreaOption> BuildAreaOptions(IReadOnlyList<RegularPad> pads)
    {
        var options = new List<NotchApplySimulationAreaOption>
        {
            new(null, "All regular pads"),
        };

        options.AddRange(pads
            .Select(static pad => pad.IcIndex)
            .Distinct()
            .OrderBy(static icIndex => icIndex)
            .Select(static icIndex => new NotchApplySimulationAreaOption(icIndex, $"IC {icIndex + 1}")));

        return options;
    }

    partial void OnSelectedInputSourceOptionChanged(SimulationInputSourceOption value)
    {
        if (!IsCsvSource)
        {
            StopPlayback();
        }

        RefreshSnapshot();
    }

    partial void OnSelectedCopperContactModelOptionChanged(SimulationContactModelOption value)
    {
        CopperPeakValue = value.PeakValue;
        OnPropertyChanged(nameof(CopperContactModelSummaryText));
        OnPropertyChanged(nameof(CopperSummaryText));
        OnPropertyChanged(nameof(InputPrimarySummaryText));
        OnPropertyChanged(nameof(SelectedFrameDisplayText));
    }

    partial void OnSelectedVersionOptionChanged(NotchApplySimulationVersionOption value)
    {
        RefreshSnapshot();
        OnPropertyChanged(nameof(VersionSummaryText));
    }

    partial void OnGlobalValueChanged(double value)
    {
        if (IsManualSource)
        {
            RefreshSnapshot();
        }
        else
        {
            RefreshCommandState();
        }
    }

    partial void OnCopperCenterXChanged(double value)
    {
        RefreshCopperSnapshotIfActive();
    }

    partial void OnCopperCenterYChanged(double value)
    {
        RefreshCopperSnapshotIfActive();
    }

    partial void OnCopperDiameterChanged(double value)
    {
        if (value < 0d)
        {
            CopperDiameter = 0d;
            return;
        }

        RefreshCopperSnapshotIfActive();
    }

    partial void OnCopperPeakValueChanged(double value)
    {
        OnPropertyChanged(nameof(CopperContactModelSummaryText));
        RefreshCopperSnapshotIfActive();
    }

    partial void OnCopperBaselineValueChanged(double value)
    {
        RefreshCopperSnapshotIfActive();
    }

    private void RefreshCopperSnapshotIfActive()
    {
        OnPropertyChanged(nameof(CopperPositionText));
        OnPropertyChanged(nameof(CopperSummaryText));
        OnPropertyChanged(nameof(CopperProjectionModelText));
        OnPropertyChanged(nameof(CopperContactModelSummaryText));
        OnPropertyChanged(nameof(InputSummaryText));
        OnPropertyChanged(nameof(InputPrimarySummaryText));
        OnPropertyChanged(nameof(InputSecondarySummaryText));
        OnPropertyChanged(nameof(SelectedFrameDisplayText));

        if (!IsCopperSource || _isUpdatingCopperCenter)
        {
            return;
        }

        RefreshSnapshot();
    }

    partial void OnSelectedFrameSliderIndexChanged(int value)
    {
        if (_isSyncingFrameSelection || !CanPlayFrames)
        {
            return;
        }

        var clamped = Math.Clamp(value, 0, Math.Max(FrameOptions.Count - 1, 0));
        if (clamped != value)
        {
            _isSyncingFrameSelection = true;
            SelectedFrameSliderIndex = clamped;
            _isSyncingFrameSelection = false;
            return;
        }

        RefreshSnapshot();
        OnPropertyChanged(nameof(FrameSummaryText));
        OnPropertyChanged(nameof(SelectedFrameDisplayText));
    }
}

