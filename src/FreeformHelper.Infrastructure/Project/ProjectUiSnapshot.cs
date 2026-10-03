using System.Text.Json.Serialization;

namespace FreeformHelper.Infrastructure.Project;

/// <summary>
/// Marks a deserialization-only UI snapshot field that is intentionally excluded from the current persistence contract.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LegacyUiSnapshotFieldAttribute : Attribute
{
}

/// <summary>
/// Represents a snapshot of the user interface (UI) state related to a project.
/// This class is used to persist non-critical UI settings and user preferences
/// that enhance user experience upon reloading a project, but are not essential
/// for the core application logic or data integrity.
/// </summary>
public sealed class ProjectUiSnapshot
{
    /// <summary>
    /// Gets or sets the UI snapshot for grid-related settings.
    /// </summary>
    public UiGridSnapshot Grid { get; set; } = new();
    /// <summary>
    /// Gets or sets the UI snapshot for matching-related settings.
    /// </summary>
    public UiMatchingSnapshot Matching { get; set; } = new();
    /// <summary>
    /// Gets or sets the UI snapshot for view/display-related settings.
    /// </summary>
    public UiViewSnapshot View { get; set; } = new();
    /// <summary>
    /// Gets or sets the UI snapshot for notch-related settings.
    /// </summary>
    public UiNotchSnapshot Notch { get; set; } = new();
    /// <summary>
    /// Gets or sets the UI snapshot for import-related settings.
    /// </summary>
    public UiImportSnapshot Import { get; set; } = new();
}

/// <summary>
/// Captures the UI state related to grid configuration.
/// </summary>
public sealed class UiGridSnapshot
{
    private List<int> _icX = new();
    private List<int> _icY = new();

