using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // --- Canvas Toggles (Directly bound to UI for visual state) ---

    // The canvas display switches and the import flags are stored once. The properties below forward to
    // the record and raise the same notifications and callbacks as the stored properties they replaced.
    private CanvasDisplayToggles _canvasToggles = CanvasDisplayToggles.Initial;

    /// <summary>
    /// Gets or sets a value indicating whether CAD pads should be displayed on the canvas.
    /// </summary>
    public bool ShowCad
    {
        get => _canvasToggles.ShowCad;
        set
        {
            if (_canvasToggles.ShowCad == value)
            {
                return;
            }

            OnShowCadChanging(value);
            OnPropertyChanging();
            _canvasToggles = _canvasToggles with { ShowCad = value };
            OnShowCadChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnShowCadChanging(bool value);

    partial void OnShowCadChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether regular pads should be displayed on the canvas.
    /// </summary>
    public bool ShowRegular
    {
        get => _canvasToggles.ShowRegular;
        set
        {
            if (_canvasToggles.ShowRegular == value)
            {
                return;
            }

            OnShowRegularChanging(value);
            OnPropertyChanging();
            _canvasToggles = _canvasToggles with { ShowRegular = value };
            OnShowRegularChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnShowRegularChanging(bool value);

    partial void OnShowRegularChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether unmatched pads should be highlighted on the canvas.
    /// </summary>
    public bool HighlightUnmatched
    {
        get => _canvasToggles.HighlightUnmatched;
        set
        {
            if (_canvasToggles.HighlightUnmatched == value)
            {
                return;
            }

            OnHighlightUnmatchedChanging(value);
            OnPropertyChanging();
            _canvasToggles = _canvasToggles with { HighlightUnmatched = value };
            OnHighlightUnmatchedChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnHighlightUnmatchedChanging(bool value);

    partial void OnHighlightUnmatchedChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether freeform pads should be highlighted on the canvas.
    /// </summary>
    public bool HighlightFreeform
    {
        get => _canvasToggles.HighlightFreeform;
        set
        {
            if (_canvasToggles.HighlightFreeform == value)
            {
                return;
            }

            OnHighlightFreeformChanging(value);
            OnPropertyChanging();
            _canvasToggles = _canvasToggles with { HighlightFreeform = value };
            OnHighlightFreeformChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnHighlightFreeformChanging(bool value);

    partial void OnHighlightFreeformChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether CAD pads should be colored by area on the canvas.
    /// </summary>
    public bool ColorCadByArea
    {
        get => _canvasToggles.ColorCadByArea;
        set
        {
            if (_canvasToggles.ColorCadByArea == value)
            {
                return;
            }

            OnColorCadByAreaChanging(value);
            OnPropertyChanging();
            _canvasToggles = _canvasToggles with { ColorCadByArea = value };
            OnColorCadByAreaChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnColorCadByAreaChanging(bool value);

    partial void OnColorCadByAreaChanged(bool value);

    public bool ImportOnlyClosedPolylines { get => CadImport.ImportOnlyClosedPolylines; set => CadImport.ImportOnlyClosedPolylines = value; }
    public bool ImportBlockPolylines { get => CadImport.ImportBlockPolylines; set => CadImport.ImportBlockPolylines = value; }

    private readonly WorkflowPanelState _workflowPanelState;

    /// <summary>
    /// Gets or sets whether the console panel is expanded.
    /// </summary>
    public bool IsConsoleExpanded { get => _workflowPanelState.IsConsoleExpanded; set => _workflowPanelState.IsConsoleExpanded = value; }

    /// <summary>
    /// Gets or sets whether Step 1 workflow block is expanded.
    /// </summary>
    public bool IsStep1Expanded { get => _workflowPanelState.IsStep1Expanded; set => _workflowPanelState.IsStep1Expanded = value; }
    /// <summary>
    /// Gets or sets whether Step 2 workflow block is expanded.
    /// </summary>
    public bool IsStep2Expanded { get => _workflowPanelState.IsStep2Expanded; set => _workflowPanelState.IsStep2Expanded = value; }
    /// <summary>
    /// Gets or sets whether Step 3 workflow block is expanded.
    /// </summary>
    public bool IsStep3Expanded { get => _workflowPanelState.IsStep3Expanded; set => _workflowPanelState.IsStep3Expanded = value; }
    /// <summary>
    /// Gets or sets whether Step 3 shows detailed To Full explanation text.
    /// </summary>
    [ObservableProperty] private bool _isStep3ToFullDetailsExpanded;
    /// <summary>
    /// Gets or sets whether Step 4 workflow block is expanded.
    /// </summary>
    public bool IsStep4Expanded { get => _workflowPanelState.IsStep4Expanded; set => _workflowPanelState.IsStep4Expanded = value; }

    /// <summary>
    /// Gets or sets whether Step 5 workflow block is expanded.
    /// </summary>
    public bool IsStep5Expanded { get => _workflowPanelState.IsStep5Expanded; set => _workflowPanelState.IsStep5Expanded = value; }
    /// <summary>
    /// Gets or sets whether Step 6 workflow block is expanded.
    /// </summary>
    public bool IsStep6Expanded { get => _workflowPanelState.IsStep6Expanded; set => _workflowPanelState.IsStep6Expanded = value; }

    // --- Canvas Visual Style Properties ---

    private const decimal GlobalFontSizePercentMin = 80m;
    private const decimal GlobalFontSizePercentMax = 140m;

    private readonly CanvasStrokeFillState _canvasStrokeFillState;

    /// <summary>
    /// Gets or sets the line width for drawing CAD pads on the canvas.
    /// </summary>
    public decimal CadLineWidth { get => _canvasStrokeFillState.CadLineWidth; set => _canvasStrokeFillState.CadLineWidth = value; }
    /// <summary>
    /// Gets or sets the line color for drawing CAD pads on the canvas.
    /// </summary>
    [ObservableProperty] private Color _cadLineColor;
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for CAD pad lines.
    /// </summary>
    public decimal CadLineOpacity { get => _canvasStrokeFillState.CadLineOpacity; set => _canvasStrokeFillState.CadLineOpacity = value; }
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for CAD pad fills.
    /// </summary>
    public decimal CadFillOpacity { get => _canvasStrokeFillState.CadFillOpacity; set => _canvasStrokeFillState.CadFillOpacity = value; }
    /// <summary>
    /// Gets or sets the line width for drawing regular pads on the canvas.
    /// </summary>
    public decimal RegularLineWidth { get => _canvasStrokeFillState.RegularLineWidth; set => _canvasStrokeFillState.RegularLineWidth = value; }
    /// <summary>
    /// Gets or sets a line-width adjustment for highlight/selection overlays.
    /// Positive values make highlights thicker; negative values make them thinner.
    /// </summary>
    public decimal HighlightStrokeWidthAdjust
    {
        get => _canvasStrokeFillState.HighlightStrokeWidthAdjust;
        set => _canvasStrokeFillState.HighlightStrokeWidthAdjust = value;
    }
    /// <summary>
    /// Gets or sets the line color for drawing regular pads on the canvas.
    /// </summary>
    [ObservableProperty] private Color _regularLineColor;
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for regular pad lines.
    /// </summary>
    public decimal RegularLineOpacity { get => _canvasStrokeFillState.RegularLineOpacity; set => _canvasStrokeFillState.RegularLineOpacity = value; }
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for regular pad fills.
    /// </summary>
    public decimal RegularFillOpacity { get => _canvasStrokeFillState.RegularFillOpacity; set => _canvasStrokeFillState.RegularFillOpacity = value; }
    /// <summary>
    /// Gets or sets the line/fill color for selected regular pads on the canvas.
    /// </summary>
    [ObservableProperty] private Color _regularSelectedColor;
    /// <summary>
    /// Gets or sets the fill opacity (0-1) for selected regular pads.
    /// </summary>
    public decimal RegularSelectedFillOpacity
    {
        get => _canvasStrokeFillState.RegularSelectedFillOpacity;
        set => _canvasStrokeFillState.RegularSelectedFillOpacity = value;
    }
    /// <summary>
    /// Gets or sets the hexadecimal string representation of the CAD line color.
    /// </summary>
    [ObservableProperty] private string _cadLineColorHex = string.Empty;
    /// <summary>
    /// Gets or sets the hexadecimal string representation of the regular line color.
    /// </summary>
    [ObservableProperty] private string _regularLineColorHex = string.Empty;
    /// <summary>
    /// Gets or sets the hexadecimal string representation of the selected regular pad color.
    /// </summary>
    [ObservableProperty] private string _regularSelectedColorHex = string.Empty;

    private void ApplyCanvasColorDefaultsFromTokens()
    {
        if (!UiThread.IsCurrent(out _, out _))
        {
            return;
        }

        var cadLineColor = ResolveTokenColor("ColorCanvasCadLine");
        var regularLineColor = ResolveTokenColor("ColorCanvasRegularLine");
        var regularSelectedColor = ResolveTokenColor("ColorCanvasRegularSelected");

        if (cadLineColor.HasValue)
        {
            CadLineColor = cadLineColor.Value;
        }

        if (regularLineColor.HasValue)
        {
            RegularLineColor = regularLineColor.Value;
        }

        if (regularSelectedColor.HasValue)
        {
            RegularSelectedColor = regularSelectedColor.Value;
        }
    }

    internal static Color? ResolveTokenColor(string resourceKey)
    {
        if (!UiThread.IsCurrent(out _, out var app))
        {
            return null;
        }

        var theme = app!.ActualThemeVariant ?? ThemeVariant.Default;
        if (TryResolveTokenColor(app.Resources, resourceKey, theme, out var resolvedColor))
        {
            return resolvedColor;
        }

        if (!ReferenceEquals(theme, ThemeVariant.Default) &&
            TryResolveTokenColor(app.Resources, resourceKey, ThemeVariant.Default, out resolvedColor))
        {
            return resolvedColor;
        }

        return null;
    }

    private static bool TryResolveTokenColor(global::Avalonia.Controls.IResourceDictionary resources, string resourceKey, ThemeVariant theme, out Color color)
    {
        if (resources.TryGetResource(resourceKey, theme, out var resource))
        {
            switch (resource)
            {
                case Color resolvedColor:
                    color = resolvedColor;
                    return true;
                case SolidColorBrush brush:
                    color = brush.Color;
                    return true;
            }
        }

        color = default;
        return false;
    }

    partial void OnShowCadChanging(bool value) => TrackUndo(ShowCad, value, v => ShowCad = v, "Toggle CAD layer");
    partial void OnShowRegularChanging(bool value) => TrackUndo(ShowRegular, value, v => ShowRegular = v, "Toggle regular grid");
    partial void OnHighlightUnmatchedChanging(bool value) => TrackUndo(HighlightUnmatched, value, v => HighlightUnmatched = v, "Toggle unmatched highlight");
    partial void OnHighlightFreeformChanging(bool value) => TrackUndo(HighlightFreeform, value, v => HighlightFreeform = v, "Toggle freeform highlight");
    partial void OnColorCadByAreaChanging(bool value) => TrackUndo(ColorCadByArea, value, v => ColorCadByArea = v, "Toggle CAD area colors");
    partial void OnCadLineColorHexChanging(string value) => TrackUndo(CadLineColorHex, value, v => CadLineColorHex = v, "CAD line color");
    partial void OnRegularLineColorHexChanging(string value) => TrackUndo(RegularLineColorHex, value, v => RegularLineColorHex = v, "Regular line color");
    partial void OnRegularSelectedColorHexChanging(string value) => TrackUndo(RegularSelectedColorHex, value, v => RegularSelectedColorHex = v, "Regular selection color");
}
