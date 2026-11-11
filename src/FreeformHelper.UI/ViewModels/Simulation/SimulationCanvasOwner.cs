// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns display choices and the accepted overlay projection. UI thread only.</summary>
internal sealed class SimulationCanvasOwner<TProjection> where TProjection : class
{
    private const double SignificantDeltaEpsilon = 1e-9;
    private const double DefaultThresholdValue = 5d;
    private readonly Func<IReadOnlyList<NotchApplySimulationDiffCell>, int?, NotchApplySimulationCanvasViewMode, TProjection> _project;
    private readonly Func<TProjection, IReadOnlyDictionary<int, NotchApplySimulationDiffCell>> _readCells;
    private readonly Func<TProjection, IReadOnlyList<NotchApplySimulationAaDisplayCell>> _readOverlay;
    private readonly Func<TProjection, (double Minimum, double Maximum, double MaximumAbsDelta, double AutoNegativeClampAbs, double AutoPositiveClamp)> _readRange;
    private readonly Func<NotchApplySimulationResult?> _readResult;
    private readonly Action _ensureSelectionWithinArea;
    private readonly Action _refreshViolations;
    private readonly Action _refreshSelection;
    private readonly Action _refreshCommands;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private TProjection _projection;
    private NotchApplySimulationCanvasViewOption _selectedCanvasViewOption;
    private NotchApplySimulationCanvasColorOption _selectedCanvasColorOption;
    private NotchApplySimulationAreaOption _selectedAreaOption;
    private double _colorThresholdValue;

    internal SimulationCanvasOwner(
        IReadOnlyList<NotchApplySimulationCanvasViewOption> viewOptions,
        IReadOnlyList<NotchApplySimulationCanvasColorOption> colorOptions,
        IReadOnlyList<NotchApplySimulationAreaOption> areaOptions, TProjection initialProjection,
        Func<IReadOnlyList<NotchApplySimulationDiffCell>, int?, NotchApplySimulationCanvasViewMode, TProjection> project,
        Func<TProjection, IReadOnlyDictionary<int, NotchApplySimulationDiffCell>> cells,
        Func<TProjection, IReadOnlyList<NotchApplySimulationAaDisplayCell>> overlay,
        Func<TProjection, (double Minimum, double Maximum, double MaximumAbsDelta, double AutoNegativeClampAbs, double AutoPositiveClamp)> range,
        Func<NotchApplySimulationResult?> readResult,
        Action ensureSelectionWithinArea, Action refreshViolations, Action refreshSelection, Action refreshCommands,
        Action<string> propertyChanging, Action<string> propertyChanged)
    {
        CanvasViewOptions = viewOptions;
        CanvasColorOptions = colorOptions;
        AreaOptions = areaOptions;
        _selectedCanvasViewOption = viewOptions[0];
        _selectedCanvasColorOption = colorOptions[0];
        _selectedAreaOption = areaOptions[0];
        _colorThresholdValue = DefaultThresholdValue;
        _projection = initialProjection;
        _project = project;
        _readCells = cells;
        _readOverlay = overlay;
        _readRange = range;
        _readResult = readResult;
        _ensureSelectionWithinArea = ensureSelectionWithinArea;
        _refreshViolations = refreshViolations;
        _refreshSelection = refreshSelection;
        _refreshCommands = refreshCommands;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
    }

    public IReadOnlyList<NotchApplySimulationCanvasViewOption> CanvasViewOptions { get; }
    public IReadOnlyList<NotchApplySimulationCanvasColorOption> CanvasColorOptions { get; }
    public IReadOnlyList<NotchApplySimulationAreaOption> AreaOptions { get; }
    public IReadOnlyDictionary<int, NotchApplySimulationDiffCell> CellsByRegularPadId => _readCells(_projection);
    private NotchApplySimulationResult? Result => _readResult();
    private (double Minimum, double Maximum, double MaximumAbsDelta, double AutoNegativeClampAbs, double AutoPositiveClamp) LegendRange => _readRange(_projection);

