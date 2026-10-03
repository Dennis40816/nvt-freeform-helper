using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// Represents a single pad within the generated regular grid.
/// These pads are typically rectangular and are derived from the grid's dimensions.
/// </summary>
public sealed class RegularPad
{
    /// <summary>
    /// Gets the zero-based row index of this pad within the grid.
    /// </summary>
    public int Row { get; }
    /// <summary>
    /// Gets the zero-based column index of this pad within the grid.
    /// </summary>
    public int Col { get; }
    /// <summary>
    /// Gets a unique, linear index for this pad, often calculated as (row * totalColumns + col).
    /// Legacy name kept for compatibility. Prefer <see cref="RegularPadId"/> for new code.
    /// </summary>
    public int Index { get; }
    /// <summary>
    /// Gets the stable unique identifier of this regular pad.
    /// Alias of <see cref="Index"/> introduced to reduce naming ambiguity.
    /// </summary>
    public int RegularPadId => Index;
    /// <summary>
    /// Gets the polygonal shape of this regular pad (typically a rectangle).
    /// </summary>
    public Polygon2 Polygon { get; }

    /// <summary>
    /// Gets the axis-aligned bounding box of the regular pad's polygon.
    /// This will typically be the pad's own boundaries.
    /// </summary>
    public Rect2 Bounds => Polygon.Bounds;
    /// <summary>
    /// Gets the area of the regular pad's polygon.
    /// </summary>
    public double Area => Polygon.Area();
    /// <summary>
    /// Gets the centroid (geometric center) of the regular pad's polygon.
    /// </summary>
    public Point2 Centroid => Polygon.Centroid();

    /// <summary>
    /// Gets or sets the zero-based index of the IC (Integrated Circuit) this pad is mapped to.
    /// This is determined by the AFE mapping logic.
    /// </summary>
    public int IcIndex { get; set; }
    /// <summary>
    /// Gets or sets the contiguous FW diff index for this regular pad across the whole grid (no overlap).
    /// This is a per-IC, 0-based index that follows the configured scan order.
    /// Combine <see cref="IcIndex"/> + <see cref="RegularFwDiffIndex"/> for stable channel identity.
    /// </summary>
    public int DiffIndex { get; set; }

    /// <summary>
    /// Gets or sets the FW memory diff index represented by this regular pad.
    /// Alias of <see cref="DiffIndex"/> introduced to make regular/FW identity explicit.
    /// </summary>
    public int RegularFwDiffIndex
    {
        get => DiffIndex;
        set => DiffIndex = value;
    }

    /// <summary>
    /// Gets or sets the ID of the <see cref="CadPad"/> that this regular pad was matched to.
    /// Null if no matching CAD pad was found.
    /// </summary>
    public int? MatchedCadPadId { get; set; }
    /// <summary>
    /// Gets or sets the match score, indicating the quality of the match between this regular pad
    /// and its <see cref="MatchedCadPadId"/>. Typically based on overlap area or other metrics.
    /// </summary>
    public double MatchScore { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="FreeformType"/> of this pad, indicating if it's part of a
    /// larger freeform structure spanning multiple regular pads.
    /// Default is <see cref="FreeformType.None"/>.
    /// </summary>
    public FreeformType Freeform { get; set; } = FreeformType.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegularPad"/> class.
    /// </summary>
    /// <param name="row">The row index of the pad.</param>
    /// <param name="col">The column index of the pad.</param>
    /// <param name="index">The linear index of the pad.</param>
    /// <param name="polygon">The polygonal geometry of the pad.</param>
    public RegularPad(int row, int col, int index, Polygon2 polygon)
    {
        Row = row;
        Col = col;
        Index = index;
        Polygon = polygon;
    }
}
