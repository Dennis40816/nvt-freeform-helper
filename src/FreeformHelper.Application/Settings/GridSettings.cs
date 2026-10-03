namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines all settings related to the generation and configuration of the regular grid.
/// This includes dimensions, channel allocation, sizing overrides, and alignment.
/// </summary>
public sealed class GridSettings
{
    private List<int> _perIcXChannels = new();
    private List<int> _perIcYChannels = new();
    private List<double> _columnWidths = new();
    private List<bool> _columnOverrides = new();
    private List<double> _rowHeights = new();
    private List<bool> _rowOverrides = new();
    private List<List<double>> _rowWidthOverrides = new();
    private List<List<bool>> _rowWidthOverrideFlags = new();
    private List<List<double>> _columnHeightOverrides = new();
    private List<List<bool>> _columnHeightOverrideFlags = new();

    /// <summary>
    /// Legacy default channel limit used by older project files.
    /// </summary>
    public const int LegacyChannelLimit = 256;

    /// <summary>
    /// Current default soft channel limit. Set to <see cref="int.MaxValue"/> to avoid
    /// truncating user-requested channel counts in modern workflows.
    /// </summary>
    public const int DefaultChannelLimit = int.MaxValue;

    /// <summary>
    /// Gets or sets the total number of columns (X channels) in the regular grid.
    /// Default is 32. Must be positive.
    /// </summary>
    public int XChannels { get; set; } = 32;

    /// <summary>
    /// Gets or sets the total number of rows (Y channels) in the regular grid.
    /// Default is 20. Must be positive.
    /// </summary>
    public int YChannels { get; set; } = 20;

    /// <summary>
    /// Gets or sets the scope for adjusting column widths.
    /// <list type="bullet">
    /// <item><description><see cref="WidthAdjustmentScope.ColumnGlobal"/>: Widths are applied globally across all rows.</description></item>
    /// <item><description><see cref="WidthAdjustmentScope.RowLocal"/>: Widths can be defined per row.</description></item>
    /// </list>
    /// Default is <see cref="WidthAdjustmentScope.RowLocal"/>.
    /// </summary>
    public WidthAdjustmentScope WidthScope { get; set; } = WidthAdjustmentScope.RowLocal;

    /// <summary>
    /// Gets or sets the scope for adjusting row heights.
    /// <list type="bullet">
    /// <item><description><see cref="HeightAdjustmentScope.RowGlobal"/>: Heights are applied globally across all columns.</description></item>
    /// <item><description><see cref="HeightAdjustmentScope.ColumnLocal"/>: Heights can be defined per column.</description></item>
    /// </list>
    /// Default is <see cref="HeightAdjustmentScope.ColumnLocal"/>.
    /// </summary>
    public HeightAdjustmentScope HeightScope { get; set; } = HeightAdjustmentScope.ColumnLocal;

    /// <summary>
    /// Gets or sets the number of cascaded ICs (Integrated Circuits).
    /// Default is 1. Must be positive and not exceed <see cref="XChannels"/>.
    /// </summary>
    public int CascadeNum { get; set; } = 1;

    /// <summary>
    /// Gets or sets an optional list defining the number of X channels allocated to each cascaded IC.
    /// If the list is empty or its sum does not equal <see cref="XChannels"/> or its length does not equal <see cref="CascadeNum"/>,
    /// an equal split strategy is used instead.
    /// </summary>
    public List<int> PerIcXChannels
    {
        get => _perIcXChannels;
        set => _perIcXChannels = NormalizePerIcChannels(value);
    }

    /// <summary>
    /// Gets or sets an optional list defining the number of Y channels allocated to each cascaded IC.
    /// This property is maintained for potential future panel variants or specific hardware configurations.
    /// Currently, its length should equal <see cref="CascadeNum"/>.
    /// </summary>
    public List<int> PerIcYChannels
    {
        get => _perIcYChannels;
        set => _perIcYChannels = NormalizePerIcChannels(value);
    }

    public void ReplacePerIcChannels(IEnumerable<int>? perIcXChannels, IEnumerable<int>? perIcYChannels)
    {
        _perIcXChannels = NormalizePerIcChannels(perIcXChannels);
        _perIcYChannels = NormalizePerIcChannels(perIcYChannels);
    }

