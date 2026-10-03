using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// Represents an immutable collection of <see cref="CadPad"/> objects, typically
/// all the pads imported from a single CAD file or relevant layers.
/// </summary>
public sealed class CadPadSet
{
    /// <summary>
    /// Gets the read-only list of <see cref="CadPad"/> objects in this set.
    /// </summary>
    public IReadOnlyList<CadPad> Pads { get; }
    /// <summary>
    /// Gets the overall bounding rectangle that encloses all pads in this set.
    /// </summary>
    public Rect2 Bounds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CadPadSet"/> class.
    /// </summary>
    /// <param name="pads">A read-only list of <see cref="CadPad"/> objects.</param>
    public CadPadSet(IReadOnlyList<CadPad> pads)
    {
        Pads = pads;
        Bounds = ComputeBounds(pads);
    }

    /// <summary>
    /// Computes the union of the bounding boxes of all pads in the set.
    /// </summary>
    /// <param name="pads">The list of CAD pads.</param>
    /// <returns>A <see cref="Rect2"/> representing the overall bounding box, or an empty rect if no pads.</returns>
    private static Rect2 ComputeBounds(IReadOnlyList<CadPad> pads)
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
