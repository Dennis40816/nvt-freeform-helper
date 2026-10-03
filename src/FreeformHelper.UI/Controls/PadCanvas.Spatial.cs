using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// This partial class of <see cref="PadCanvas"/> is responsible for managing
/// spatial data structures for efficient querying of regular pads.
/// </summary>
public sealed partial class PadCanvas
{
    /// <summary>
    /// Ensures that the <see cref="CadSpatialIndex"/> is initialized and populated.
    /// This index is used for fast hit-testing and rectangle queries of CAD pads.
    /// </summary>
    private void EnsureCadSpatialIndex()
    {
        if (_cadIndex is not null)
        {
            return;
        }

        if (CadPads is not { Count: > 0 })
        {
            _cadIndex = null;
            return;
        }

        _cadIndex = new CadSpatialIndex(CadPads);
    }

    /// <summary>
    /// Ensures that the <see cref="RegularSpatialIndex"/> is initialized and populated.
    /// This index is used for fast hit-testing and querying of regular pads.
    /// </summary>
    private void EnsureRegularSpatialIndex()
    {
        // If the index is already built, no need to rebuild.
        if (_regularIndex is not null)
        {
            return;
        }

        // If there are no regular pads, clear the index and return.
        if (RegularPads is not { Count: > 0 })
        {
            _regularIndex = null;
            return;
        }

        // Build a new spatial index from the current collection of regular pads.
        // Some manual sizing modes can produce non-separable row/column boundaries.
        // The current index model assumes separable X/Y edges, so disable it when unsafe.
        var index = new RegularSpatialIndex(RegularPads);
        _regularIndex = index.SupportsExactQueries ? index : null;
    }

    /// <summary>
    /// A coarse spatial index for CAD pads. It indexes pad bounds into fixed-size buckets
    /// so hit-test and box-select avoid scanning the full CAD set on every interaction.
    /// </summary>
    private sealed class CadSpatialIndex
    {
        private const double EdgeEpsilon = 1e-9;
        private const int MinAxisBuckets = 8;
        private const int MaxAxisBuckets = 128;

        private readonly IReadOnlyList<CadPad> _allPads;
        private readonly List<CadPad>?[] _buckets;
        private readonly Rect2 _bounds;
        private readonly int _rows;
        private readonly int _cols;
        private readonly double _cellWidth;
        private readonly double _cellHeight;

        public CadSpatialIndex(IReadOnlyList<CadPad> pads)
        {
            _allPads = pads;
            _bounds = BuildBounds(pads);

            if (pads.Count == 0 || _bounds.Width <= EdgeEpsilon || _bounds.Height <= EdgeEpsilon)
            {
                _rows = 1;
                _cols = 1;
                _cellWidth = Math.Max(_bounds.Width, EdgeEpsilon);
                _cellHeight = Math.Max(_bounds.Height, EdgeEpsilon);
                _buckets = new List<CadPad>?[1];
                if (pads.Count > 0)
                {
                    _buckets[0] = pads.ToList();
                }

                return;
            }

            var axisTarget = Math.Clamp((int)Math.Ceiling(Math.Sqrt(pads.Count)), MinAxisBuckets, MaxAxisBuckets);
            var aspect = Math.Clamp(_bounds.Width / Math.Max(_bounds.Height, EdgeEpsilon), 0.25, 4.0);
            _cols = Math.Clamp((int)Math.Round(axisTarget * Math.Sqrt(aspect)), MinAxisBuckets, MaxAxisBuckets);
            _rows = Math.Clamp((int)Math.Round(axisTarget / Math.Sqrt(aspect)), MinAxisBuckets, MaxAxisBuckets);
            _cellWidth = Math.Max(_bounds.Width / _cols, EdgeEpsilon);
            _cellHeight = Math.Max(_bounds.Height / _rows, EdgeEpsilon);
            _buckets = new List<CadPad>?[_rows * _cols];

            foreach (var pad in pads)
            {
                var bounds = pad.Bounds;
                var minCol = GetColumn(bounds.MinX);
                var maxCol = GetColumn(bounds.MaxX);
                var minRow = GetRow(bounds.MinY);
                var maxRow = GetRow(bounds.MaxY);
                for (var row = minRow; row <= maxRow; row++)
                {
                    for (var col = minCol; col <= maxCol; col++)
                    {
                        AddBucketPad(row, col, pad);
                    }
                }
            }
        }