    /// <summary>
    /// Gets or sets how the regular grid's position is determined.
    /// <list type="bullet">
    /// <item><description><see cref="GridAlignmentMode.FromPanelAa"/>: Aligns the grid based on panel active area bias settings.</description></item>
    /// <item><description><see cref="GridAlignmentMode.FromCadBounds"/>: Aligns the grid to the bounding box of the imported CAD data.</description></item>
    /// </list>
    /// Default is <see cref="GridAlignmentMode.FromCadBounds"/>.
    /// </summary>
    public GridAlignmentMode AlignmentMode { get; set; } = GridAlignmentMode.FromCadBounds;

    /// <summary>
    /// Gets or sets the scanning order for AFE (Analog Front-End) index assignment.
    /// This affects how pads are numbered within the grid.
    /// Default is <see cref="ScanOrder.LeftToRight_TopToBottom"/>.
    /// </summary>
    public ScanOrder ScanOrder { get; set; } = ScanOrder.LeftToRight_TopToBottom;

    /// <summary>
    /// (Unused) Reserved for future firmware indexing behavior.
    /// This is not exposed in the UI and is currently treated as 0.
    /// </summary>
    public int AfeOverlap { get; set; }

    /// <summary>
    /// Gets or sets how regular pads are sourced.
    /// Default is <see cref="RegularSourceMode.GeneratedGrid"/>.
    /// </summary>
    public RegularSourceMode RegularSourceMode { get; set; } = RegularSourceMode.GeneratedGrid;

    /// <summary>
    /// Optional DXF layer name used when <see cref="RegularSourceMode"/> is
    /// <see cref="RegularSourceMode.FromDxfLayer"/>.
    /// </summary>
    public string? RegularSourceLayerName { get; set; }

    /// <summary>
    /// Gets or sets a list of explicit widths for each column.
    /// If this list is empty or its count does not match <see cref="XChannels"/>,
    /// uniform widths are calculated based on the total grid width.
    /// Values are in world units (e.g., millimeters), consistent with DXF data.
    /// </summary>
    public List<double> ColumnWidths
    {
        get => _columnWidths;
        set => _columnWidths = NormalizeDoubleList(value);
    }

    /// <summary>
    /// Gets or sets a list of flags indicating whether a specific column's width has been
    /// manually overridden by the user. A value of <c>true</c> means the width is user-defined.
    /// </summary>
    public List<bool> ColumnOverrides
    {
        get => _columnOverrides;
        set => _columnOverrides = NormalizeBoolList(value);
    }

    /// <summary>
    /// Gets or sets a list of explicit heights for each row.
    /// If this list is empty or its count does not match <see cref="YChannels"/>,
    /// uniform heights are calculated based on the total grid height.
    /// Values are in world units (e.g., millimeters), consistent with DXF data.
    /// </summary>
    public List<double> RowHeights
    {
        get => _rowHeights;
        set => _rowHeights = NormalizeDoubleList(value);
    }

    /// <summary>
    /// Gets or sets a list of flags indicating whether a specific row's height has been
    /// manually overridden by the user. A value of <c>true</c> means the height is user-defined.
    /// </summary>
    public List<bool> RowOverrides
    {
        get => _rowOverrides;
        set => _rowOverrides = NormalizeBoolList(value);
    }

    /// <summary>
    /// Gets or sets a list of lists representing per-row column width overrides.
    /// The outer list is indexed by row, and each inner list contains widths for columns in that row.
    /// This is used when <see cref="WidthScope"/> is <see cref="WidthAdjustmentScope.RowLocal"/>.
    /// </summary>
    public List<List<double>> RowWidthOverrides
    {
        get => _rowWidthOverrides;
        set => _rowWidthOverrides = NormalizeJaggedDoubleList(value);
    }

    /// <summary>
    /// Gets or sets a list of lists representing flags for per-row column width overrides.
    /// The outer list is indexed by row, and each inner list contains boolean flags for columns in that row.
    /// A flag of <c>true</c> indicates a user-defined width for that specific cell.
    /// </summary>
    public List<List<bool>> RowWidthOverrideFlags
    {
        get => _rowWidthOverrideFlags;
        set => _rowWidthOverrideFlags = NormalizeJaggedBoolList(value);
    }

