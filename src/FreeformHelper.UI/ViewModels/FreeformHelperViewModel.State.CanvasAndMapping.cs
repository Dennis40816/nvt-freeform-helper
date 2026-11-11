using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly CanvasAppearanceState _canvasAppearance;
    private readonly CadImportChoices _cadImportChoices;

    // Compatibility properties keep the existing binding and snapshot ports.
    public bool ShowCad { get => _canvasAppearance.ShowCad; set => _canvasAppearance.ShowCad = value; }
    public bool ShowRegular { get => _canvasAppearance.ShowRegular; set => _canvasAppearance.ShowRegular = value; }
    public bool HighlightUnmatched { get => _canvasAppearance.HighlightUnmatched; set => _canvasAppearance.HighlightUnmatched = value; }
    public bool HighlightFreeform { get => _canvasAppearance.HighlightFreeform; set => _canvasAppearance.HighlightFreeform = value; }
    public bool ColorCadByArea { get => _canvasAppearance.ColorCadByArea; set => _canvasAppearance.ColorCadByArea = value; }
    public bool ImportOnlyClosedPolylines
    {
        get => _cadImportChoices.ImportOnlyClosedPolylines;
        set => _cadImportChoices.ImportOnlyClosedPolylines = value;
    }
    public bool ImportBlockPolylines
    {
        get => _cadImportChoices.ImportBlockPolylines;
        set => _cadImportChoices.ImportBlockPolylines = value;
    }

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

    /// <summary>
    /// Gets or sets the line width for drawing CAD pads on the canvas.
    /// </summary>
    public decimal CadLineWidth
    {
        get => _canvasAppearance.StrokeFill.CadLineWidth;
        set => _canvasAppearance.StrokeFill.CadLineWidth = value;
    }
    /// <summary>
    /// Gets or sets the line color for drawing CAD pads on the canvas.
    /// </summary>
    public Color CadLineColor
    {
        get => Color.FromUInt32(_canvasAppearance.CadLineColor);
        set => _canvasAppearance.CadLineColor = value.ToUInt32();
    }
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for CAD pad lines.
    /// </summary>
    public decimal CadLineOpacity
    {
        get => _canvasAppearance.StrokeFill.CadLineOpacity;
        set => _canvasAppearance.StrokeFill.CadLineOpacity = value;
    }
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for CAD pad fills.
    /// </summary>
    public decimal CadFillOpacity
    {
        get => _canvasAppearance.StrokeFill.CadFillOpacity;
        set => _canvasAppearance.StrokeFill.CadFillOpacity = value;
    }
    /// <summary>
    /// Gets or sets the line width for drawing regular pads on the canvas.
    /// </summary>
    public decimal RegularLineWidth
    {
        get => _canvasAppearance.StrokeFill.RegularLineWidth;
        set => _canvasAppearance.StrokeFill.RegularLineWidth = value;
    }
    /// <summary>
    /// Gets or sets a line-width adjustment for highlight/selection overlays.
    /// Positive values make highlights thicker; negative values make them thinner.
    /// </summary>
    public decimal HighlightStrokeWidthAdjust
    {
        get => _canvasAppearance.StrokeFill.HighlightStrokeWidthAdjust;
        set => _canvasAppearance.StrokeFill.HighlightStrokeWidthAdjust = value;
    }
    /// <summary>
    /// Gets or sets the line color for drawing regular pads on the canvas.
    /// </summary>
    public Color RegularLineColor
    {
        get => Color.FromUInt32(_canvasAppearance.RegularLineColor);
        set => _canvasAppearance.RegularLineColor = value.ToUInt32();
    }
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for regular pad lines.
    /// </summary>
    public decimal RegularLineOpacity
    {
        get => _canvasAppearance.StrokeFill.RegularLineOpacity;
        set => _canvasAppearance.StrokeFill.RegularLineOpacity = value;
    }
    /// <summary>
    /// Gets or sets the opacity multiplier (0-1) for regular pad fills.
    /// </summary>
    public decimal RegularFillOpacity
    {
        get => _canvasAppearance.StrokeFill.RegularFillOpacity;
        set => _canvasAppearance.StrokeFill.RegularFillOpacity = value;
    }
    /// <summary>
    /// Gets or sets the line/fill color for selected regular pads on the canvas.
    /// </summary>
    public Color RegularSelectedColor
    {
        get => Color.FromUInt32(_canvasAppearance.RegularSelectedColor);
        set => _canvasAppearance.RegularSelectedColor = value.ToUInt32();
    }
    /// <summary>
    /// Gets or sets the fill opacity (0-1) for selected regular pads.
    /// </summary>
    public decimal RegularSelectedFillOpacity
    {
        get => _canvasAppearance.StrokeFill.RegularSelectedFillOpacity;
        set => _canvasAppearance.StrokeFill.RegularSelectedFillOpacity = value;
    }
    /// <summary>
    /// Gets or sets the hexadecimal string representation of the CAD line color.
    /// </summary>
    public string CadLineColorHex { get => _canvasAppearance.CadLineColorHex; set => _canvasAppearance.CadLineColorHex = value; }
    /// <summary>
    /// Gets or sets the hexadecimal string representation of the regular line color.
    /// </summary>
    public string RegularLineColorHex
    {
        get => _canvasAppearance.RegularLineColorHex;
        set => _canvasAppearance.RegularLineColorHex = value;
    }
    /// <summary>
    /// Gets or sets the hexadecimal string representation of the selected regular pad color.
    /// </summary>
    public string RegularSelectedColorHex
    {
        get => _canvasAppearance.RegularSelectedColorHex;
        set => _canvasAppearance.RegularSelectedColorHex = value;
    }

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

    private CanvasAppearanceState CreateCanvasAppearanceState()
    {
        var strokeFill = new CanvasStrokeFillState(
            () => _isLoadingSettings,
            () => _undoSuppression.IsActive,
            () => _undoSuppression.Enter(),
            PushUndo,
            () => CanvasHost?.Invalidate(),
            OnPropertyChanging,
            OnPropertyChanged);
        return new CanvasAppearanceState(strokeFill, _workspaceToggleRegistry,
            ParseCanvasColor, FormatCanvasColor, () => _undoSuppression.Enter(),
            TrackUndo, TrackUndo, OnPropertyChanging, OnPropertyChanged);
    }

    private static uint? ParseCanvasColor(string text)
        => Color.TryParse(text, out var color) ? color.ToUInt32() : null;

    private static string FormatCanvasColor(uint argb)
    {
        var color = Color.FromUInt32(argb);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
