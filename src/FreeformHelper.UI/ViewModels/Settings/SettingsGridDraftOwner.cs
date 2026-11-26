// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels.Settings;

/// <summary>Owns the settings grid draft and its synchronous transitions. UI thread only.</summary>
internal sealed class SettingsGridDraftOwner
{
    private readonly Func<object?> _readDraftProjectIdentity;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly Func<int, int, int, IEnumerable<int>, IEnumerable<int>, CascadeDraftLayout> _buildCascadeLayout;
    private readonly Action<string> _markEdited;

    private decimal _xChannels;
    private decimal _yChannels;
    private decimal _cascadeNum;
    private decimal _coordinatePixelWidth;
    private decimal _coordinatePixelHeight;
    private decimal _gridPaddingPercent;
    private decimal _activeAreaWidth;
    private decimal _activeAreaHeight;
    private decimal _panelBiasX;
    private decimal _panelBiasY;
    private ScanOrderOption _selectedScanOrderOption;
    private RegularSourceModeOption _selectedRegularSourceModeOption;
    private RegularSourceLayerOption _selectedRegularSourceLayerOption;
    private string _dxfRegularSourceHint;
    private GridAlignmentOption _selectedGridAlignmentOption;
    private ObservableCollection<SettingsWindowViewModel.CascadeIcDraftRow> _cascadeIcSettings;

    internal readonly record struct Seed(
        decimal XChannels,
        decimal YChannels,
        decimal CascadeNum,
        decimal CoordinatePixelWidth,
        decimal CoordinatePixelHeight,
        decimal GridPaddingPercent,
        decimal ActiveAreaWidth,
        decimal ActiveAreaHeight,
        decimal PanelBiasX,
        decimal PanelBiasY,
        ScanOrderOption SelectedScanOrderOption,
        RegularSourceModeOption SelectedRegularSourceModeOption,
        RegularSourceLayerOption SelectedRegularSourceLayerOption,
        string DxfRegularSourceHint,
        GridAlignmentOption SelectedGridAlignmentOption,
        ObservableCollection<SettingsWindowViewModel.CascadeIcDraftRow> CascadeIcSettings);

    public SettingsGridDraftOwner(
        Seed seed,
        Func<object?> readDraftProjectIdentity,
        ActiveAreaPixelsDraft activeAreaPixels,
        IEnumerable<int> perIcXChannels,
        IEnumerable<int> perIcYChannels,
        Func<int, int, int, IEnumerable<int>, IEnumerable<int>, CascadeDraftLayout> buildCascadeLayout,
        Action<string> markEdited,
        IReadOnlyList<ScanOrderOption> scanOrderOptions,
        IReadOnlyList<GridAlignmentOption> gridAlignmentOptions,
        IReadOnlyList<RegularSourceModeOption> regularSourceModeOptions,
        IReadOnlyList<RegularSourceLayerOption> regularSourceLayerOptions,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _readDraftProjectIdentity = readDraftProjectIdentity;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        ActiveAreaPixels = activeAreaPixels;
        _buildCascadeLayout = buildCascadeLayout;
        _markEdited = markEdited;
        ScanOrderOptions = scanOrderOptions;
        GridAlignmentOptions = gridAlignmentOptions;
        RegularSourceModeOptions = regularSourceModeOptions;
        RegularSourceLayerOptions = regularSourceLayerOptions;

        _xChannels = seed.XChannels;
        _yChannels = seed.YChannels;
        _cascadeNum = seed.CascadeNum;
        _coordinatePixelWidth = seed.CoordinatePixelWidth;
        _coordinatePixelHeight = seed.CoordinatePixelHeight;
        _gridPaddingPercent = seed.GridPaddingPercent;
        _activeAreaWidth = seed.ActiveAreaWidth;
        _activeAreaHeight = seed.ActiveAreaHeight;
        _panelBiasX = seed.PanelBiasX;
        _panelBiasY = seed.PanelBiasY;
        _selectedScanOrderOption = seed.SelectedScanOrderOption;
        _selectedRegularSourceModeOption = seed.SelectedRegularSourceModeOption;
        _selectedRegularSourceLayerOption = seed.SelectedRegularSourceLayerOption;
        _dxfRegularSourceHint = seed.DxfRegularSourceHint;
        _selectedGridAlignmentOption = seed.SelectedGridAlignmentOption;
        _cascadeIcSettings = seed.CascadeIcSettings;

        InitializeCascadeIcSettings(perIcXChannels, perIcYChannels);
    }