    public NotchApplySimulationCanvasViewOption SelectedCanvasViewOption
    {
        get => _selectedCanvasViewOption;
        set => SetValue(ref _selectedCanvasViewOption, value, nameof(SelectedCanvasViewOption), OnSelectedCanvasViewOptionChanged);
    }

    public NotchApplySimulationCanvasColorOption SelectedCanvasColorOption
    {
        get => _selectedCanvasColorOption;
        set => SetValue(ref _selectedCanvasColorOption, value, nameof(SelectedCanvasColorOption), OnSelectedCanvasColorOptionChanged);
    }

    public NotchApplySimulationAreaOption SelectedAreaOption
    {
        get => _selectedAreaOption;
        set => SetValue(ref _selectedAreaOption, value, nameof(SelectedAreaOption), OnSelectedAreaOptionChanged);
    }

    public double ColorThresholdValue
    {
        get => _colorThresholdValue;
        set => SetValue(ref _colorThresholdValue, value, nameof(ColorThresholdValue), () => OnColorThresholdValueChanged(value));
    }

    public IReadOnlyList<NotchApplySimulationAaDisplayCell> SimulationOverlayItems => _readOverlay(_projection);

    public NotchApplySimulationCanvasViewMode SelectedCanvasViewMode => SelectedCanvasViewOption.Value;

    public NotchApplySimulationCanvasColorMode SelectedCanvasColorMode => SelectedCanvasColorOption.Value;

    public bool IsThresholdMode => SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Threshold;

    public bool IsSignedCanvasView => SelectedCanvasViewMode is NotchApplySimulationCanvasViewMode.Delta or NotchApplySimulationCanvasViewMode.ChangedOnly;

    public bool UsesSignedColorScale => IsSignedCanvasView || LegendRange.Minimum < -SignificantDeltaEpsilon;

    public string AreaSummaryText => SelectedAreaOption.Display;

    public string ViewModeSummaryText => $"View: {SelectedCanvasViewOption.Display}";

    public string ColorModeSummaryText => SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Threshold
        ? $"Color: TH scale ({ColorThresholdValue.ToString("0.###", CultureInfo.InvariantCulture)})"
        : "Color: AUTO scale";

    public bool HasSimulationData => SimulationOverlayItems.Count > 0;

    public string ColorLegendTitleText => IsThresholdMode ? "TH color scale" : "AUTO color scale";

    public string ColorLegendMeaningText => UsesSignedColorScale
        ? SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Auto
            ? "AUTO percentile-clamped scale: ordinary values stay green, strongly negative values trend blue, and stronger positive values move through yellow/orange to red based on the visible distribution."
            : "TH signed scale: negative values trend blue, values near 0 trend green, and positive values move through yellow/orange to red, using TH as the scale reference."
        : SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Auto
            ? "AUTO percentile-clamped scale: regular values stay green at baseline, then move through yellow/orange to red as they rise within the visible distribution."
            : "TH mode: lower diff trends blue, near-threshold values trend green/yellow, and higher diff trends orange/red, using TH as the color scale reference.";

    public string ColorLegendStartText => UsesSignedColorScale
        ? FormatLegendValue(-GetLegendSignedRange())
        : IsThresholdMode
            ? "0"
            : FormatLegendValue(LegendRange.Minimum);

    public string ColorLegendCenterText => UsesSignedColorScale
        ? "0"
        : string.Empty;

    public string ColorLegendEndText => UsesSignedColorScale
        ? FormatSignedLegendValue(GetLegendSignedRange())
        : IsThresholdMode
            ? $"TH {ColorThresholdValue.ToString("0.###", CultureInfo.InvariantCulture)}"
            : FormatLegendValue(LegendRange.Maximum);

    public bool ShowLegendCenterLabel => UsesSignedColorScale;

    public bool ShowSequentialLegend => !ShowLegendCenterLabel;

    public string CanvasHintText => HasSimulationData
        ? string.Empty
        : $"No projected regular pads under {SelectedAreaOption.Display}.";

