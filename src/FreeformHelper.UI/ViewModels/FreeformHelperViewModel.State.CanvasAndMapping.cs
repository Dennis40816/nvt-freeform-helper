using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using Nvt.Core.Avalonia.Threading;

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
    /// Gets or sets the latest DXF overlap check result. The view model replaces the whole value.
    /// The properties below forward to it and raise their change notification when their own
    /// value changed.
    /// </summary>
    [ObservableProperty] private DxfOverlapResult _dxfOverlapResult = DxfOverlapResult.CreateNotRun();
    /// <summary>
    /// Gets the summary message for DXF overlap issues (e.g., "no overlaps detected").
    /// </summary>
    public string DxfOverlapSummary => DxfOverlapResult.Summary;
    /// <summary>
    /// Gets the observable collection of detailed DXF overlap issue messages.
    /// </summary>
    public ObservableCollection<string> DxfOverlapIssues => DxfOverlapResult.Issues;
    /// <summary>
    /// Gets the number of overlap issues currently loaded in details.
    /// </summary>
    public int DxfOverlapIssueCount => DxfOverlapResult.Issues.Count;
    /// <summary>
    /// Gets a value indicating whether any DXF overlap issues were found.
    /// </summary>
    public bool HasDxfOverlapIssues => DxfOverlapResult.Issues.Count > 0;
    /// <summary>
    /// Gets or sets a value indicating whether DXF overlap checking is currently running.
    /// </summary>
    public bool IsCheckingDxfOverlap
    {
        get => _dxfOverlapState.Current.IsBusy;
        set => SetOperationBusy(_dxfOverlapState, value, nameof(IsCheckingDxfOverlap));
    }
    /// <summary>
    /// Gets or sets the current progress value (0-1) for DXF overlap checking.
    /// </summary>
    public double DxfOverlapProgress
    {
        get => _dxfOverlapState.Current.Progress;
        set => SetOperationProgress(_dxfOverlapState, value, nameof(DxfOverlapProgress));
    }
    /// <summary>
    /// Gets the CAD pad IDs highlighted by overlap checking.
    /// </summary>
    public ObservableCollection<int> DxfOverlapHighlightedCadIds => DxfOverlapResult.HighlightedCadIds;
    /// <summary>
    /// Gets a value indicating whether highlighted overlap CAD pads exist.
    /// </summary>
    public bool HasDxfOverlapHighlights => DxfOverlapResult.HighlightedCadIds.Count > 0;
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

    /// <summary>Gets the number of hidden CAD pads.</summary>
    public int DeletedCadPadCount => _hiddenCadPadIds.Count;

    /// <summary>Gets whether hidden CAD pads exist.</summary>
    public bool HasDeletedCadPads => DeletedCadPadCount > 0;

    /// <summary>Gets the number of detected duplicate CAD pads.</summary>
    public int DuplicateCadPadCount => _duplicateCadPadIdToCanonicalId.Count;

    /// <summary>Gets whether detected duplicate CAD pads exist.</summary>
    public bool HasDuplicateCadPads => DuplicateCadPadCount > 0;

    /// <summary>Gets the number of synthetic combined CAD pads.</summary>
    public int CombinedCadPadCount => _combinedCad.OutputCadIds.Count;

    /// <summary>Gets whether synthetic combined CAD pads exist.</summary>
    public bool HasCombinedCadPads => CombinedCadPadCount > 0;

    /// <summary>
    /// Gets or sets the count of CAD pads whose layer differs from the current DXF edit baseline.
    /// </summary>
    // UI-thread owned; sync measures the O(n) baseline scan so binding reads stay O(1).
    [ObservableProperty] private int _relayeredCadPadCount;

    /// <summary>Gets whether any CAD pad has been moved to a different layer.</summary>
    public bool HasRelayeredCadPads => RelayeredCadPadCount > 0;

    /// <summary>
    /// Gets or sets the count of CAD pads whose polygon differs from the current DXF edit baseline.
    /// </summary>
    // UI-thread owned; sync measures the O(n) baseline scan so binding reads stay O(1).
    [ObservableProperty] private int _rotatedCadPadCount;

    /// <summary>Gets whether any CAD pad geometry differs from baseline.</summary>
    public bool HasRotatedCadPads => RotatedCadPadCount > 0;

    /// <summary>Gets whether hidden, combined, relayered or rotated CAD edits exist.</summary>
    public bool HasAnyCadEdits => HasDxfEditChanges(HasDeletedCadPads, HasCombinedCadPads, HasRelayeredCadPads, HasRotatedCadPads);

    /// <summary>
    /// Gets or sets available DXF layer names for "move selected pads" action.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _dxfEditLayerOptions = new();

    // The DXF edit panel inputs are stored once. The properties below forward to the record and raise
    // the same notifications and callbacks as the stored properties they replaced.
    private DxfEditPanelInput _dxfEditPanel = DxfEditPanelInput.Initial;

    /// <summary>
    /// Gets or sets the target DXF layer name used to move selected pads.
    /// </summary>
    public string DxfEditTargetLayerName
    {
        get => _dxfEditPanel.DxfEditTargetLayerName;
        set
        {
            if (_dxfEditPanel.DxfEditTargetLayerName == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditTargetLayerName = value };
            OnDxfEditTargetLayerNameChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditTargetLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the currently selected DXF rotation scope option.
    /// </summary>
    public DxfEditRotationScopeOption SelectedDxfEditRotationScopeOption
    {
        get => _dxfEditPanel.SelectedDxfEditRotationScopeOption;
        set
        {
            if (_dxfEditPanel.SelectedDxfEditRotationScopeOption == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { SelectedDxfEditRotationScopeOption = value };
            OnSelectedDxfEditRotationScopeOptionChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnSelectedDxfEditRotationScopeOptionChanged(DxfEditRotationScopeOption value);

    /// <summary>
    /// Gets or sets the target DXF layer name used when rotating by layer scope.
    /// </summary>
    public string DxfEditRotationLayerName
    {
        get => _dxfEditPanel.DxfEditRotationLayerName;
        set
        {
            if (_dxfEditPanel.DxfEditRotationLayerName == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditRotationLayerName = value };
            OnDxfEditRotationLayerNameChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditRotationLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the input text used to create a new DXF layer.
    /// </summary>
    public string DxfEditNewLayerName
    {
        get => _dxfEditPanel.DxfEditNewLayerName;
        set
        {
            if (_dxfEditPanel.DxfEditNewLayerName == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditNewLayerName = value };
            OnDxfEditNewLayerNameChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditNewLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the rotation angle for selected CAD pads in DXF edit workflow.
    /// </summary>
    public decimal DxfEditRotationDegrees
    {
        get => _dxfEditPanel.DxfEditRotationDegrees;
        set
        {
            if (_dxfEditPanel.DxfEditRotationDegrees == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditRotationDegrees = value };
            OnDxfEditRotationDegreesChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditRotationDegreesChanged(decimal value);

    /// <summary>
    /// Gets or sets a value indicating whether the advanced DXF edit block is expanded.
    /// </summary>
    public bool IsDxfEditAdvancedPanelExpanded
    {
        get => _dxfEditPanel.IsDxfEditAdvancedPanelExpanded;
        set
        {
            if (_dxfEditPanel.IsDxfEditAdvancedPanelExpanded == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { IsDxfEditAdvancedPanelExpanded = value };
            OnPropertyChanged();
        }
    }

    // --- DXF/Regular Mapping Properties ---

    /// <summary>
    /// Gets or sets the latest DXF/Regular mapping diagnostics result. The view model replaces the
    /// whole value. The properties that forward to it raise their change notification when their
    /// own value changed.
    /// </summary>
    [ObservableProperty] private MappingDiagnosticsResult _mappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun();
    /// <summary>
    /// Gets the summary message for DXF/Regular mapping analysis.
    /// </summary>
    public string DxfRegularMappingSummary => MappingDiagnosticsResult.Summary;
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
    public bool IsMatchingPads
    {
        get => _matchState.Current.IsBusy;
        set => SetOperationBusy(_matchState, value, nameof(IsMatchingPads));
    }
    /// <summary>
    /// Gets or sets the current progress value (0-1) for pad matching.
    /// </summary>
    public double MatchProgress
    {
        get => _matchState.Current.Progress;
        set => SetOperationProgress(_matchState, value, nameof(MatchProgress));
    }
    /// <summary>
    /// Gets or sets a value indicating whether index mapping diagnostics are currently running.
    /// </summary>
    public bool IsAnalyzingIndexMapping
    {
        get => _indexMappingState.Current.IsBusy;
        set => SetOperationBusy(_indexMappingState, value, nameof(IsAnalyzingIndexMapping));
    }
    /// <summary>
    /// Gets or sets the current progress value (0-1) for index mapping diagnostics.
    /// </summary>
    public double IndexMappingProgress
    {
        get => _indexMappingState.Current.Progress;
        set => SetOperationProgress(_indexMappingState, value, nameof(IndexMappingProgress));
    }

    /// <summary>
    /// Gets the observable collection of detailed DXF/Regular mapping issues.
    /// </summary>
    public ObservableCollection<string> DxfRegularMappingIssues => MappingDiagnosticsResult.Issues;
    /// <summary>
    /// Gets a value indicating whether any DXF/Regular mapping issues were found.
    /// </summary>
    public bool HasDxfRegularMappingIssues => MappingDiagnosticsResult.HasIssues;

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