        public IEnumerable<CadPad> Query(Point2 world)
        {
            if (_allPads.Count == 0 || !_bounds.Contains(world))
            {
                yield break;
            }

            if (_rows == 1 && _cols == 1)
            {
                foreach (var pad in _allPads)
                {
                    yield return pad;
                }

                yield break;
            }

            var row = GetRow(world.Y);
            var col = GetColumn(world.X);
            var bucket = _buckets[(row * _cols) + col];
            if (bucket is null)
            {
                yield break;
            }

            foreach (var pad in bucket)
            {
                yield return pad;
            }
        }

        public IEnumerable<CadPad> Query(Rect2 worldRect)
        {
            if (_allPads.Count == 0 || !_bounds.Intersects(worldRect))
            {
                yield break;
            }

            if (_rows == 1 && _cols == 1)
            {
                foreach (var pad in _allPads)
                {
                    if (pad.Bounds.Intersects(worldRect))
                    {
                        yield return pad;
                    }
                }

                yield break;
            }

            var minCol = GetColumn(worldRect.MinX);
            var maxCol = GetColumn(worldRect.MaxX);
            var minRow = GetRow(worldRect.MinY);
            var maxRow = GetRow(worldRect.MaxY);
            var seenPadIds = new HashSet<int>();
            for (var row = minRow; row <= maxRow; row++)
            {
                for (var col = minCol; col <= maxCol; col++)
                {
                    var bucket = _buckets[(row * _cols) + col];
                    if (bucket is null)
                    {
                        continue;
                    }

                    foreach (var pad in bucket)
                    {
                        if (!seenPadIds.Add(pad.Id))
                        {
                            continue;
                        }

                        if (pad.Bounds.Intersects(worldRect))
                        {
                            yield return pad;
                        }
                    }
                }
            }
        }

        private static Rect2 BuildBounds(IReadOnlyList<CadPad> pads)
        {
            if (pads.Count == 0)
            {
                return new Rect2(0, 0, 0, 0);
            }

            var bounds = pads[0].Bounds;
            for (var i = 1; i < pads.Count; i++)
            {
                bounds = Rect2.Union(bounds, pads[i].Bounds);
            }

            return bounds;
        }

        private void AddBucketPad(int row, int col, CadPad pad)
        {
            var idx = (row * _cols) + col;
            var bucket = _buckets[idx];
            if (bucket is null)
            {
                bucket = new List<CadPad>();
                _buckets[idx] = bucket;
            }

            bucket.Add(pad);
        }

        private int GetColumn(double x)
        {
            if (_cols <= 1)
            {
                return 0;
            }

            if (x <= _bounds.MinX)
            {
                return 0;
            }

            if (x >= _bounds.MaxX)
            {
                return _cols - 1;
            }

            var normalized = (x - _bounds.MinX) / _cellWidth;
            return Math.Clamp((int)Math.Floor(normalized), 0, _cols - 1);
        }

        private int GetRow(double y)
        {
            if (_rows <= 1)
            {
                return 0;
            }

            if (y <= _bounds.MinY)
            {
                return 0;
            }

            if (y >= _bounds.MaxY)
            {
                return _rows - 1;
            }

            var normalized = (y - _bounds.MinY) / _cellHeight;
            return Math.Clamp((int)Math.Floor(normalized), 0, _rows - 1);
        }
    }

    /// <summary>
    /// A spatial index specifically designed for efficiently querying <see cref="RegularPad"/> objects.
    /// It uses a grid-like structure to map world coordinates to pad instances.
    /// </summary>
    private sealed class RegularSpatialIndex
    {
        private const double EdgeEpsilon = 1e-6;
        // Internal 2D array representing the grid of pads.
        private readonly RegularPad?[,] _grid;
        // Arrays storing the X-coordinates of the vertical grid lines (column edges).
        private readonly double[] _xEdges;
        // Arrays storing the Y-coordinates of the horizontal grid lines (row edges).
        private readonly double[] _yEdges;