    public object? ProjectIdentity => _readDraftProjectIdentity();

    public IReadOnlyList<ScanOrderOption> ScanOrderOptions { get; }
    public IReadOnlyList<GridAlignmentOption> GridAlignmentOptions { get; }
    public IReadOnlyList<RegularSourceModeOption> RegularSourceModeOptions { get; }
    public IReadOnlyList<RegularSourceLayerOption> RegularSourceLayerOptions { get; }

    public decimal XChannels
    {
        get => _xChannels;
        set => SetValue(ref _xChannels, value, nameof(XChannels), _ => _propertyChanged(nameof(CascadeLayoutSummary)));
    }

    public decimal YChannels
    {
        get => _yChannels;
        set => SetValue(ref _yChannels, value, nameof(YChannels), _ => _propertyChanged(nameof(CascadeLayoutSummary)));
    }

    public decimal CascadeNum
    {
        get => _cascadeNum;
        set => SetValue(ref _cascadeNum, value, nameof(CascadeNum), OnCascadeNumChanged);
    }

    public decimal CoordinatePixelWidth
    {
        get => _coordinatePixelWidth;
        set => SetValue(ref _coordinatePixelWidth, value, nameof(CoordinatePixelWidth), OnCoordinatePixelWidthChanged);
    }

    public decimal CoordinatePixelHeight
    {
        get => _coordinatePixelHeight;
        set => SetValue(ref _coordinatePixelHeight, value, nameof(CoordinatePixelHeight), OnCoordinatePixelHeightChanged);
    }

    public decimal GridPaddingPercent
    {
        get => _gridPaddingPercent;
        set => SetValue(ref _gridPaddingPercent, value, nameof(GridPaddingPercent));
    }

    public decimal ActiveAreaWidth
    {
        get => _activeAreaWidth;
        set => SetValue(ref _activeAreaWidth, value, nameof(ActiveAreaWidth), OnActiveAreaWidthChanged);
    }

    public decimal ActiveAreaHeight
    {
        get => _activeAreaHeight;
        set => SetValue(ref _activeAreaHeight, value, nameof(ActiveAreaHeight), OnActiveAreaHeightChanged);
    }

    public decimal PanelBiasX
    {
        get => _panelBiasX;
        set => SetValue(ref _panelBiasX, value, nameof(PanelBiasX));
    }

    public decimal PanelBiasY
    {
        get => _panelBiasY;
        set => SetValue(ref _panelBiasY, value, nameof(PanelBiasY));
    }

    public ScanOrderOption SelectedScanOrderOption
    {
        get => _selectedScanOrderOption;
        set => SetValue(ref _selectedScanOrderOption, value, nameof(SelectedScanOrderOption));
    }

    public RegularSourceModeOption SelectedRegularSourceModeOption
    {
        get => _selectedRegularSourceModeOption;
        set => SetValue(ref _selectedRegularSourceModeOption, value, nameof(SelectedRegularSourceModeOption), _ => _propertyChanged(nameof(IsDxfLayerRegularSource)));
    }

    public RegularSourceLayerOption SelectedRegularSourceLayerOption
    {
        get => _selectedRegularSourceLayerOption;
        set => SetValue(ref _selectedRegularSourceLayerOption, value, nameof(SelectedRegularSourceLayerOption));
    }

    public string DxfRegularSourceHint
    {
        get => _dxfRegularSourceHint;
        set => SetValue(ref _dxfRegularSourceHint, value, nameof(DxfRegularSourceHint));
    }

    public GridAlignmentOption SelectedGridAlignmentOption
    {
        get => _selectedGridAlignmentOption;
        set => SetValue(ref _selectedGridAlignmentOption, value, nameof(SelectedGridAlignmentOption), _ => _propertyChanged(nameof(IsPanelAlignment)));
    }

    public ObservableCollection<SettingsWindowViewModel.CascadeIcDraftRow> CascadeIcSettings
    {
        get => _cascadeIcSettings;
        set => SetValue(ref _cascadeIcSettings, value, nameof(CascadeIcSettings));
    }

    public ActiveAreaPixelsDraft ActiveAreaPixels { get; }