    /// <summary>
    /// Gets or sets the number of cascaded ICs displayed in the UI.
    /// </summary>
    public int CascadeCount { get; set; }
    /// <summary>
    /// Gets or sets the list of X-channels per IC as displayed in the UI.
    /// </summary>
    public List<int> IcX
    {
        get => _icX;
        set => _icX = value is null ? new List<int>() : new List<int>(value);
    }
    /// <summary>
    /// Gets or sets the list of Y-channels per IC as displayed in the UI.
    /// </summary>
    public List<int> IcY
    {
        get => _icY;
        set => _icY = value is null ? new List<int>() : new List<int>(value);
    }
    /// <summary>
    /// Gets or sets the total number of X-channels displayed in the UI.
    /// </summary>
    public int TotalX { get; set; }
    /// <summary>
    /// Gets or sets the total number of Y-channels displayed in the UI.
    /// </summary>
    public int TotalY { get; set; }
    /// <summary>
    /// Gets or sets the selected scan order string as displayed in the UI.
    /// </summary>
    public string ScanOrder { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the selected grid alignment mode string as displayed in the UI.
    /// </summary>
    public string AlignmentMode { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the grid padding percentage value displayed in the UI.
    /// </summary>
    public double GridPaddingPercent { get; set; }
    /// <summary>
    /// Gets or sets the active area width in mm displayed in the UI.
    /// </summary>
    public double ActiveAreaWidthMm { get; set; }
    /// <summary>
    /// Gets or sets the active area height in mm displayed in the UI.
    /// </summary>
    public double ActiveAreaHeightMm { get; set; }
    /// <summary>
    /// Gets or sets the X-direction pitch in mm displayed in the UI.
    /// </summary>
    public double PitchXmm { get; set; }
    /// <summary>
    /// Gets or sets the Y-direction pitch in mm displayed in the UI.
    /// </summary>
    public double PitchYmm { get; set; }
}

/// <summary>
/// Captures the UI state related to pad matching configuration.
/// </summary>
public sealed class UiMatchingSnapshot
{
    /// <summary>
    /// Gets or sets the retired match-mode display value retained for project compatibility.
    /// </summary>
    public string MatchMode { get; set; } = "Overlap";
    /// <summary>
    /// Gets or sets the match threshold value displayed in the UI.
    /// </summary>
    public double MatchThreshold { get; set; }
    /// <summary>
    /// Gets or sets the freeform directional threshold displayed in the UI.
    /// </summary>
    public double FreeformAxisThreshold { get; set; } = 0.1;
    /// <summary>
    /// Gets or sets a value indicating whether XY auto-detect is enabled in the UI.
    /// </summary>
    public bool EnableAutoDetectXy { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether edge-only freeform specialization is enabled in the UI.
    /// </summary>
    public bool EnableFreeformEdgeSpecialization { get; set; }
    /// <summary>
    /// Gets or sets the retired centroid-fallback value retained for project compatibility.
    /// </summary>
    public bool EnableCentroidFallback { get; set; } = true;
    /// <summary>
    /// Gets or sets the retired nearest-K value retained for project compatibility.
    /// </summary>
    public int NearestK { get; set; } = 5;
}

/// <summary>
/// Captures the UI state related to view and display settings.
/// </summary>
public sealed class UiViewSnapshot
{
    private List<LayerSelectionSnapshot> _layerSelections = new();

    /// <summary>
    /// Gets or sets a value indicating whether CAD pads are shown in the UI.
    /// </summary>
    public bool ShowCad { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether regular pads are shown in the UI.
    /// </summary>
    public bool ShowRegular { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether unmatched pads are highlighted in the UI.
    /// </summary>
    public bool HighlightUnmatched { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether freeform pads are highlighted in the UI.
    /// </summary>
    public bool HighlightFreeform { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether CAD pads are colored by area in the UI.
    /// </summary>
    public bool ColorCadByArea { get; set; }
    /// <summary>
    /// Gets or sets the line width for CAD pads displayed in the UI.
    /// </summary>
    public double CadLineWidth { get; set; } = 0.2;
    /// <summary>
    /// Gets or sets the fill opacity for CAD pads displayed in the UI.
    /// </summary>
    public double CadFillOpacity { get; set; } = 1.0;
    /// <summary>
    /// Gets or sets the line opacity for CAD pads displayed in the UI.
    /// </summary>
    public double CadLineOpacity { get; set; } = 1.0;
    /// <summary>
    /// Gets or sets the line color for CAD pads displayed in the UI (e.g., hex string).
    /// </summary>
    public string CadLineColor { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the line width for regular pads displayed in the UI.
    /// </summary>
    public double RegularLineWidth { get; set; } = 0.2;
    /// <summary>
    /// Gets or sets the line-width adjustment for highlight/selection overlays.
    /// </summary>
    public double HighlightStrokeWidthAdjust { get; set; } = -1.0;
    /// <summary>
    /// Gets or sets the fill opacity for regular pads displayed in the UI.
    /// </summary>
    public double RegularFillOpacity { get; set; } = 1.0;
    /// <summary>
    /// Gets or sets the line opacity for regular pads displayed in the UI.
    /// </summary>
    public double RegularLineOpacity { get; set; } = 1.0;
    /// <summary>
    /// Gets or sets the line color for regular pads displayed in the UI (e.g., hex string).
    /// </summary>
    public string RegularLineColor { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the selection color for regular pads displayed in the UI (e.g., hex string).
    /// </summary>
    public string RegularSelectedColor { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the selection fill opacity (0-1) for regular pads displayed in the UI.
    /// </summary>
    public double RegularSelectedFillOpacity { get; set; }
    /// <summary>
    /// Gets or sets the area bucket tolerance for coloring displayed in the UI.
    /// </summary>
    public double AreaBucketTolerance { get; set; } = 0.001;
    /// <summary>
    /// Gets or sets the maximum number of area buckets for coloring displayed in the UI.
    /// </summary>
    public int MaxAreaBuckets { get; set; } = 32;
    /// <summary>
    /// Gets or sets the list of layer selection states (name and selected status).
    /// </summary>
    public List<LayerSelectionSnapshot> LayerSelections
    {
        get => _layerSelections;
        set => _layerSelections = value is null ? new List<LayerSelectionSnapshot>() : new List<LayerSelectionSnapshot>(value);
    }
    /// <summary>
    /// Gets or sets a value indicating whether CAD Output FW Diff overlay markers are shown on CAD pads.
    /// </summary>
    public bool ShowDiffIndexOverlay { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether Notch preview overlay is shown on AA canvas.
    /// </summary>
    public bool ShowNotchCanvasPreview { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether To Regular ratio labels are shown on AA canvas.
    /// </summary>
    public bool ShowNotchToRegularLabels { get; set; } = true;
    /// <summary>
    /// Gets or sets the global UI font size scale in percent.
    /// </summary>
    public double GlobalFontSizePercent { get; set; } = 120.0;
    /// <summary>
    /// Gets or sets staged To Full preview step on AA canvas.
    /// </summary>
    public double NotchPreviewVisualizationStep { get; set; } = 3.0;
    /// <summary>
    /// Gets or sets a value indicating whether Step 3 preview stage auto-play is enabled.
    /// </summary>
    public bool NotchPreviewAutoPlayEnabled { get; set; } = true;
    /// <summary>
    /// Gets or sets Step 3 preview auto-play interval in milliseconds.
    /// </summary>
    public double NotchPreviewAutoPlayIntervalMs { get; set; } = 700.0;
    /// <summary>
    /// Gets or sets Step 5 export file type preference (for example, "Csv", "Cv21", or "Cv22").
    /// </summary>
    public string NotchExportFileType { get; set; } = "Csv";
    /// <summary>
    /// Gets or sets the preferred DXF layer name for export-image dialog defaults.
    /// </summary>
    public string DxfLayerImagePreferredLayer { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets DXF layer image export default width in pixels.
    /// </summary>
    public double DxfLayerImageWidthPixels { get; set; } = 1024.0;
    /// <summary>
    /// Gets or sets DXF layer image export default height in pixels.
    /// </summary>
    public double DxfLayerImageHeightPixels { get; set; } = 1024.0;
    /// <summary>
    /// Gets or sets DXF layer image export default line width in pixels.
    /// </summary>
    public double DxfLayerImageLineWidthPixels { get; set; } = 1.0;
    /// <summary>
    /// Gets or sets DXF layer image export default X padding in pixels.
    /// </summary>
    public double DxfLayerImagePaddingXPixels { get; set; } = 200.0;
    /// <summary>
    /// Gets or sets DXF layer image export default Y padding in pixels.
    /// </summary>
    public double DxfLayerImagePaddingYPixels { get; set; } = 200.0;
    /// <summary>
    /// Gets or sets DXF layer image export default format (for example, "Png").
    /// </summary>
    public string DxfLayerImageFormat { get; set; } = "Png";
    /// <summary>
    /// Gets or sets whether DXF layer image export uses dark theme by default.
    /// </summary>
    public bool DxfLayerImageUseDarkTheme { get; set; }
    /// <summary>
    /// Gets or sets Coordinate page default pixel width.
    /// </summary>
    public double CoordinatePixelWidth { get; set; }
    /// <summary>
    /// Gets or sets Coordinate page default pixel height.
    /// </summary>
    public double CoordinatePixelHeight { get; set; }
    /// <summary>
    /// Gets or sets preferred AA outline layer name for Coordinate page.
    /// </summary>
    public string CoordinatePreferredAaOutlineLayerName { get; set; } = string.Empty;
}

/// <summary>
/// Captures the selection state of a single layer.
/// </summary>
public sealed class LayerSelectionSnapshot
{
    /// <summary>
    /// Gets or sets the name of the layer.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets a value indicating whether the layer is selected (visible/active) in the UI.
    /// </summary>
    public bool IsSelected { get; set; }
}

/// <summary>
/// Captures the UI state related to notch settings.
/// </summary>
public sealed class UiNotchSnapshot
{
    private List<string>? _enabledVersions;

    /// <summary>
    /// Gets or sets legacy UI-only enabled version strings.
    /// Current projects use <see cref="Application.Settings.NotchSettings.EnabledVersions"/> as the only source of truth.
    /// </summary>
    [LegacyUiSnapshotField]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? EnabledVersions
    {
        get => _enabledVersions is null ? null : new List<string>(_enabledVersions);
        set => _enabledVersions = value is null ? null : new List<string>(value);
    }
    /// <summary>
    /// Gets or sets the length scale value displayed in the UI for notch calculations.
    /// </summary>
    public int LenScale { get; set; }
    /// <summary>
    /// Gets or sets the null value representation displayed in the UI for notch tables.
    /// </summary>
    public int NullValue { get; set; }
    /// <summary>
    /// Gets or sets the Q7 threshold (x/128) displayed in the UI for notch filtering.
    /// </summary>
    public int ThresholdQ7 { get; set; }
    /// <summary>
    /// Gets or sets the v2.2 threshold in percent displayed in the UI for notch filtering.
    /// </summary>
    public double ThresholdPercentV22 { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether v2.1/v2.2 thresholds are linked in the UI.
    /// </summary>
    public bool LinkThresholds { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 To Full compensation is enabled in the UI.
    /// </summary>
    public bool EnableToFull { get; set; }
}

/// <summary>
/// Captures the UI state related to DXF import settings.
/// </summary>
public sealed class UiImportSnapshot
{
    /// <summary>
    /// Gets or sets a value indicating whether only closed polylines are imported, as displayed in the UI.
    /// </summary>
    public bool OnlyClosedPolylines { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether BLOCKS-section polylines are imported, as displayed in the UI.
    /// </summary>
    public bool IncludeBlockPolylines { get; set; } = true;

    /// <summary>
    /// Gets or sets the selected minimum log level name (for example, "Info") in the UI.
    /// </summary>
    public string LogLevel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source path of imported SeeRegular.csv used for regular visibility mask.
    /// </summary>
    public string? RegularVisibilityMaskSourcePath { get; set; }

    /// <summary>
    /// Deserializes the pre-rename regular signal mask source path into <see cref="RegularVisibilityMaskSourcePath"/>.
    /// </summary>
    [LegacyUiSnapshotField]
    [JsonPropertyName("regularSignalMaskSourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegularSignalMaskSourcePath
    {
        get => null;
        set
        {
            if (string.IsNullOrWhiteSpace(RegularVisibilityMaskSourcePath))
            {
                RegularVisibilityMaskSourcePath = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether regular visibility mask should be enabled.
    /// </summary>
    public bool UseRegularVisibilityMask { get; set; }

    /// <summary>
    /// Deserializes the pre-rename regular signal mask toggle into <see cref="UseRegularVisibilityMask"/>.
    /// </summary>
    [LegacyUiSnapshotField]
    [JsonPropertyName("useRegularSignalMask")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseRegularSignalMask
    {
        get => null;
        set
        {
            if (value.HasValue)
            {
                UseRegularVisibilityMask = value.Value;
            }
        }
    }
}
