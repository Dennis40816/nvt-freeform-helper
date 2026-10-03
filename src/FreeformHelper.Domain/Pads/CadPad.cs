using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// Represents a single pad entity imported directly from CAD (e.g., DXF) data.
/// These pads can have arbitrary polygonal shapes.
/// </summary>
public sealed class CadPad
{
    /// <summary>
    /// Gets the unique identifier for this CAD pad.
    /// </summary>
    public int Id { get; }
    /// <summary>
    /// Gets the name or designation of this CAD pad.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the CAD layer on which this pad resides.
    /// </summary>
    public string Layer { get; }
    /// <summary>
    /// Gets the polygonal shape of this CAD pad.
    /// </summary>
    public Polygon2 Polygon { get; }

    /// <summary>
    /// Gets the axis-aligned bounding box of the CAD pad's polygon.
    /// </summary>
    public Rect2 Bounds => Polygon.Bounds;
    /// <summary>
    /// Gets the area of the CAD pad's polygon.
    /// </summary>
    public double Area => Polygon.Area();
    /// <summary>
    /// Gets the centroid (geometric center) of the CAD pad's polygon.
    /// </summary>
    public Point2 Centroid => Polygon.Centroid();

    /// <summary>
    /// Initializes a new instance of the <see cref="CadPad"/> class.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="name">The name of the pad.</param>
    /// <param name="layer">The CAD layer.</param>
    /// <param name="polygon">The polygonal geometry of the pad.</param>
    public CadPad(int id, string name, string layer, Polygon2 polygon)
    {
        Id = id;
        Name = name;
        Layer = layer;
        Polygon = polygon;
    }
}