    public bool IsPanelAlignment => SelectedGridAlignmentOption.Value == FreeformHelper.Application.Settings.GridAlignmentMode.FromPanelAa;

    public bool IsDxfLayerRegularSource => SelectedRegularSourceModeOption.Value == FreeformHelper.Application.Settings.RegularSourceMode.FromDxfLayer;

    public string CascadeLayoutSummary =>
        $"IC {CascadeIcSettings.Count} · X {XChannels:0} · Y {YChannels:0}";

    public IReadOnlyList<int> PerIcXChannels => GetCascadeLayout().PerIcXChannels;

    public IReadOnlyList<int> PerIcYChannels => GetCascadeLayout().PerIcYChannels;

    private void OnActiveAreaWidthChanged(decimal value)
    {
        ActiveAreaPixels.WidthMm = value;
    }

    private void OnActiveAreaHeightChanged(decimal value)
    {
        ActiveAreaPixels.HeightMm = value;
    }

    private void OnCoordinatePixelWidthChanged(decimal value)
    {
        var clamped = CoordinatePixelSizePolicy.Normalize(value);
        if (clamped != value)
        {
            CoordinatePixelWidth = clamped;
        }
    }

    private void OnCoordinatePixelHeightChanged(decimal value)
    {
        var clamped = CoordinatePixelSizePolicy.Normalize(value);
        if (clamped != value)
        {
            CoordinatePixelHeight = clamped;
        }
    }

    private void InitializeCascadeIcSettings(IEnumerable<int> perIcXChannels, IEnumerable<int> perIcYChannels)
    {
        var layout = _buildCascadeLayout(
            ClampPositiveInt(CascadeNum),
            ClampPositiveInt(XChannels),
            ClampPositiveInt(YChannels),
            perIcXChannels,
            perIcYChannels);

        ReplaceCascadeIcSettings(layout);
    }

    private CascadeDraftLayout GetCascadeLayout()
    {
        return _buildCascadeLayout(
            ClampPositiveInt(CascadeNum),
            ClampPositiveInt(XChannels),
            ClampPositiveInt(YChannels),
            CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.XChannels))),
            CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.YChannels))));
    }

    private void ReplaceCascadeIcSettings(CascadeDraftLayout layout)
    {
        DetachCascadeIcRows();

        var rows = new ObservableCollection<SettingsWindowViewModel.CascadeIcDraftRow>(
            layout.Rows.Select(static row =>
                new SettingsWindowViewModel.CascadeIcDraftRow(row.IcIndex, row.XChannels, row.YChannels)));
        foreach (var row in rows)
        {
            row.PropertyChanged += OnCascadeIcDraftRowChanged;
        }

        CascadeIcSettings = rows;
        RefreshCascadeTotalsFromRows();
    }

    private void DetachCascadeIcRows()
    {
        foreach (var row in CascadeIcSettings)
        {
            row.PropertyChanged -= OnCascadeIcDraftRowChanged;
        }
    }

    private void OnCascadeIcDraftRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SettingsWindowViewModel.CascadeIcDraftRow.XChannels) or nameof(SettingsWindowViewModel.CascadeIcDraftRow.YChannels)))
        {
            return;
        }

        _markEdited(nameof(CascadeIcSettings));
        RefreshCascadeTotalsFromRows();
    }

    private void RefreshCascadeTotalsFromRows()
    {
        var layout = GetCascadeLayout();
        XChannels = layout.TotalXChannels;
        YChannels = layout.TotalYChannels;
        _propertyChanged(nameof(CascadeLayoutSummary));
    }

    private void RefreshCascadeRowsForCurrentCount()
    {
        var layout = GetCascadeLayout();
        ReplaceCascadeIcSettings(layout);
    }

    private void OnCascadeNumChanged(decimal value)
    {
        var clamped = ClampPositiveInt(value);
        if (clamped != value)
        {
            CascadeNum = clamped;
            return;
        }

        RefreshCascadeRowsForCurrentCount();
    }

    internal static int ClampPositiveInt(decimal value)
    {
        return Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));
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

internal readonly record struct CascadeDraftLayout(
    IReadOnlyList<(int IcIndex, int XChannels, int YChannels)> Rows,
    int TotalXChannels,
    int TotalYChannels,
    IReadOnlyList<int> PerIcXChannels,
    IReadOnlyList<int> PerIcYChannels);
