using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // --- Workspace Header Items ---

    /// <summary>
    /// Gets or sets the observable collection of <see cref="WorkspaceHeaderItem"/>
    /// that are displayed in the application's header menu.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<WorkspaceHeaderItem> _workspaceHeaderItems = new();

    // --- Project State Flags ---

    /// <summary>
    /// Gets or sets a value indicating whether the current project has unsaved changes.
    /// </summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>
    /// Gets or sets a value indicating whether CAD bounds should be recalculated from visible layers
    /// when layer filters change and on grid rebuilds.
    /// </summary>
    [ObservableProperty]
    private bool _recalcBoundsOnLayerFilter;

    // --- Canvas Toggles (Directly bound to UI for visual state) ---

    /// <summary>
    /// Gets or sets a value indicating whether CAD pads should be displayed on the canvas.
    /// </summary>
    [ObservableProperty] private bool _showCad = true;
    /// <summary>
    /// Gets or sets a value indicating whether regular pads should be displayed on the canvas.
    /// </summary>
    [ObservableProperty] private bool _showRegular = true;
    /// <summary>
    /// Gets or sets a value indicating whether unmatched pads should be highlighted on the canvas.
    /// </summary>
    [ObservableProperty] private bool _highlightUnmatched = true;
    /// <summary>
    /// Gets or sets a value indicating whether freeform pads should be highlighted on the canvas.
    /// </summary>
    [ObservableProperty] private bool _highlightFreeform = true;
    /// <summary>
    /// Gets or sets a value indicating whether CAD pads should be colored by area on the canvas.
    /// </summary>
    [ObservableProperty] private bool _colorCadByArea = false;
    /// <summary>
    /// Gets or sets a value indicating whether only closed polylines should be imported from DXF.
    /// </summary>
    [ObservableProperty] private bool _importOnlyClosedPolylines = true;
    /// <summary>
    /// Gets or sets a value indicating whether polylines from the DXF BLOCKS section should be imported.
    /// </summary>
    [ObservableProperty] private bool _importBlockPolylines = true;
    /// <summary>
    /// Gets or sets the observable collection of <see cref="LayerToggle"/> objects
    /// used to control the visibility of CAD layers.
    /// </summary>
    [ObservableProperty] private ObservableCollection<LayerToggle> _layerToggles = new();

    /// <summary>
    /// Gets or sets a value indicating whether any CAD layers are available.
    /// Used to hide the layer section when no DXF is loaded.
    /// </summary>
    [ObservableProperty] private bool _hasCadLayers;

    /// <summary>
    /// Gets or sets the available layer options for bounding the grid.
    /// </summary>
    [ObservableProperty] private ObservableCollection<BoundLayerOption> _boundLayerOptions = new();

    /// <summary>
    /// Gets or sets the currently selected layer option for bounding the grid.
    /// </summary>
    [ObservableProperty] private BoundLayerOption _selectedBoundLayerOption = new BoundLayerOption(null, "Auto (visible layers)");

    /// <summary>
    /// Gets or sets the helper text shown under bounds layer selector,
    /// including truncation hint when available layers exceed dropdown cap.
    /// </summary>
    [ObservableProperty] private string _boundLayerOptionSummary = "Hidden bounds layer is still valid.";

    /// <summary>
    /// Gets or sets the available layer options for DXF regular source mode.
    /// </summary>
    [ObservableProperty] private ObservableCollection<RegularSourceLayerOption> _regularSourceLayerOptions = new();

    /// <summary>
    /// Gets or sets the currently selected layer option for DXF regular source mode.
    /// </summary>
    [ObservableProperty] private RegularSourceLayerOption _selectedRegularSourceLayerOption = new RegularSourceLayerOption(null, "Select layer");

    /// <summary>
    /// Gets or sets helper text for DXF regular-source validation and suggested IC/grid settings.
    /// </summary>
    [ObservableProperty] private string _dxfRegularSourceHint = string.Empty;

    // --- DXF Quality Check Properties ---

    /// <summary>
    /// Gets or sets a summary message for DXF overlap issues (e.g., "no overlaps detected").
    /// </summary>
    [ObservableProperty] private string _dxfOverlapSummary = "DXF check: not run.";
    /// <summary>
    /// Gets or sets the observable collection of detailed DXF overlap issue messages.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _dxfOverlapIssues = new();
    /// <summary>
    /// Gets or sets the number of overlap issues currently loaded in details.
    /// </summary>
    [ObservableProperty] private int _dxfOverlapIssueCount;
    /// <summary>
    /// Gets or sets a value indicating whether any DXF overlap issues were found.
    /// </summary>
    [ObservableProperty] private bool _hasDxfOverlapIssues;
    /// <summary>
    /// Gets or sets a value indicating whether DXF overlap checking is currently running.
    /// </summary>
    [ObservableProperty] private bool _isCheckingDxfOverlap;
    /// <summary>
    /// Gets or sets the current progress value (0-1) for DXF overlap checking.
    /// </summary>
    [ObservableProperty] private double _dxfOverlapProgress;
    /// <summary>
    /// Gets or sets CAD pad IDs highlighted by overlap checking.
    /// </summary>
    [ObservableProperty] private ObservableCollection<int> _dxfOverlapHighlightedCadIds = new();
    /// <summary>
    /// Gets or sets a value indicating whether highlighted overlap CAD pads exist.
    /// </summary>
    [ObservableProperty] private bool _hasDxfOverlapHighlights;
    /// <summary>
    /// Gets or sets the latest CAD?egular overlap links for canvas ratio labels.
    /// </summary>
    [ObservableProperty] private IReadOnlyList<PadMatchLink> _matchLinksForCanvas = Array.Empty<PadMatchLink>();

    /// <summary>
    /// Gets or sets a value indicating whether the AA canvas should show the shared CAD-load overlay.
    /// This is used by both Open DXF and Load Project while DXF import/reload and grid rebuild are in progress.
    /// </summary>
    [ObservableProperty] private bool _isCadLoadCanvasOverlayVisible;

    /// <summary>
    /// Gets or sets a value indicating whether the global modal loading spinner host should be visible.
    /// Long-running operations use this as a single entry/exit lifecycle rather than re-deriving ad hoc busy UI.
    /// </summary>
    [ObservableProperty] private bool _isModalLoadingSpinnerVisible;

    /// <summary>
    /// Gets or sets Notch 2.2 preview overlays shown on the main canvas.
    /// </summary>
    [ObservableProperty] private int _deletedCadPadCount;

    /// <summary>
    /// Gets or sets a value indicating whether there are hidden CAD pads.
    /// </summary>
    [ObservableProperty] private bool _hasDeletedCadPads;

    /// <summary>
    /// Gets or sets the count of synthetic combined CAD pads.
    /// </summary>
    [ObservableProperty] private int _duplicateCadPadCount;

    /// <summary>
    /// Gets or sets a value indicating whether auto-hidden duplicate CAD pads exist.
    /// </summary>
    [ObservableProperty] private bool _hasDuplicateCadPads;

    /// <summary>
    /// Gets or sets the count of synthetic combined CAD pads.
    /// </summary>
    [ObservableProperty] private int _combinedCadPadCount;

    /// <summary>
    /// Gets or sets a value indicating whether synthetic combined CAD pads exist.
    /// </summary>
    [ObservableProperty] private bool _hasCombinedCadPads;

    /// <summary>
    /// Gets or sets the count of CAD pads whose layer differs from the current DXF edit baseline.
    /// </summary>
    [ObservableProperty] private int _relayeredCadPadCount;

    /// <summary>
    /// Gets or sets a value indicating whether any CAD pad has been moved to a different layer.
    /// </summary>
    [ObservableProperty] private bool _hasRelayeredCadPads;

    /// <summary>
    /// Gets or sets the count of CAD pads whose polygon differs from the current DXF edit baseline.
    /// </summary>
    [ObservableProperty] private int _rotatedCadPadCount;

    /// <summary>
    /// Gets or sets a value indicating whether any CAD pad geometry has been rotated from baseline.
    /// </summary>
    [ObservableProperty] private bool _hasRotatedCadPads;

    /// <summary>
    /// Gets or sets a value indicating whether any DXF edit (hidden, combined, or relayered) exists.
    /// </summary>
    [ObservableProperty] private bool _hasAnyCadEdits;

    /// <summary>
    /// Gets or sets available DXF layer names for "move selected pads" action.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _dxfEditLayerOptions = new();

    /// <summary>
    /// Gets or sets the target DXF layer name used to move selected pads.
    /// </summary>
    [ObservableProperty] private string _dxfEditTargetLayerName = string.Empty;

    /// <summary>
    /// Gets or sets the currently selected DXF rotation scope option.
    /// </summary>
    [ObservableProperty] private DxfEditRotationScopeOption _selectedDxfEditRotationScopeOption;

    /// <summary>
    /// Gets or sets the target DXF layer name used when rotating by layer scope.
    /// </summary>
    [ObservableProperty] private string _dxfEditRotationLayerName = string.Empty;

    /// <summary>
    /// Gets or sets the input text used to create a new DXF layer.
    /// </summary>
    [ObservableProperty] private string _dxfEditNewLayerName = string.Empty;

    /// <summary>
    /// Gets or sets the rotation angle for selected CAD pads in DXF edit workflow.
    /// </summary>
    [ObservableProperty] private decimal _dxfEditRotationDegrees = 90m;

    /// <summary>
    /// Gets or sets a value indicating whether the advanced DXF edit block is expanded.
    /// </summary>
    [ObservableProperty] private bool _isDxfEditAdvancedPanelExpanded = true;

    // --- DXF/Regular Mapping Properties ---

    /// <summary>
    /// Gets or sets a summary message for DXF/Regular mapping analysis.
    /// </summary>
    [ObservableProperty] private string _dxfRegularMappingSummary = "Diagnostics: not run.";
    /// <summary>
    /// Gets or sets a summary message for the externally measured regular visibility mask.
    /// </summary>
    [ObservableProperty] private string _regularVisibilityMaskSummary = "Regular visibility mask: not loaded. Import Regular Visibility Mask (SeeRegular.csv) in Step 1 to constrain Step 4 and Simulation.";
    /// <summary>
    /// Gets or sets a value indicating whether the imported regular visibility mask should constrain Step 4 and Simulation.
    /// </summary>
    [ObservableProperty] private bool _isRegularVisibilityMaskEnabled;
    /// <summary>
    /// Gets or sets a value indicating whether pad matching is currently running.
    /// </summary>
    [ObservableProperty] private bool _isMatchingPads;
    /// <summary>
    /// Gets or sets the current progress value (0-1) for pad matching.
    /// </summary>
    [ObservableProperty] private double _matchProgress;
    /// <summary>
    /// Gets or sets a value indicating whether index mapping diagnostics are currently running.
    /// </summary>
    [ObservableProperty] private bool _isAnalyzingIndexMapping;
    /// <summary>
    /// Gets or sets the current progress value (0-1) for index mapping diagnostics.
    /// </summary>
    [ObservableProperty] private double _indexMappingProgress;
    /// <summary>
    /// Gets or sets the observable collection of detailed DXF/Regular mapping issues.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _dxfRegularMappingIssues = new();
    /// <summary>
    /// Gets or sets a value indicating whether any DXF/Regular mapping issues were found.
    /// </summary>
    [ObservableProperty] private bool _hasDxfRegularMappingIssues;

    /// <summary>
    /// Gets or sets the console font size (used for in-app log view).
    /// </summary>
    [ObservableProperty] private double _consoleFontSize = 12.0;
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
    private const decimal NotchPreviewAutoPlayIntervalMinMs = 200m;
    private const decimal NotchPreviewAutoPlayIntervalMaxMs = 5000m;

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

}
