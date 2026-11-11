// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels.Settings;

/// <summary>Owns the settings general draft and its synchronous transitions. UI thread only.</summary>
internal sealed class SettingsGeneralDraftOwner
{
    private readonly Func<object?> _readDraftProjectIdentity;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly Func<IReadOnlyList<FreeformHelper.Application.CadImport.DxfLayerCategoryState>> _readCategoryStates;
    private readonly Action<FreeformHelper.Application.CadImport.DxfLayerCategory, bool> _applyCategory;

    private bool _importOnlyClosedPolylines;
    private bool _importBlockPolylines;
    private bool _recalcBoundsOnLayerFilter;
    private decimal _matchThreshold;
    private decimal _freeformAxisThreshold;
    private bool _enableAutoDetectXy;
    private bool _enableFreeformEdgeSpecialization;
    private bool _autoReplayStep2AfterProjectLoad;
    private bool _applyAppVisualPreferencesOnProjectLoad;
    private FreeformHelperViewModel.LogLevelOption _selectedLogLevelOption;
    private bool _useLocalSizing;
    private decimal _globalFontSizePercent;
    private decimal _highlightStrokeWidthAdjust;
    private ObservableCollection<SettingsWindowViewModel.LayerCategoryRow> _layerCategoryRows;

    internal readonly record struct Seed(
        bool ImportOnlyClosedPolylines,
        bool ImportBlockPolylines,
        bool RecalcBoundsOnLayerFilter,
        decimal MatchThreshold,
        decimal FreeformAxisThreshold,
        bool EnableAutoDetectXy,
        bool EnableFreeformEdgeSpecialization,
        bool AutoReplayStep2AfterProjectLoad,
        bool ApplyAppVisualPreferencesOnProjectLoad,
        FreeformHelperViewModel.LogLevelOption SelectedLogLevelOption,
        bool UseLocalSizing,
        decimal GlobalFontSizePercent,
        decimal HighlightStrokeWidthAdjust,
        ObservableCollection<SettingsWindowViewModel.LayerCategoryRow> LayerCategoryRows);

    public SettingsGeneralDraftOwner(
        Seed seed,
        Func<object?> readDraftProjectIdentity,
        Func<IReadOnlyList<FreeformHelper.Application.CadImport.DxfLayerCategoryState>> readCategoryStates,
        Action<FreeformHelper.Application.CadImport.DxfLayerCategory, bool> applyCategory,
        IReadOnlyList<FreeformHelperViewModel.LogLevelOption> logLevelOptions,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _readDraftProjectIdentity = readDraftProjectIdentity;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _readCategoryStates = readCategoryStates;
        _applyCategory = applyCategory;
        LogLevelOptions = logLevelOptions;

        _importOnlyClosedPolylines = seed.ImportOnlyClosedPolylines;
        _importBlockPolylines = seed.ImportBlockPolylines;
        _recalcBoundsOnLayerFilter = seed.RecalcBoundsOnLayerFilter;
        _matchThreshold = seed.MatchThreshold;
        _freeformAxisThreshold = seed.FreeformAxisThreshold;
        _enableAutoDetectXy = seed.EnableAutoDetectXy;
        _enableFreeformEdgeSpecialization = seed.EnableFreeformEdgeSpecialization;
        _autoReplayStep2AfterProjectLoad = seed.AutoReplayStep2AfterProjectLoad;
        _applyAppVisualPreferencesOnProjectLoad = seed.ApplyAppVisualPreferencesOnProjectLoad;
        _selectedLogLevelOption = seed.SelectedLogLevelOption;
        _useLocalSizing = seed.UseLocalSizing;
        _globalFontSizePercent = seed.GlobalFontSizePercent;
        _highlightStrokeWidthAdjust = seed.HighlightStrokeWidthAdjust;
        _layerCategoryRows = seed.LayerCategoryRows;
    }

    public object? ProjectIdentity => _readDraftProjectIdentity();

    public IReadOnlyList<FreeformHelperViewModel.LogLevelOption> LogLevelOptions { get; }

    public bool ImportOnlyClosedPolylines
    {
        get => _importOnlyClosedPolylines;
        set => SetValue(ref _importOnlyClosedPolylines, value, nameof(ImportOnlyClosedPolylines));
    }