    /// <summary>
    /// Gets or sets a list of lists representing per-column row height overrides.
    /// The outer list is indexed by column, and each inner list contains heights for rows in that column.
    /// This is used when <see cref="HeightScope"/> is <see cref="HeightAdjustmentScope.ColumnLocal"/>.
    /// </summary>
    public List<List<double>> ColumnHeightOverrides
    {
        get => _columnHeightOverrides;
        set => _columnHeightOverrides = NormalizeJaggedDoubleList(value);
    }

    /// <summary>
    /// Gets or sets a list of lists representing flags for per-column row height overrides.
    /// The outer list is indexed by column, and each inner list contains boolean flags for rows in that column.
    /// A flag of <c>true</c> indicates a user-defined height for that specific cell.
    /// </summary>
    public List<List<bool>> ColumnHeightOverrideFlags
    {
        get => _columnHeightOverrideFlags;
        set => _columnHeightOverrideFlags = NormalizeJaggedBoolList(value);
    }

    public void ReplaceSizingCollections(
        IEnumerable<double>? columnWidths,
        IEnumerable<double>? rowHeights,
        IEnumerable<bool>? columnOverrides,
        IEnumerable<bool>? rowOverrides,
        IEnumerable<IEnumerable<double>>? rowWidthOverrides,
        IEnumerable<IEnumerable<bool>>? rowWidthOverrideFlags,
        IEnumerable<IEnumerable<double>>? columnHeightOverrides,
        IEnumerable<IEnumerable<bool>>? columnHeightOverrideFlags)
    {
        _columnWidths = NormalizeDoubleList(columnWidths);
        _rowHeights = NormalizeDoubleList(rowHeights);
        _columnOverrides = NormalizeBoolList(columnOverrides);
        _rowOverrides = NormalizeBoolList(rowOverrides);
        _rowWidthOverrides = NormalizeJaggedDoubleList(rowWidthOverrides);
        _rowWidthOverrideFlags = NormalizeJaggedBoolList(rowWidthOverrideFlags);
        _columnHeightOverrides = NormalizeJaggedDoubleList(columnHeightOverrides);
        _columnHeightOverrideFlags = NormalizeJaggedBoolList(columnHeightOverrideFlags);
    }

