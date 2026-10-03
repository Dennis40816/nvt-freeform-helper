using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Infrastructure.Project;

public sealed class ProjectDxfCombinedCadGroup
{
    private List<int> _sourceCadIds = new();
    private List<ProjectCadPadSnapshot> _outputPads = new();

    public int GroupId { get; set; }

    public List<int> SourceCadIds
    {
        get => _sourceCadIds;
        set => _sourceCadIds = value is null ? new List<int>() : new List<int>(value);
    }

    public List<ProjectCadPadSnapshot> OutputPads
    {
        get => _outputPads;
        set => _outputPads = value is null ? new List<ProjectCadPadSnapshot>() : new List<ProjectCadPadSnapshot>(value);
    }
}

public sealed class ProjectCadPadSnapshot
{
    private List<Point2> _vertices = new();

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Layer { get; set; } = string.Empty;

    public List<Point2> Vertices
    {
        get => _vertices;
        set => _vertices = value is null ? new List<Point2>() : new List<Point2>(value);
    }

    public static ProjectCadPadSnapshot FromCadPad(CadPad pad)
    {
        ArgumentNullException.ThrowIfNull(pad);

        return new ProjectCadPadSnapshot
        {
            Id = pad.Id,
            Name = pad.Name,
            Layer = pad.Layer,
            Vertices = pad.Polygon.Vertices.ToList(),
        };
    }

    public CadPad ToCadPad()
    {
        return new CadPad(Id, Name, Layer, new Polygon2(Vertices));
    }
}

public sealed class ProjectCadGeometrySnapshot
{
    private List<Point2> _vertices = new();

    public List<Point2> Vertices
    {
        get => _vertices;
        set => _vertices = value is null ? new List<Point2>() : new List<Point2>(value);
    }

    public static ProjectCadGeometrySnapshot FromCadPad(CadPad pad)
    {
        ArgumentNullException.ThrowIfNull(pad);

        return new ProjectCadGeometrySnapshot
        {
            Vertices = pad.Polygon.Vertices.ToList(),
        };
    }

    public Polygon2 ToPolygon()
    {
        return new Polygon2(Vertices);
    }
}