        /// <summary>
        /// Gets the number of rows in the spatial index grid.
        /// </summary>
        public int Rows { get; }
        /// <summary>
        /// Gets the number of columns in the spatial index grid.
        /// </summary>
        public int Cols { get; }
        /// <summary>
        /// Gets a value indicating whether this index can answer queries exactly for the current grid geometry.
        /// </summary>
        public bool SupportsExactQueries { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegularSpatialIndex"/> class.
        /// Constructs the spatial grid from a flat list of regular pads.
        /// </summary>
        /// <param name="pads">A read-only list of <see cref="RegularPad"/> objects.</param>
        public RegularSpatialIndex(IReadOnlyList<RegularPad> pads)
        {
            // Determine the minimum row and column indices to normalize the grid.
            var minRow = pads.Min(p => p.Row);
            var minCol = pads.Min(p => p.Col);

            // Calculate the dimensions of the internal grid.
            Rows = pads.Max(p => p.Row) - minRow + 1;
            Cols = pads.Max(p => p.Col) - minCol + 1;

            _grid = new RegularPad?[Rows, Cols];
            _xEdges = new double[Cols + 1]; // +1 for the last edge.
            _yEdges = new double[Rows + 1];

            // Temporary arrays to store min/max X/Y coordinates for each column/row,
            // used to derive the grid edges.
            var colMin = Enumerable.Repeat(double.PositiveInfinity, Cols).ToArray();
            var colMax = Enumerable.Repeat(double.NegativeInfinity, Cols).ToArray();
            var rowMin = Enumerable.Repeat(double.PositiveInfinity, Rows).ToArray();
            var rowMax = Enumerable.Repeat(double.NegativeInfinity, Rows).ToArray();

            // Populate the internal grid and update min/max coordinates for edge calculation.
            foreach (var pad in pads)
            {
                var r = pad.Row - minRow; // Normalized row index.
                var c = pad.Col - minCol; // Normalized column index.

                // Bounds check for safety, though pads should typically fit.
                if ((uint)r >= (uint)Rows || (uint)c >= (uint)Cols)
                {
                    continue;
                }

                _grid[r, c] = pad; // Place pad in the grid.

                var b = pad.Bounds;
                // Update min/max bounds for the current column and row.
                colMin[c] = Math.Min(colMin[c], b.MinX);
                colMax[c] = Math.Max(colMax[c], b.MaxX);
                rowMin[r] = Math.Min(rowMin[r], b.MinY);
                rowMax[r] = Math.Max(rowMax[r], b.MaxY);
            }

            SupportsExactQueries = ValidateSeparableGrid(pads, minRow, minCol, colMin, colMax, rowMin, rowMax);

            // Build _xEdges: The left edge of each column (colMin) and the right edge of the last column (colMax).
            _xEdges[0] = colMin[0];
            for (var c = 1; c < Cols; c++)
            {
                _xEdges[c] = colMin[c];
            }
            _xEdges[Cols] = colMax[Cols - 1]; // Last edge is the right edge of the last column.

            // Build _yEdges: The bottom edge of each row (rowMin) and the top edge of the last row (rowMax).
            _yEdges[0] = rowMin[0];
            for (var r = 1; r < Rows; r++)
            {
                _yEdges[r] = rowMin[r];
            }
            _yEdges[Rows] = rowMax[Rows - 1]; // Last edge is the top edge of the last row.
        }

        private static bool ValidateSeparableGrid(
            IReadOnlyList<RegularPad> pads,
            int minRow,
            int minCol,
            double[] colMin,
            double[] colMax,
            double[] rowMin,
            double[] rowMax)
        {
            foreach (var pad in pads)
            {
                var r = pad.Row - minRow;
                var c = pad.Col - minCol;
                if ((uint)r >= (uint)rowMin.Length || (uint)c >= (uint)colMin.Length)
                {
                    return false;
                }

                var bounds = pad.Bounds;
                if (Math.Abs(bounds.MinX - colMin[c]) > EdgeEpsilon ||
                    Math.Abs(bounds.MaxX - colMax[c]) > EdgeEpsilon ||
                    Math.Abs(bounds.MinY - rowMin[r]) > EdgeEpsilon ||
                    Math.Abs(bounds.MaxY - rowMax[r]) > EdgeEpsilon)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Attempts to find a <see cref="RegularPad"/> at the given world coordinate point.
        /// </summary>
        /// <param name="world">The world coordinate point.</param>
        /// <returns>The <see cref="RegularPad"/> at the point, or <c>null</c> if no pad is found.</returns>
        public RegularPad? TryHit(Point2 world)
        {
            // Find the column index corresponding to the world X-coordinate.
            var col = FindBucket(_xEdges, world.X);
            if (col < 0) return null; // Outside grid horizontally.

            // Find the row index corresponding to the world Y-coordinate.
            var row = FindBucket(_yEdges, world.Y);
            if (row < 0) return null; // Outside grid vertically.

            // Return the pad from the grid at the found (row, col) position.
            return _grid[row, col];
        }

        /// <summary>
        /// Queries the spatial index for all <see cref="RegularPad"/> objects that intersect a given rectangle.
        /// </summary>
        /// <param name="rect">The query rectangle in world coordinates.</param>
        /// <returns>An enumerable collection of intersecting <see cref="RegularPad"/> objects.</returns>
        public IEnumerable<RegularPad> Query(Rect2 rect)
        {
            // Determine the range of columns (c0 to c1) that potentially intersect the query rectangle.
            var c0 = FindFirstCell(_xEdges, rect.MinX);
            var c1 = FindLastCell(_xEdges, rect.MaxX);
            // Determine the range of rows (r0 to r1) that potentially intersect the query rectangle.
            var r0 = FindFirstCell(_yEdges, rect.MinY);
            var r1 = FindLastCell(_yEdges, rect.MaxY);

            // If the ranges are invalid (e.g., max < min), no intersection.
            if (c1 < c0 || r1 < r0)
            {
                yield break;
            }

            // Iterate through the identified grid cells.
            for (var r = r0; r <= r1; r++)
            {
                for (var c = c0; c <= c1; c++)
                {
                    var pad = _grid[r, c];
                    if (pad is null) continue; // Skip empty cells.
                    if (!pad.Bounds.Intersects(rect)) continue; // Refine with actual pad bounds intersection.
                    yield return pad; // Return intersecting pad.
                }
            }
        }

        /// <summary>
        /// Finds the bucket (cell index) within a set of sorted edges that contains a given value.
        /// </summary>
        /// <param name="edges">Sorted array of edge coordinates.</param>
        /// <param name="v">The value to locate.</param>
        /// <returns>The index of the bucket, or -1 if the value is outside the range of edges.</returns>
        private static int FindBucket(double[] edges, double v)
        {
            // Returns i such that edges[i] <= v <= edges[i+1].
            if (v < edges[0] || v > edges[^1]) return -1; // Value is outside the total range.

            var idx = Array.BinarySearch(edges, v);
            if (idx >= 0)
            {
                // If value is exactly on an edge, prefer the bucket to its left (for discrete indexing).
                // Handle the case where it's the very last edge specifically.
                return Math.Clamp(idx == edges.Length - 1 ? idx - 1 : idx, 0, edges.Length - 2);
            }

            // If not exact match, bitwise complement of 'idx' gives insertion point.
            // This insertion point is effectively the right edge of the bucket containing 'v'.
            idx = ~idx;
            return Math.Clamp(idx - 1, 0, edges.Length - 2);
        }

        /// <summary>
        /// Finds the first cell (inclusive) in the grid along an axis that is intersected by a given minimum coordinate.
        /// </summary>
        private static int FindFirstCell(double[] edges, double min)
        {
            if (min <= edges[0]) return 0; // If min is before or at the first edge, it's the first cell.
            if (min >= edges[^1]) return edges.Length - 2; // If min is beyond the last edge, it's the last cell.

            var idx = Array.BinarySearch(edges, min);
            if (idx < 0) idx = ~idx; // If not found, get insertion point.
            return Math.Clamp(idx - 1, 0, edges.Length - 2); // Return the cell index.
        }

        /// <summary>
        /// Finds the last cell (inclusive) in the grid along an axis that is intersected by a given maximum coordinate.
        /// </summary>
        private static int FindLastCell(double[] edges, double max)
        {
            if (max <= edges[0]) return 0; // If max is before or at the first edge, it's the first cell.
            if (max >= edges[^1]) return edges.Length - 2; // If max is beyond the last edge, it's the last cell.

            var idx = Array.BinarySearch(edges, max);
            if (idx < 0) idx = ~idx - 1; // If not found, get insertion point and adjust for 'last' cell.
            return Math.Clamp(idx, 0, edges.Length - 2); // Return the cell index.
        }
    }
}
