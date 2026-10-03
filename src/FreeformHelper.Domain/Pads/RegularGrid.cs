using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// Represents a structured grid of <see cref="RegularPad"/> objects.
/// The grid can have varying row heights and column widths.
/// </summary>
public sealed class RegularGrid
{
    /// <summary>
    /// Gets the number of rows in the grid.
    /// </summary>
    public int Rows { get; }
    /// <summary>
    /// Gets the number of columns in the grid.
    /// </summary>
    public int Cols { get; }
    /// <summary>
    /// Gets a read-only list of the X-coordinates of the vertical grid lines (column edges).
    /// </summary>
    public IReadOnlyList<double> XEdges { get; }
    /// <summary>
    /// Gets a read-only list of the Y-coordinates of the horizontal grid lines (row edges).
    /// </summary>
    public IReadOnlyList<double> YEdges { get; }
    /// <summary>
    /// Gets a read-only list of all <see cref="RegularPad"/> objects in the grid,
    /// ordered row by row, then column by column.
    /// </summary>
    public IReadOnlyList<RegularPad> Pads { get; }
    /// <summary>
    /// Gets the overall bounding rectangle that encloses all pads in this grid.
    /// </summary>
    public Rect2 Bounds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegularGrid"/> class.
    /// </summary>
    /// <param name="rows">The number of rows in the grid.</param>
    /// <param name="cols">The number of columns in the grid.</param>
    /// <param name="xEdges">The X-coordinates of the vertical grid lines.</param>
    /// <param name="yEdges">The Y-coordinates of the horizontal grid lines.</param>
    /// <param name="pads">The list of <see cref="RegularPad"/> objects comprising the grid.</param>
    public RegularGrid(int rows, int cols, IReadOnlyList<double> xEdges, IReadOnlyList<double> yEdges, IReadOnlyList<RegularPad> pads)
    {
        Rows = rows;
        Cols = cols;
        XEdges = xEdges;
        YEdges = yEdges;
        Pads = pads;
        Bounds = ComputeBounds(pads);
    }

    /// <summary>
    /// Retrieves a specific <see cref="RegularPad"/> from the grid using its row and column indices.
    /// </summary>
    /// <param name="row">The zero-based row index of the pad.</param>
    /// <param name="col">The zero-based column index of the pad.</param>
    /// <returns>The <see cref="RegularPad"/> at the specified grid position.</returns>
    public RegularPad GetPad(int row, int col) => Pads[row * Cols + col];

    /// <summary>
    /// Computes the union of the bounding boxes of all regular pads in the grid.
    /// </summary>
    /// <param name="pads">The list of regular pads.</param>
    /// <returns>A <see cref="Rect2"/> representing the overall bounding box, or an empty rect if no pads.</returns>
    private static Rect2 ComputeBounds(IReadOnlyList<RegularPad> pads)
    {
        Rect2 b = default; // Initialize with default (empty) Rect2
        var has = false; // Flag to track if any pads have been processed
        foreach (var p in pads)
        {
            // If this is the first pad, initialize 'b' with its bounds.
            // Otherwise, union 'b' with the current pad's bounds.
            b = has ? Rect2.Union(b, p.Bounds) : p.Bounds;
            has = true;
        }
        // If no pads were present, return a zero-sized rectangle.
        return has ? b : new Rect2(0, 0, 0, 0);
    }
}
