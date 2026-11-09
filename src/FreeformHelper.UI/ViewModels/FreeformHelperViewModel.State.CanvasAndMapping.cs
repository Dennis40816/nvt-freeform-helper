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

    /// <summary>
    /// Gets or sets a value indicating whether only closed polylines should be imported from DXF.
    /// </summary>
    public bool ImportOnlyClosedPolylines
    {
        get => _canvasToggles.ImportOnlyClosedPolylines;
        set
        {
            if (_canvasToggles.ImportOnlyClosedPolylines == value)
            {
                return;
            }

            OnImportOnlyClosedPolylinesChanging(value);
            OnPropertyChanging();
            _canvasToggles = _canvasToggles with { ImportOnlyClosedPolylines = value };
            OnPropertyChanged();
        }
    }

    partial void OnImportOnlyClosedPolylinesChanging(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether polylines from the DXF BLOCKS section should be imported.
    /// </summary>
    public bool ImportBlockPolylines
    {
        get => _canvasToggles.ImportBlockPolylines;
        set
        {
            if (_canvasToggles.ImportBlockPolylines == value)
            {
                return;
            }

            OnImportBlockPolylinesChanging(value);
            OnPropertyChanging();
            _canvasToggles = _canvasToggles with { ImportBlockPolylines = value };
            OnPropertyChanged();
        }
    }

    partial void OnImportBlockPolylinesChanging(bool value);

    /// <summary>
    /// Gets or sets whether the console panel is expanded.
    /// </summary>
    [ObservableProperty] private bool _isConsoleExpanded = true;

    /// <summary>
    /// Gets or sets whether Step 1 workflow block is expanded.
    /// </summary>
    [ObservableProperty] private bool _isStep1Expanded = true;
    /// <summary>
    /// Gets or sets whether Step 2 workflow block is expanded.
    /// </summary>
    [ObservableProperty] private bool _isStep2Expanded;
    /// <summary>
    /// Gets or sets whether Step 3 workflow block is expanded.
    /// </summary>
    [ObservableProperty] private bool _isStep3Expanded;
    /// <summary>
    /// Gets or sets whether Step 3 shows detailed To Full explanation text.
    /// </summary>
    [ObservableProperty] private bool _isStep3ToFullDetailsExpanded;
    /// <summary>
    /// Gets or sets whether Step 4 workflow block is expanded.
    /// </summary>
    [ObservableProperty] private bool _isStep4Expanded;

    /// <summary>
    /// Gets or sets whether Step 5 workflow block is expanded.
    /// </summary>
    [ObservableProperty] private bool _isStep5Expanded;
    /// <summary>
    /// Gets or sets whether Step 6 workflow block is expanded.
    /// </summary>
    [ObservableProperty] private bool _isStep6Expanded;

    // --- Canvas Visual Style Properties ---

    private const decimal GlobalFontSizePercentMin = 80m;
    private const decimal GlobalFontSizePercentMax = 140m;

    /// <summary>
    /// Gets or sets the line width for drawing CAD pads on the canvas.
    /// </summary>
    [ObservableProperty] private decimal _cadLineWidth = 0.2m;
    /// <summary>
    /// Gets or sets the line color for drawing CAD pads on the canvas.
    /// </summary>
    [ObservableProperty] private Color _cadLineColor;
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for CAD pad lines.
    /// </summary>
    [ObservableProperty] private decimal _cadLineOpacity = 1.0m;
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for CAD pad fills.
    /// </summary>
    [ObservableProperty] private decimal _cadFillOpacity = 1.0m;
    /// <summary>
    /// Gets or sets the line width for drawing regular pads on the canvas.
    /// </summary>
    [ObservableProperty] private decimal _regularLineWidth = 0.2m;
    /// <summary>
    /// Gets or sets a line-width adjustment for highlight/selection overlays.
    /// Positive values make highlights thicker; negative values make them thinner.
    /// </summary>
    [ObservableProperty] private decimal _highlightStrokeWidthAdjust = -1m;
    /// <summary>
    /// Gets or sets the line color for drawing regular pads on the canvas.
    /// </summary>
    [ObservableProperty] private Color _regularLineColor;
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for regular pad lines.
    /// </summary>
    [ObservableProperty] private decimal _regularLineOpacity = 1.0m;
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for regular pad fills.
    /// </summary>
    [ObservableProperty] private decimal _regularFillOpacity = 1.0m;
    /// <summary>
    /// Gets or sets the line/fill color for selected regular pads on the canvas.
    /// </summary>
    [ObservableProperty] private Color _regularSelectedColor;
    /// <summary>
    /// Gets or sets the fill opacity (0-1) for selected regular pads.
    /// </summary>
    [ObservableProperty] private decimal _regularSelectedFillOpacity = 0.24m;
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
    partial void OnImportOnlyClosedPolylinesChanging(bool value) => TrackUndo(ImportOnlyClosedPolylines, value, v => ImportOnlyClosedPolylines = v, "Import closed polylines");
    partial void OnImportBlockPolylinesChanging(bool value) => TrackUndo(ImportBlockPolylines, value, v => ImportBlockPolylines = v, "Import block polylines");
    partial void OnCadLineWidthChanging(decimal value) => TrackUndo(CadLineWidth, value, v => CadLineWidth = v, "CAD line width");
    partial void OnRegularLineWidthChanging(decimal value) => TrackUndo(RegularLineWidth, value, v => RegularLineWidth = v, "Regular line width");
    partial void OnHighlightStrokeWidthAdjustChanging(decimal value) => TrackUndo(HighlightStrokeWidthAdjust, value, v => HighlightStrokeWidthAdjust = v, "Highlight line width");
    partial void OnCadLineColorHexChanging(string value) => TrackUndo(CadLineColorHex, value, v => CadLineColorHex = v, "CAD line color");
    partial void OnRegularLineColorHexChanging(string value) => TrackUndo(RegularLineColorHex, value, v => RegularLineColorHex = v, "Regular line color");
    partial void OnRegularSelectedColorHexChanging(string value) => TrackUndo(RegularSelectedColorHex, value, v => RegularSelectedColorHex = v, "Regular selection color");
    partial void OnRegularSelectedFillOpacityChanging(decimal value) => TrackUndo(RegularSelectedFillOpacity, value, v => RegularSelectedFillOpacity = v, "Regular selection fill opacity");
}
