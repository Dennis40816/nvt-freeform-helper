using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

public enum CoordinateArtifactGeometryKind
{
    Point,
    Line,
    Rectangle,
}

public sealed record CoordinateArtifactSnapshot(
    Rect2 ActiveAreaBounds,
    Rect2 GuideBounds,
    string ActiveAreaSourceText,
    string GuideSourceText,
    IReadOnlyList<CoordinateArtifactRow> Rows);

public sealed record CoordinateArtifactRow(
    string Key,
    string Label,
    string Kind,
    string Recipe,
    CoordinateArtifactGeometryKind GeometryKind,
    int SortOrder,
    double PixelX,
    double PixelY,
    double? PixelEndX,
    double? PixelEndY,
    double MachineX,
    double MachineY,
    double? MachineEndX,
    double? MachineEndY,
    double SafeMachineX,
    double SafeMachineY,
    double? SafeMachineEndX,
    double? SafeMachineEndY,
    double WorldX,
    double WorldY,
    double? WorldEndX,
    double? WorldEndY,
    string Unit,
    string SourceBoundsText,
    string SourceText,
    string DetailText)
{
    [JsonIgnore]
    public string KindRecipeText => $"{Kind} - {Recipe}";

    [JsonIgnore]
    public string PixelText => FormatCoordinate(PixelX, PixelY, PixelEndX, PixelEndY);

    [JsonIgnore]
    public string MachineText => FormatCoordinate(MachineX, MachineY, MachineEndX, MachineEndY);

    [JsonIgnore]
    public string SafeMachineText => FormatCoordinate(SafeMachineX, SafeMachineY, SafeMachineEndX, SafeMachineEndY);

    [JsonIgnore]
    public string WorldText => FormatCoordinate(WorldX, WorldY, WorldEndX, WorldEndY);

    [JsonIgnore]
    public double SortPixelX => PixelX;

    [JsonIgnore]
    public double SortPixelY => PixelY;

    [JsonIgnore]
    public double SortMachineX => MachineX;

    [JsonIgnore]
    public double SortMachineY => MachineY;

    [JsonIgnore]
    public double SortSafeMachineX => SafeMachineX;

    [JsonIgnore]
    public double SortSafeMachineY => SafeMachineY;

    private static string FormatCoordinate(double x, double y, double? endX, double? endY)
    {
        var start = FormatPoint(x, y);
        return endX.HasValue && endY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{start} -> {FormatPoint(endX.Value, endY.Value)}")
            : start;
    }

    private static string FormatPoint(double x, double y) =>
        string.Create(CultureInfo.InvariantCulture, $"({x:0.###}, {y:0.###})");
}

public static class CoordinateArtifactService
{
    private const string CoordinateUnitText = "pixel/mm/world";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static CoordinateArtifactSnapshot BuildSnapshot(
        CoordinatePlannerSnapshot snapshot,
        Rect2 guideBounds,
        string activeAreaSourceText,
        string guideSourceText)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var rows = new List<CoordinateArtifactRow>();
        var sortOrder = 0;
        var activeSource = string.IsNullOrWhiteSpace(activeAreaSourceText)
            ? "AA outline"
            : activeAreaSourceText;
        var guideSource = string.IsNullOrWhiteSpace(guideSourceText)
            ? activeSource
            : guideSourceText;
        var activeBoundsText = FormatBounds(snapshot.ActiveAreaBounds);
        var guideBoundsText = FormatBounds(guideBounds);

        foreach (var point in snapshot.Points.Where(static point => point.Kind == CoordinatePlannerPointKind.AaCorner))
        {
            rows.Add(BuildPointArtifactRow(point, "AA corners", "Point", activeSource, activeBoundsText, sortOrder++));
        }

        foreach (var rect in snapshot.Rectangles)
        {
            rows.Add(BuildRectangleArtifactRow(rect, "BIST rectangle", "Rect", activeSource, activeBoundsText, sortOrder++));
        }

        foreach (var point in snapshot.Points.Where(static point => point.Kind == CoordinatePlannerPointKind.BistCorner))
        {
            rows.Add(BuildPointArtifactRow(point, "BIST rectangle", "Point", activeSource, activeBoundsText, sortOrder++));
        }