    public bool HasCanvasHint => !HasSimulationData;

    public void SetCanvasViewMode(NotchApplySimulationCanvasViewMode mode)
    {
        var option = CanvasViewOptions.FirstOrDefault(candidate => candidate.Value == mode);
        if (!option.Equals(default(NotchApplySimulationCanvasViewOption)))
        {
            SelectedCanvasViewOption = option;
        }
    }

    public void ToggleBeforeAfterCanvasView()
    {
        SetCanvasViewMode(SelectedCanvasViewMode == NotchApplySimulationCanvasViewMode.Before
            ? NotchApplySimulationCanvasViewMode.After
            : NotchApplySimulationCanvasViewMode.Before);
    }

    public string ColorScaleCaptionText => SelectedCanvasViewMode switch
    {
        NotchApplySimulationCanvasViewMode.Before => "Before",
        NotchApplySimulationCanvasViewMode.After => "After",
        _ => "Delta (After - Before)",
    };

    public void Rebuild(IReadOnlyList<NotchApplySimulationDiffCell> cells)
    {
        _projection = _project(cells, SelectedAreaOption.IcIndex, SelectedCanvasViewOption.Value);

        _ensureSelectionWithinArea();
        _propertyChanged(nameof(SimulationOverlayItems));
        _propertyChanged(nameof(HasSimulationData));
        _propertyChanged(nameof(CanvasHintText));
        _propertyChanged(nameof(HasCanvasHint));
        _propertyChanged(nameof(ColorLegendTitleText));
        _propertyChanged(nameof(ColorLegendMeaningText));
        _propertyChanged(nameof(ColorLegendStartText));
        _propertyChanged(nameof(ColorLegendCenterText));
        _propertyChanged(nameof(ColorLegendEndText));
        _propertyChanged(nameof(ShowLegendCenterLabel));
        _propertyChanged(nameof(ShowSequentialLegend));
    }

    private void OnSelectedCanvasViewOptionChanged()
    {
        _propertyChanged(nameof(SelectedCanvasViewMode));
        _propertyChanged(nameof(ViewModeSummaryText));
        _propertyChanged(nameof(IsSignedCanvasView));
        if (Result is not null)
        {
            Rebuild(Result.Cells);
        }
    }

    private void OnSelectedCanvasColorOptionChanged()
    {
        _propertyChanged(nameof(SelectedCanvasColorMode));
        _propertyChanged(nameof(ColorModeSummaryText));
        _refreshCommands();
    }

    private void OnSelectedAreaOptionChanged()
    {
        _propertyChanged(nameof(AreaSummaryText));
        _propertyChanged(nameof(SimulationWorkspaceViewModel.CanvasStatusSummaryText));
        if (Result is not null)
        {
            Rebuild(Result.Cells);
            _refreshViolations();
        }

        _refreshSelection();
        _refreshCommands();
    }

    private void OnColorThresholdValueChanged(double value)
    {
        if (value < 0d)
        {
            ColorThresholdValue = 0d;
            return;
        }

        _propertyChanged(nameof(ColorModeSummaryText));
        _propertyChanged(nameof(ColorLegendEndText));
        _propertyChanged(nameof(ColorLegendStartText));
    }

    private double GetLegendSignedRange()
    {
        if (IsThresholdMode)
        {
            return Math.Max(0d, ColorThresholdValue);
        }

        if (SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Auto)
        {
            return Math.Max(LegendRange.AutoNegativeClampAbs, LegendRange.AutoPositiveClamp);
        }

        return IsSignedCanvasView
            ? LegendRange.MaximumAbsDelta
            : Math.Max(Math.Abs(LegendRange.Minimum), Math.Abs(LegendRange.Maximum));
    }

    private static string FormatLegendValue(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatSignedLegendValue(double value)
    {
        return value.ToString("+0.###;-0.###;0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SetValue<T>(ref T field, T value, string name, Action? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(name);
        field = value;
        changed?.Invoke();
        _propertyChanged(name);
    }
}
