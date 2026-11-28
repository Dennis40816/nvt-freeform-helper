// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Simulation;

/// <summary>Owns source inputs and accepted results. UI thread only after view attachment.</summary>
internal sealed class SimulationSourceOwner<TSnapshot, TDataset>
    where TSnapshot : class
    where TDataset : class
{
    internal readonly record struct Seed(
        SimulationInputSourceOption SelectedInputSourceOption,
        NotchApplySimulationVersionOption SelectedVersionOption,
        double GlobalValue);

    private readonly Func<IReadOnlyList<string>, (bool Success, TDataset Dataset, string Diagnostic)> _importCsv;
    private readonly Func<NotchAlgorithmVersion, TDataset, int, TSnapshot> _buildCsv;
    private readonly Func<NotchAlgorithmVersion, double, IReadOnlyDictionary<int, double>, TSnapshot> _buildManual;
    private readonly Func<NotchAlgorithmVersion, TSnapshot> _buildCopper;
    private readonly Action<TSnapshot> _accept;
    private readonly TDataset _emptyDataset;
    private readonly Func<TDataset, bool> _hasCsvData;
    private readonly Func<TDataset, IEnumerable<NotchApplySimulationSourceFileItemViewModel>> _projectFiles;
    private readonly Func<TDataset, IEnumerable<NotchApplySimulationFrameOption>> _projectFrames;
    private readonly IReadOnlyList<RegularPad> _regularPads;
    private readonly Func<int> _readFrameIndex;
    private readonly Action _stopPlayback;
    private readonly Action _reconcileFrames;
    private readonly Action _refreshCommands;
    private readonly Action _refreshSelection;
    private readonly Func<bool> _hasSelectedRegular;
    private readonly Func<int> _readSelectedPadId;
    private readonly Func<double> _readSelectedValue;
    private readonly Action<double> _setSelectedValue;
    private readonly Func<RegularPad, bool> _isVisiblePad;
    private readonly Func<NotchApplySimulationCanvasViewMode> _readViewMode;
    private readonly Func<string> _readAreaSummary;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly Dictionary<int, double> _manualOverridesByRegularPadId = new();
    private readonly Dictionary<SimulationSnapshotCacheKey, TSnapshot> _csvSnapshotCache = new();
    private readonly ObservableCollection<NotchApplySimulationSourceFileItemViewModel> _csvFiles = new();
    private readonly ObservableCollection<NotchApplySimulationFrameOption> _frameOptions = new();
    private IReadOnlyList<string> _csvSourcePaths = Array.Empty<string>();
    private TDataset _csvDataset;
    private SimulationInputSourceOption _selectedInputSourceOption;
    private NotchApplySimulationVersionOption _selectedVersionOption;
    private double _globalValue;

    private readonly record struct SimulationSnapshotCacheKey(NotchAlgorithmVersion Version, int FrameIndex);

    internal SimulationSourceOwner(
        Seed seed, IReadOnlyList<SimulationInputSourceOption> sourceOptions,
        IReadOnlyList<NotchApplySimulationVersionOption> versionOptions, IReadOnlyList<RegularPad> regularPads,
        TDataset emptyDataset, Func<TDataset, bool> hasCsvData,
        Func<TDataset, IEnumerable<NotchApplySimulationSourceFileItemViewModel>> projectFiles,
        Func<TDataset, IEnumerable<NotchApplySimulationFrameOption>> projectFrames,
        Func<IReadOnlyList<string>, (bool Success, TDataset Dataset, string Diagnostic)> importCsv,
        Func<NotchAlgorithmVersion, TDataset, int, TSnapshot> buildCsv,
        Func<NotchAlgorithmVersion, double, IReadOnlyDictionary<int, double>, TSnapshot> buildManual,
        Func<NotchAlgorithmVersion, TSnapshot> buildCopper, Action<TSnapshot> accept,
        Func<int> readFrameIndex, Action stopPlayback, Action reconcileFrames,
        Action refreshCommands, Action refreshSelection,
        Func<bool> hasSelectedRegular, Func<int> readSelectedPadId,
        Func<double> readSelectedValue, Action<double> setSelectedValue,
        Func<RegularPad, bool> isVisiblePad, Func<NotchApplySimulationCanvasViewMode> readViewMode,
        Func<string> readAreaSummary, Action<string> setStatus,
        Action<string> propertyChanging, Action<string> propertyChanged)
    {
        _selectedInputSourceOption = seed.SelectedInputSourceOption;
        _selectedVersionOption = seed.SelectedVersionOption;
        _globalValue = seed.GlobalValue;
        SourceOptions = sourceOptions;
        VersionOptions = versionOptions;
        _regularPads = regularPads;
        _emptyDataset = emptyDataset;
        _csvDataset = emptyDataset;
        _hasCsvData = hasCsvData;
        _projectFiles = projectFiles;
        _projectFrames = projectFrames;
        _importCsv = importCsv;
        _buildCsv = buildCsv;
        _buildManual = buildManual;
        _buildCopper = buildCopper;
        _accept = accept;
        _readFrameIndex = readFrameIndex;
        _stopPlayback = stopPlayback;
        _reconcileFrames = reconcileFrames;
        _refreshCommands = refreshCommands;
        _refreshSelection = refreshSelection;
        _hasSelectedRegular = hasSelectedRegular;
        _readSelectedPadId = readSelectedPadId;
        _readSelectedValue = readSelectedValue;
        _setSelectedValue = setSelectedValue;
        _isVisiblePad = isVisiblePad;
        _readViewMode = readViewMode;
        _readAreaSummary = readAreaSummary;
        _setStatus = setStatus;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        CsvFiles = new ReadOnlyObservableCollection<NotchApplySimulationSourceFileItemViewModel>(_csvFiles);
        FrameOptions = new ReadOnlyObservableCollection<NotchApplySimulationFrameOption>(_frameOptions);
        ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync, () => CanImportCsv);
        ClearCsvCommand = new RelayCommand(ClearCsv, () => HasCsvFiles);
        RandomizeVisibleValuesCommand = new RelayCommand(RandomizeVisibleValues, () => IsManualSource && CanRandomizeVisibleValues);
        ApplyManualPresetCommand = new RelayCommand<string?>(ApplyManualPreset, _ => IsManualSource);
        ApplySelectedOverrideCommand = new RelayCommand(ApplySelectedOverride, _hasSelectedRegular);
        ClearSelectedOverrideCommand = new RelayCommand(ClearSelectedOverride, () => HasSelectedOverride);
        ClearAllManualOverridesCommand = new RelayCommand(ClearAllManualOverrides, () => HasManualOverrides);
    }

    public TSnapshot? Current { get; private set; }
    public TDataset Dataset => _csvDataset;
    public IReadOnlyDictionary<int, double> ManualOverrides => _manualOverridesByRegularPadId;
    public IReadOnlyList<SimulationInputSourceOption> SourceOptions { get; }
    public IReadOnlyList<NotchApplySimulationVersionOption> VersionOptions { get; }
    public ReadOnlyObservableCollection<NotchApplySimulationSourceFileItemViewModel> CsvFiles { get; }
    public ReadOnlyObservableCollection<NotchApplySimulationFrameOption> FrameOptions { get; }
    public Func<Task<IReadOnlyList<string>>>? PickOpenDiffCsvPathsAsync { get; set; }
    public IAsyncRelayCommand ImportCsvCommand { get; }
    public IRelayCommand ClearCsvCommand { get; }
    public IRelayCommand RandomizeVisibleValuesCommand { get; }
    public IRelayCommand<string?> ApplyManualPresetCommand { get; }
    public IRelayCommand ApplySelectedOverrideCommand { get; }
    public IRelayCommand ClearSelectedOverrideCommand { get; }
    public IRelayCommand ClearAllManualOverridesCommand { get; }
    public bool CanImportCsv => PickOpenDiffCsvPathsAsync is not null;
    public bool HasCsvFiles => CsvFiles.Count > 0;
    public bool HasCsvData => _hasCsvData(_csvDataset);
    public bool IsCsvSource => SelectedInputSourceOption.Value == SimulationInputSourceMode.Csv;
    public bool IsCopperSource => SelectedInputSourceOption.Value == SimulationInputSourceMode.Copper;
    public bool IsManualSource => SelectedInputSourceOption.Value == SimulationInputSourceMode.Manual;
    public bool CanRandomizeVisibleValues => _regularPads.Any(_isVisiblePad);
    public bool HasManualOverrides => _manualOverridesByRegularPadId.Count > 0;
    public bool HasSelectedOverride => _hasSelectedRegular() && _manualOverridesByRegularPadId.ContainsKey(_readSelectedPadId());

    public SimulationInputSourceOption SelectedInputSourceOption
    {
        get => _selectedInputSourceOption;
        set => SetValue(ref _selectedInputSourceOption, value, nameof(SelectedInputSourceOption), OnSelectedInputSourceOptionChanged);
    }

    public NotchApplySimulationVersionOption SelectedVersionOption
    {
        get => _selectedVersionOption;
        set => SetValue(ref _selectedVersionOption, value, nameof(SelectedVersionOption), OnSelectedVersionOptionChanged);
    }

    public double GlobalValue
    {
        get => _globalValue;
        set => SetValue(ref _globalValue, value, nameof(GlobalValue), OnGlobalValueChanged);
    }

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

        var sourcePaths = _csvSourcePaths
            .Concat(picked)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var (success, dataset, diagnostic) = _importCsv(sourcePaths);
        if (!success)
        {
            _setStatus($"CSV import failed: {diagnostic}");
            return;
        }

        _csvSourcePaths = sourcePaths;
        _csvDataset = dataset;
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
        _stopPlayback();
        _csvSourcePaths = Array.Empty<string>();
        _csvDataset = _emptyDataset;
        _csvSnapshotCache.Clear();
        _csvFiles.Clear();
        RefreshFrameOptions();
        RefreshSnapshot();
    }

    public void RefreshSnapshot()
    {
        TSnapshot snapshot;
        if (IsCsvSource && HasCsvData)
        {
            var sliderIndex = Math.Clamp(_readFrameIndex(), 0, Math.Max(FrameOptions.Count - 1, 0));
            var frameIndex = FrameOptions.Count > 0 ? FrameOptions[sliderIndex].Value : sliderIndex;
            var cacheKey = new SimulationSnapshotCacheKey(SelectedVersionOption.Value, frameIndex);
            if (!_csvSnapshotCache.TryGetValue(cacheKey, out snapshot!))
            {
                snapshot = _buildCsv(SelectedVersionOption.Value, _csvDataset, frameIndex);
                _csvSnapshotCache[cacheKey] = snapshot;
            }
        }
        else if (IsCopperSource)
        {
            snapshot = _buildCopper(SelectedVersionOption.Value);
        }
        else
        {
            snapshot = _buildManual(SelectedVersionOption.Value, GlobalValue, _manualOverridesByRegularPadId);
        }

        Current = snapshot;
        _accept(snapshot);
    }

    private void RebuildCsvFiles()
    {
        _csvFiles.Clear();
        foreach (var file in _projectFiles(_csvDataset))
        {
            _csvFiles.Add(file);
        }
    }

    public void RefreshFrameOptions()
    {
        _frameOptions.Clear();
        foreach (var frame in _projectFrames(_csvDataset))
        {
            _frameOptions.Add(frame);
        }

        _reconcileFrames();
    }

    private void OnSelectedInputSourceOptionChanged()
    {
        if (!IsCsvSource)
        {
            _stopPlayback();
        }

        RefreshSnapshot();
    }

    private void OnSelectedVersionOptionChanged()
    {
        RefreshSnapshot();
        _propertyChanged(nameof(SimulationWorkspaceViewModel.VersionSummaryText));
    }

    private void OnGlobalValueChanged()
    {
        if (IsManualSource)
        {
            RefreshSnapshot();
        }
        else
        {
            _refreshCommands();
        }
    }

    private void ApplySelectedOverride()
    {
        if (!_hasSelectedRegular())
        {
            return;
        }

        _manualOverridesByRegularPadId[_readSelectedPadId()] = _readSelectedValue();
        if (IsManualSource || !HasCsvData)
        {
            RefreshSnapshot();
            return;
        }

        _refreshSelection();
        _refreshCommands();
    }

    private void ClearSelectedOverride()
    {
        if (!_hasSelectedRegular())
        {
            return;
        }

        _manualOverridesByRegularPadId.Remove(_readSelectedPadId());
        _setSelectedValue(GlobalValue);
        if (IsManualSource || !HasCsvData)
        {
            RefreshSnapshot();
            return;
        }

        _refreshSelection();
        _refreshCommands();
    }

    private void ClearAllManualOverrides()
    {
        if (_manualOverridesByRegularPadId.Count == 0)
        {
            return;
        }

        _manualOverridesByRegularPadId.Clear();
        if (_hasSelectedRegular())
        {
            _setSelectedValue(GlobalValue);
        }

        RefreshSnapshot();
    }

    private void RandomizeVisibleValues()
    {
        var visiblePads = _regularPads
            .Where(_isVisiblePad)
            .ToList();
        if (visiblePads.Count == 0)
        {
            return;
        }

        _stopPlayback();
        foreach (var pad in visiblePads)
        {
            var nextValue = _readViewMode() is NotchApplySimulationCanvasViewMode.Delta or NotchApplySimulationCanvasViewMode.ChangedOnly
                ? Random.Shared.Next(-120, 121)
                : Random.Shared.Next(-40, 401);
            _manualOverridesByRegularPadId[pad.RegularPadId] = nextValue;
        }

        if (_hasSelectedRegular() && _manualOverridesByRegularPadId.TryGetValue(_readSelectedPadId(), out var overrideValue))
        {
            _setSelectedValue(overrideValue);
        }

        RefreshSnapshot();
        _setStatus($"Randomized {visiblePads.Count} regular pad(s) in {_readAreaSummary()}.");
    }

    private void ApplyManualPreset(string? valueText)
    {
        if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        _stopPlayback();
        var hadOverrides = _manualOverridesByRegularPadId.Count > 0;
        var globalChanged = Math.Abs(GlobalValue - value) > 1e-9;
        _manualOverridesByRegularPadId.Clear();

        if (_hasSelectedRegular())
        {
            _setSelectedValue(value);
        }

        GlobalValue = value;
        if (IsManualSource && !globalChanged && hadOverrides)
        {
            RefreshSnapshot();
        }
        else
        {
            _refreshCommands();
        }

        _setStatus($"Manual preset applied: uniform {SimulationWorkspaceViewModel.FormatWholeNumberDisplay(value)}.");
    }

    private void SetValue<T>(ref T field, T value, string name, Action changed)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(name);
        field = value;
        changed();
        _propertyChanged(name);
    }
}