    /// <summary>
    /// Gets or sets a ratio to add extra padding around the CAD bounds when
    /// generating a grid from CAD data. This expands the grid beyond the CAD's extent.
    /// Default is 0.0 (no extra padding).
    /// </summary>
    public double BoundsPaddingRatio { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed number of X channels (columns) as a safeguard.
    /// Default is effectively unbounded for current versions.
    /// </summary>
    public int MaxChannelsX { get; set; } = DefaultChannelLimit;

    /// <summary>
    /// Gets or sets the maximum allowed number of Y channels (rows) as a safeguard.
    /// Default is effectively unbounded for current versions.
    /// </summary>
    public int MaxChannelsY { get; set; } = DefaultChannelLimit;

    /// <summary>
    /// Gets or sets the total width of the active area for the full panel.
    /// Used when <see cref="AlignmentMode"/> is <see cref="GridAlignmentMode.FromPanelAa"/>.
    /// Value is in millimeters. Default is 310.0 mm.
    /// </summary>
    public double ActiveAreaWidth { get; set; } = 310.0;

    /// <summary>
    /// Gets or sets the total height of the active area for the full panel.
    /// Used when <see cref="AlignmentMode"/> is <see cref="GridAlignmentMode.FromPanelAa"/>.
    /// Value is in millimeters. Default is 174.0 mm.
    /// </summary>
    public double ActiveAreaHeight { get; set; } = 174.0;

    /// <summary>
    /// Gets or sets an optional X-axis bias for grid alignment relative to the panel's active area.
    /// This is only considered when <see cref="AlignmentMode"/> is <see cref="GridAlignmentMode.FromPanelAa"/>.
    /// Value is in millimeters. Default is 0.0.
    /// </summary>
    public double PanelBiasX { get; set; }

    /// <summary>
    /// Gets or sets an optional Y-axis bias for grid alignment relative to the panel's active area.
    /// This is only considered when <see cref="AlignmentMode"/> is <see cref="GridAlignmentMode.FromPanelAa"/>.
    /// Value is in millimeters. Default is 0.0.
    /// </summary>
    public double PanelBiasY { get; set; }

    /// <summary>
    /// Optional DXF layer name used to derive bounds when <see cref="AlignmentMode"/> is
    /// <see cref="GridAlignmentMode.FromCadBounds"/>. When empty, bounds follow the CAD output set.
    /// </summary>
    public string? BoundLayerName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CAD bounds should be recalculated from
    /// visible layers when layer filters are toggled or the grid is rebuilt.
    /// Default is <c>true</c>.
    /// </summary>
    public bool RecalcBoundsOnLayerFilter { get; set; } = true;

    /// <summary>
    /// Validates the current settings and throws an <see cref="InvalidOperationException"/> if any are invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="XChannels"/> or <see cref="YChannels"/> are not positive,
    /// exceed maximum limits, <see cref="CascadeNum"/> is not positive or exceeds <see cref="XChannels"/>,
    /// or <see cref="ActiveAreaWidth"/> or <see cref="ActiveAreaHeight"/> are negative.
    /// </exception>
    public void ValidateOrThrow()
    {
        if (XChannels <= 0 || YChannels <= 0) throw new InvalidOperationException("XChannels/YChannels must be positive.");
        if (XChannels > MaxChannelsX || YChannels > MaxChannelsY) throw new InvalidOperationException("Channels exceed configured max limit.");
        if (CascadeNum <= 0) throw new InvalidOperationException("CascadeNum must be positive.");
        if (CascadeNum > XChannels) throw new InvalidOperationException("CascadeNum cannot exceed XChannels.");
        if (ActiveAreaWidth < 0 || ActiveAreaHeight < 0) throw new InvalidOperationException("Active area must be non-negative.");
        if (AfeOverlap < 0) throw new InvalidOperationException("AfeOverlap must be non-negative.");
    }

    private static List<int> NormalizePerIcChannels(IEnumerable<int>? channels)
    {
        return channels is null
            ? new List<int>()
            : new List<int>(channels);
    }

    private static List<double> NormalizeDoubleList(IEnumerable<double>? values)
    {
        return values is null
            ? new List<double>()
            : new List<double>(values);
    }

    private static List<bool> NormalizeBoolList(IEnumerable<bool>? values)
    {
        return values is null
            ? new List<bool>()
            : new List<bool>(values);
    }

    private static List<List<double>> NormalizeJaggedDoubleList(IEnumerable<IEnumerable<double>>? values)
    {
        if (values is null)
        {
            return new List<List<double>>();
        }

        var normalized = new List<List<double>>();
        foreach (var inner in values)
        {
            normalized.Add(inner is null ? new List<double>() : new List<double>(inner));
        }

        return normalized;
    }

    private static List<List<bool>> NormalizeJaggedBoolList(IEnumerable<IEnumerable<bool>>? values)
    {
        if (values is null)
        {
            return new List<List<bool>>();
        }

        var normalized = new List<List<bool>>();
        foreach (var inner in values)
        {
            normalized.Add(inner is null ? new List<bool>() : new List<bool>(inner));
        }

        return normalized;
    }
}

/// <summary>
/// Defines how width adjustments are applied across the grid.
/// </summary>
public enum WidthAdjustmentScope
{
    /// <summary>
    /// Column widths are determined globally and applied uniformly across all rows.
    /// </summary>
    ColumnGlobal = 0,
    /// <summary>
    /// Column widths can be defined independently for each row.
    /// </summary>
    RowLocal = 1,
}

/// <summary>
/// Defines how height adjustments are applied across the grid.
/// </summary>
public enum HeightAdjustmentScope
{
    /// <summary>
    /// Row heights are determined globally and applied uniformly across all columns.
    /// </summary>
    RowGlobal = 0,
    /// <summary>
    /// Row heights can be defined independently for each column.
    /// </summary>
    ColumnLocal = 1,
}