        foreach (var line in snapshot.Lines.Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide))
        {
            rows.Add(BuildLineArtifactRow(line, "Horizontal guides", "Line", guideSource, guideBoundsText, sortOrder++));
        }

        foreach (var line in snapshot.Lines.Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide))
        {
            rows.Add(BuildLineArtifactRow(line, "Vertical guides", "Line", guideSource, guideBoundsText, sortOrder++));
        }

        foreach (var line in snapshot.Lines.Where(static line => line.Kind == CoordinatePlannerLineKind.CustomArrayEdge))
        {
            rows.Add(BuildLineArtifactRow(line, "4-point array", "Line", "4-point array reference", activeBoundsText, sortOrder++));
        }

        foreach (var point in snapshot.Points.Where(static point => point.Kind == CoordinatePlannerPointKind.CustomArrayCorner))
        {
            rows.Add(BuildPointArtifactRow(point, "4-point array", "Point", "4-point array reference", activeBoundsText, sortOrder++));
        }

        foreach (var point in snapshot.Points.Where(static point => point.Kind == CoordinatePlannerPointKind.CustomArrayDot))
        {
            rows.Add(BuildPointArtifactRow(point, "4-point array", "Dot", "4-point array reference", activeBoundsText, sortOrder++));
        }

        foreach (var point in snapshot.Points.Where(static point => point.Kind == CoordinatePlannerPointKind.CustomPoint))
        {
            rows.Add(BuildPointArtifactRow(point, "Custom point", "Point", "Custom coordinate recipe", activeBoundsText, sortOrder++));
        }

        foreach (var line in snapshot.Lines.Where(static line => line.Kind == CoordinatePlannerLineKind.CustomPath))
        {
            rows.Add(BuildLineArtifactRow(line, "Custom path", "Path", "Custom coordinate recipe", activeBoundsText, sortOrder++));
        }

        foreach (var point in snapshot.Points.Where(static point => point.Kind == CoordinatePlannerPointKind.CustomPathStep))
        {
            rows.Add(BuildPointArtifactRow(point, "Custom path", "Step", "Custom coordinate recipe", activeBoundsText, sortOrder++));
        }

        return new CoordinateArtifactSnapshot(
            snapshot.ActiveAreaBounds,
            guideBounds,
            activeSource,
            guideSource,
            rows);
    }

    public static string ExportCsv(CoordinateArtifactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return ExportCsv(snapshot.Rows);
    }

    public static string ExportCsv(IEnumerable<CoordinateArtifactRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.AppendLine("key,label,kind,recipe,geometry,pixel_x,pixel_y,pixel_end_x,pixel_end_y,machine_x,machine_y,machine_end_x,machine_end_y,safe_machine_x,safe_machine_y,safe_machine_end_x,safe_machine_end_y,world_x,world_y,world_end_x,world_end_y,unit,source_bounds,source,detail");
        foreach (var row in rows)
        {
            AppendCsvCell(builder, row.Key);
            AppendCsvCell(builder, row.Label);
            AppendCsvCell(builder, row.Kind);
            AppendCsvCell(builder, row.Recipe);
            AppendCsvCell(builder, row.GeometryKind.ToString());
            AppendCsvCell(builder, row.PixelX);
            AppendCsvCell(builder, row.PixelY);
            AppendCsvCell(builder, row.PixelEndX);
            AppendCsvCell(builder, row.PixelEndY);
            AppendCsvCell(builder, row.MachineX);
            AppendCsvCell(builder, row.MachineY);
            AppendCsvCell(builder, row.MachineEndX);
            AppendCsvCell(builder, row.MachineEndY);
            AppendCsvCell(builder, row.SafeMachineX);
            AppendCsvCell(builder, row.SafeMachineY);
            AppendCsvCell(builder, row.SafeMachineEndX);
            AppendCsvCell(builder, row.SafeMachineEndY);
            AppendCsvCell(builder, row.WorldX);
            AppendCsvCell(builder, row.WorldY);
            AppendCsvCell(builder, row.WorldEndX);
            AppendCsvCell(builder, row.WorldEndY);
            AppendCsvCell(builder, row.Unit);
            AppendCsvCell(builder, row.SourceBoundsText);
            AppendCsvCell(builder, row.SourceText);
            AppendCsvCell(builder, row.DetailText, endOfRow: true);
        }

        return builder.ToString();
    }

    public static string ExportJson(CoordinateArtifactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static string ExportClipboardTable(IEnumerable<CoordinateArtifactRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.AppendLine("Label\tKind\tRecipe\tPixel\tMachine\tSafe machine\tWorld\tSource bounds\tSource");
        foreach (var row in rows)
        {
            builder
                .Append(row.Label).Append('\t')
                .Append(row.Kind).Append('\t')
                .Append(row.Recipe).Append('\t')
                .Append(row.PixelText).Append('\t')
                .Append(row.MachineText).Append('\t')
                .Append(row.SafeMachineText).Append('\t')
                .Append(row.WorldText).Append('\t')
                .Append(row.SourceBoundsText).Append('\t')
                .Append(row.SourceText)
                .AppendLine();
        }

        return builder.ToString();
    }

    private static CoordinateArtifactRow BuildPointArtifactRow(
        CoordinatePlannerPoint point,
        string recipe,
        string kind,
        string sourceText,
        string sourceBoundsText,
        int sortOrder)
    {
        var pixelText = FormatPoint(point.PixelX, point.PixelY);
        var machineText = FormatPoint(point.MachineX, point.MachineY);
        var safeMachineText = FormatPoint(point.SafeMachineX, point.SafeMachineY);
        var worldText = FormatPoint(point.World.X, point.World.Y);

        return new CoordinateArtifactRow(
            point.Key,
            point.Label,
            kind,
            recipe,
            CoordinateArtifactGeometryKind.Point,
            sortOrder,
            point.PixelX,
            point.PixelY,
            null,
            null,
            point.MachineX,
            point.MachineY,
            null,
            null,
            point.SafeMachineX,
            point.SafeMachineY,
            null,
            null,
            point.World.X,
            point.World.Y,
            null,
            null,
            CoordinateUnitText,
            sourceBoundsText,
            sourceText,
            $"Pixel {pixelText}; machine {machineText}; safe {safeMachineText}; world {worldText}.");
    }

    private static CoordinateArtifactRow BuildLineArtifactRow(
        CoordinatePlannerLine line,
        string recipe,
        string kind,
        string sourceText,
        string sourceBoundsText,
        int sortOrder)
    {
        var pixelText = FormatSegment(line.StartPixelX, line.StartPixelY, line.EndPixelX, line.EndPixelY);
        var machineText = FormatSegment(line.StartMachineX, line.StartMachineY, line.EndMachineX, line.EndMachineY);
        var safeMachineText = FormatSegment(line.SafeStartMachineX, line.SafeStartMachineY, line.SafeEndMachineX, line.SafeEndMachineY);
        var worldText = FormatSegment(line.StartWorld.X, line.StartWorld.Y, line.EndWorld.X, line.EndWorld.Y);

        return new CoordinateArtifactRow(
            line.Key,
            line.Label,
            kind,
            recipe,
            CoordinateArtifactGeometryKind.Line,
            sortOrder,
            line.StartPixelX,
            line.StartPixelY,
            line.EndPixelX,
            line.EndPixelY,
            line.StartMachineX,
            line.StartMachineY,
            line.EndMachineX,
            line.EndMachineY,
            line.SafeStartMachineX,
            line.SafeStartMachineY,
            line.SafeEndMachineX,
            line.SafeEndMachineY,
            line.StartWorld.X,
            line.StartWorld.Y,
            line.EndWorld.X,
            line.EndWorld.Y,
            CoordinateUnitText,
            sourceBoundsText,
            sourceText,
            $"Source {sourceText}; bounds {sourceBoundsText}; pixel {pixelText}; machine {machineText}; safe {safeMachineText}; world {worldText}.");
    }

    private static CoordinateArtifactRow BuildRectangleArtifactRow(
        CoordinatePlannerRectangle rect,
        string recipe,
        string kind,
        string sourceText,
        string sourceBoundsText,
        int sortOrder)
    {
        var pixelText = FormatSegment(rect.PixelLeft, rect.PixelTop, rect.PixelRight, rect.PixelBottom);
        var machineText = FormatSegment(rect.MachineLeft, rect.MachineTop, rect.MachineRight, rect.MachineBottom);
        var safeMachineText = FormatSegment(rect.SafeMachineLeft, rect.SafeMachineTop, rect.SafeMachineRight, rect.SafeMachineBottom);
        var worldText = FormatSegment(rect.WorldBounds.MinX, rect.WorldBounds.MaxY, rect.WorldBounds.MaxX, rect.WorldBounds.MinY);

        return new CoordinateArtifactRow(
            rect.Key,
            rect.Label,
            kind,
            recipe,
            CoordinateArtifactGeometryKind.Rectangle,
            sortOrder,
            rect.PixelLeft,
            rect.PixelTop,
            rect.PixelRight,
            rect.PixelBottom,
            rect.MachineLeft,
            rect.MachineTop,
            rect.MachineRight,
            rect.MachineBottom,
            rect.SafeMachineLeft,
            rect.SafeMachineTop,
            rect.SafeMachineRight,
            rect.SafeMachineBottom,
            rect.WorldBounds.MinX,
            rect.WorldBounds.MaxY,
            rect.WorldBounds.MaxX,
            rect.WorldBounds.MinY,
            CoordinateUnitText,
            sourceBoundsText,
            sourceText,
            $"Source {sourceText}; bounds {sourceBoundsText}; pixel {pixelText}; machine {machineText}; safe {safeMachineText}; world {worldText}.");
    }

    private static void AppendCsvCell(StringBuilder builder, string value, bool endOfRow = false)
    {
        builder.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
        if (endOfRow)
        {
            builder.AppendLine();
            return;
        }

        builder.Append(',');
    }

    private static void AppendCsvCell(StringBuilder builder, double value, bool endOfRow = false) =>
        AppendCsvCell(builder, FormatNumber(value), endOfRow);

    private static void AppendCsvCell(StringBuilder builder, double? value, bool endOfRow = false) =>
        AppendCsvCell(builder, value.HasValue ? FormatNumber(value.Value) : string.Empty, endOfRow);

    private static string FormatBounds(Rect2 bounds) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"({bounds.MinX:0.###}, {bounds.MinY:0.###}) -> ({bounds.MaxX:0.###}, {bounds.MaxY:0.###})");

    private static string FormatPoint(double x, double y) =>
        string.Create(CultureInfo.InvariantCulture, $"({x:0.###}, {y:0.###})");

    private static string FormatSegment(double startX, double startY, double endX, double endY) =>
        string.Create(CultureInfo.InvariantCulture, $"({startX:0.###}, {startY:0.###}) -> ({endX:0.###}, {endY:0.###})");

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