    public bool ImportBlockPolylines
    {
        get => _importBlockPolylines;
        set => SetValue(ref _importBlockPolylines, value, nameof(ImportBlockPolylines));
    }

    public bool RecalcBoundsOnLayerFilter
    {
        get => _recalcBoundsOnLayerFilter;
        set => SetValue(ref _recalcBoundsOnLayerFilter, value, nameof(RecalcBoundsOnLayerFilter));
    }

    public decimal MatchThreshold
    {
        get => _matchThreshold;
        set => SetValue(ref _matchThreshold, value, nameof(MatchThreshold));
    }

    public decimal FreeformAxisThreshold
    {
        get => _freeformAxisThreshold;
        set => SetValue(ref _freeformAxisThreshold, value, nameof(FreeformAxisThreshold));
    }

    public bool EnableAutoDetectXy
    {
        get => _enableAutoDetectXy;
        set => SetValue(ref _enableAutoDetectXy, value, nameof(EnableAutoDetectXy));
    }

    public bool EnableFreeformEdgeSpecialization
    {
        get => _enableFreeformEdgeSpecialization;
        set => SetValue(ref _enableFreeformEdgeSpecialization, value, nameof(EnableFreeformEdgeSpecialization));
    }

    public bool AutoReplayStep2AfterProjectLoad
    {
        get => _autoReplayStep2AfterProjectLoad;
        set => SetValue(ref _autoReplayStep2AfterProjectLoad, value, nameof(AutoReplayStep2AfterProjectLoad));
    }

    public bool ApplyAppVisualPreferencesOnProjectLoad
    {
        get => _applyAppVisualPreferencesOnProjectLoad;
        set => SetValue(ref _applyAppVisualPreferencesOnProjectLoad, value, nameof(ApplyAppVisualPreferencesOnProjectLoad));
    }

    public FreeformHelperViewModel.LogLevelOption SelectedLogLevelOption
    {
        get => _selectedLogLevelOption;
        set => SetValue(ref _selectedLogLevelOption, value, nameof(SelectedLogLevelOption));
    }

    public bool UseLocalSizing
    {
        get => _useLocalSizing;
        set => SetValue(ref _useLocalSizing, value, nameof(UseLocalSizing));
    }

    public decimal GlobalFontSizePercent
    {
        get => _globalFontSizePercent;
        set => SetValue(ref _globalFontSizePercent, value, nameof(GlobalFontSizePercent));
    }

    public decimal HighlightStrokeWidthAdjust
    {
        get => _highlightStrokeWidthAdjust;
        set => SetValue(ref _highlightStrokeWidthAdjust, value, nameof(HighlightStrokeWidthAdjust), OnHighlightStrokeWidthAdjustChanged);
    }

    public ObservableCollection<SettingsWindowViewModel.LayerCategoryRow> LayerCategoryRows
    {
        get => _layerCategoryRows;
        set => SetValue(ref _layerCategoryRows, value, nameof(LayerCategoryRows));
    }

    public bool HasLayerCategoryRows => LayerCategoryRows.Count > 0;

    private void OnHighlightStrokeWidthAdjustChanged(decimal value)
    {
        var clamped = Math.Clamp(value, -2m, 4m);
        if (clamped != value)
        {
            HighlightStrokeWidthAdjust = clamped;
        }
    }

    public void RefreshLayerCategoryRows()
    {
        LayerCategoryRows = new ObservableCollection<SettingsWindowViewModel.LayerCategoryRow>(
            _readCategoryStates()
                .Select(state => new SettingsWindowViewModel.LayerCategoryRow(state, ApplyLayerCategorySelection)));
        _propertyChanged(nameof(HasLayerCategoryRows));
    }

    private void ApplyLayerCategorySelection(FreeformHelper.Application.CadImport.DxfLayerCategory category, bool isSelected)
    {
        _applyCategory(category, isSelected);
        RefreshLayerCategoryRows();
    }

    private void SetValue<T>(ref T field, T value, string propertyName, Action<T>? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(propertyName);
        field = value;
        changed?.Invoke(value);
        _propertyChanged(propertyName);
    }
}
