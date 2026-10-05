using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed record CoordinatePlannerRequest(
    double MachineOriginX,
    double MachineOriginY,
    double MachineWidth,
    double MachineHeight,
    int PixelWidth,
    int PixelHeight,
    int HorizontalGuideCount,
    int VerticalGuideCount,
    double CopperPillarDiameter,
    bool ShowBistRectangle,
    bool ShowCustomArray,
    int CustomArrayColumnCount,
    int CustomArrayRowCount,
    double CustomArrayTopLeftMachineX,
    double CustomArrayTopLeftMachineY,
    double CustomArrayTopRightMachineX,
    double CustomArrayTopRightMachineY,
    double CustomArrayBottomRightMachineX,
    double CustomArrayBottomRightMachineY,
    double CustomArrayBottomLeftMachineX,
    double CustomArrayBottomLeftMachineY,
    CoordinateGuideGenerationMode HorizontalGuideMode = CoordinateGuideGenerationMode.Count,
    CoordinateGuideGenerationMode VerticalGuideMode = CoordinateGuideGenerationMode.Count,
    double HorizontalGuidePitch = 0d,
    double VerticalGuidePitch = 0d,
    double HorizontalGuideInset = 0d,
    double VerticalGuideInset = 0d,
    IReadOnlyList<double>? HorizontalGuidePositions = null,
    IReadOnlyList<double>? VerticalGuidePositions = null,
    IReadOnlyList<CoordinateCustomPointRequest>? CustomPoints = null,
    IReadOnlyList<CoordinateCustomPathRequest>? CustomPaths = null);

public sealed record CoordinateCustomPointRequest(
    string Key,
    string Label,
    double MachineX,
    double MachineY);

public sealed record CoordinateCustomPathRequest(
    string Key,
    string Label,
    double StartMachineX,
    double StartMachineY,
    double EndMachineX,
    double EndMachineY,
    int StepCount);

public enum CoordinateGuideGenerationMode
{
    Count,
    Pitch,
    ExplicitPositions,
}

public enum CoordinatePlannerPointKind
{
    AaCorner,
    BistCorner,
    CustomArrayCorner,
    CustomArrayDot,
    CustomPoint,
    CustomPathStep,
}

public enum CoordinatePlannerLineKind
{
    HorizontalGuide,
    VerticalGuide,
    CustomArrayEdge,
    CustomPath,
}

public enum CoordinatePlannerRectangleKind
{
    BistCenter,
}

public sealed record CoordinatePlannerPoint(
    string Key,
    string Label,
    CoordinatePlannerPointKind Kind,
    Point2 World,
    Point2 SafeWorld,
    double PixelX,
    double PixelY,
    double MachineX,
    double MachineY,
    double SafeMachineX,
    double SafeMachineY);

public sealed record CoordinatePlannerLine(
    string Key,
    string Label,
    CoordinatePlannerLineKind Kind,
    Point2 StartWorld,
    Point2 EndWorld,
    Point2 SafeStartWorld,
    Point2 SafeEndWorld,
    double StartPixelX,
    double StartPixelY,
    double EndPixelX,
    double EndPixelY,
    double StartMachineX,
    double StartMachineY,
    double EndMachineX,
    double EndMachineY,
    double SafeStartMachineX,
    double SafeStartMachineY,
    double SafeEndMachineX,
    double SafeEndMachineY);

public sealed record CoordinatePlannerRectangle(
    string Key,
    string Label,
    CoordinatePlannerRectangleKind Kind,
    Rect2 WorldBounds,
    Rect2 SafeWorldBounds,
    double PixelLeft,
    double PixelTop,
    double PixelRight,
    double PixelBottom,
    double MachineLeft,
    double MachineTop,
    double MachineRight,
    double MachineBottom,
    double SafeMachineLeft,
    double SafeMachineTop,
    double SafeMachineRight,
    double SafeMachineBottom);

public sealed record CoordinatePlannerSnapshot(
    Rect2 ActiveAreaBounds,
    double CopperPillarRadiusMachine,
    double WorldPillarRadiusX,
    double WorldPillarRadiusY,
    IReadOnlyList<CoordinatePlannerPoint> Points,
    IReadOnlyList<CoordinatePlannerLine> Lines,
    IReadOnlyList<CoordinatePlannerRectangle> Rectangles);

public static class CoordinatePlannerComputationService
{
    private readonly record struct MachinePoint(double X, double Y);

    public static CoordinatePlannerSnapshot BuildSnapshot(
        RegularGrid grid,
        CoordinatePlannerRequest request)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(request);
        if (grid.Pads.Count == 0)
        {
            throw new InvalidOperationException("Coordinate planner requires a non-empty regular grid.");
        }

        var aaBounds = ComputeBounds(grid.Pads);
        return BuildSnapshot(aaBounds, aaBounds, request);
    }

    public static CoordinatePlannerSnapshot BuildSnapshot(
        Rect2 activeAreaBounds,
        CoordinatePlannerRequest request)
    {
        return BuildSnapshot(activeAreaBounds, activeAreaBounds, request);
    }

    public static CoordinatePlannerSnapshot BuildSnapshot(
        Rect2 activeAreaBounds,
        Rect2 guideBounds,
        CoordinatePlannerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var clampedPixelWidth = Math.Max(1, request.PixelWidth);
        var clampedPixelHeight = Math.Max(1, request.PixelHeight);
        var clampedMachineWidth = Math.Max(1e-9, request.MachineWidth);
        var clampedMachineHeight = Math.Max(1e-9, request.MachineHeight);
        var pillarRadius = Math.Max(0d, request.CopperPillarDiameter * 0.5d);
        var worldPillarRadiusX = pillarRadius * activeAreaBounds.Width / clampedMachineWidth;
        var worldPillarRadiusY = pillarRadius * activeAreaBounds.Height / clampedMachineHeight;

        var points = new List<CoordinatePlannerPoint>();
        var lines = new List<CoordinatePlannerLine>();
        var rectangles = new List<CoordinatePlannerRectangle>();

        AddAaCorner(points, activeAreaBounds, request, clampedPixelWidth, clampedPixelHeight, "aa-tl", "AA TL", 0d, 0d);
        AddAaCorner(points, activeAreaBounds, request, clampedPixelWidth, clampedPixelHeight, "aa-tr", "AA TR", 1d, 0d);
        AddAaCorner(points, activeAreaBounds, request, clampedPixelWidth, clampedPixelHeight, "aa-br", "AA BR", 1d, 1d);
        AddAaCorner(points, activeAreaBounds, request, clampedPixelWidth, clampedPixelHeight, "aa-bl", "AA BL", 0d, 1d);

        AddHorizontalGuides(lines, guideBounds, request, clampedPixelWidth, clampedPixelHeight);
        AddVerticalGuides(lines, guideBounds, request, clampedPixelWidth, clampedPixelHeight);

        if (request.ShowBistRectangle)
        {
            AddBistRectangle(rectangles, points, activeAreaBounds, request, clampedPixelWidth, clampedPixelHeight);
        }

        if (request.ShowCustomArray)
        {
            AddCustomArray(lines, points, activeAreaBounds, request, clampedPixelWidth, clampedPixelHeight);
        }

        AddCustomCoordinates(lines, points, activeAreaBounds, request, clampedPixelWidth, clampedPixelHeight);

        return new CoordinatePlannerSnapshot(
            activeAreaBounds,
            pillarRadius,
            worldPillarRadiusX,
            worldPillarRadiusY,
            points,
            lines,
            rectangles);
    }

    private static Rect2 ComputeBounds(IReadOnlyList<RegularPad> pads)
    {
        var bounds = pads[0].Bounds;
        for (var i = 1; i < pads.Count; i++)
        {
            bounds = Rect2.Union(bounds, pads[i].Bounds);
        }

        return bounds;
    }

    private static void AddAaCorner(
        List<CoordinatePlannerPoint> points,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight,
        string key,
        string label,
        double normX,
        double normY)
    {
        points.Add(BuildPoint(
            key,
            label,
            CoordinatePlannerPointKind.AaCorner,
            aaBounds,
            request,
            pixelWidth,
            pixelHeight,
            normX,
            normY));
    }

    private static void AddHorizontalGuides(
        List<CoordinatePlannerLine> lines,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight)
    {
        var positions = BuildGuideNormalizedPositions(
            request.HorizontalGuideMode,
            request.HorizontalGuideCount,
            request.MachineHeight,
            request.HorizontalGuidePitch,
            request.HorizontalGuideInset,
            request.HorizontalGuidePositions);
        for (var i = 0; i < positions.Length; i++)
        {
            var normY = positions[i];
            lines.Add(BuildLine(
                $"h-{i + 1}",
                $"H{i + 1}",
                CoordinatePlannerLineKind.HorizontalGuide,
                aaBounds,
                request,
                pixelWidth,
                pixelHeight,
                0d,
                normY,
                1d,
                normY));
        }
    }

    private static void AddVerticalGuides(
        List<CoordinatePlannerLine> lines,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight)
    {
        var positions = BuildGuideNormalizedPositions(
            request.VerticalGuideMode,
            request.VerticalGuideCount,
            request.MachineWidth,
            request.VerticalGuidePitch,
            request.VerticalGuideInset,
            request.VerticalGuidePositions);
        for (var i = 0; i < positions.Length; i++)
        {
            var normX = positions[i];
            lines.Add(BuildLine(
                $"v-{i + 1}",
                $"V{i + 1}",
                CoordinatePlannerLineKind.VerticalGuide,
                aaBounds,
                request,
                pixelWidth,
                pixelHeight,
                normX,
                0d,
                normX,
                1d));
        }
    }

    private static void AddBistRectangle(
        List<CoordinatePlannerRectangle> rectangles,
        List<CoordinatePlannerPoint> points,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight)
    {
        const double leftNorm = 0.25d;
        const double rightNorm = 0.75d;
        const double topNorm = 0.25d;
        const double bottomNorm = 0.75d;

        var topLeft = Transform(aaBounds, request, pixelWidth, pixelHeight, leftNorm, topNorm, fromMachine: false);
        var bottomRight = Transform(aaBounds, request, pixelWidth, pixelHeight, rightNorm, bottomNorm, fromMachine: false);
        var safeMachineRect = ClampMachineRect(topLeft.Machine.X, topLeft.Machine.Y, bottomRight.Machine.X, bottomRight.Machine.Y, request);
        var safeTopLeft = Transform(aaBounds, request, pixelWidth, pixelHeight, safeMachineRect.left, safeMachineRect.top, fromMachine: true);
        var safeBottomRight = Transform(aaBounds, request, pixelWidth, pixelHeight, safeMachineRect.right, safeMachineRect.bottom, fromMachine: true);

        var rect = new CoordinatePlannerRectangle(
            Key: "bist-center",
            Label: "BIST center blank rect",
            Kind: CoordinatePlannerRectangleKind.BistCenter,
            WorldBounds: BuildWorldRect(topLeft.World, bottomRight.World),
            SafeWorldBounds: BuildWorldRect(safeTopLeft.World, safeBottomRight.World),
            PixelLeft: topLeft.PixelX,
            PixelTop: topLeft.PixelY,
            PixelRight: bottomRight.PixelX,
            PixelBottom: bottomRight.PixelY,
            MachineLeft: topLeft.Machine.X,
            MachineTop: topLeft.Machine.Y,
            MachineRight: bottomRight.Machine.X,
            MachineBottom: bottomRight.Machine.Y,
            SafeMachineLeft: safeMachineRect.left,
            SafeMachineTop: safeMachineRect.top,
            SafeMachineRight: safeMachineRect.right,
            SafeMachineBottom: safeMachineRect.bottom);
        rectangles.Add(rect);

        points.Add(BuildPoint("bist-tl", "BIST TL", CoordinatePlannerPointKind.BistCorner, aaBounds, request, pixelWidth, pixelHeight, leftNorm, topNorm));
        points.Add(BuildPoint("bist-tr", "BIST TR", CoordinatePlannerPointKind.BistCorner, aaBounds, request, pixelWidth, pixelHeight, rightNorm, topNorm));
        points.Add(BuildPoint("bist-br", "BIST BR", CoordinatePlannerPointKind.BistCorner, aaBounds, request, pixelWidth, pixelHeight, rightNorm, bottomNorm));
        points.Add(BuildPoint("bist-bl", "BIST BL", CoordinatePlannerPointKind.BistCorner, aaBounds, request, pixelWidth, pixelHeight, leftNorm, bottomNorm));
    }

    private static void AddCustomArray(
        List<CoordinatePlannerLine> lines,
        List<CoordinatePlannerPoint> points,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight)
    {
        var topLeft = new MachinePoint(request.CustomArrayTopLeftMachineX, request.CustomArrayTopLeftMachineY);
        var topRight = new MachinePoint(request.CustomArrayTopRightMachineX, request.CustomArrayTopRightMachineY);
        var bottomRight = new MachinePoint(request.CustomArrayBottomRightMachineX, request.CustomArrayBottomRightMachineY);
        var bottomLeft = new MachinePoint(request.CustomArrayBottomLeftMachineX, request.CustomArrayBottomLeftMachineY);

        var pillarRadius = Math.Max(0d, request.CopperPillarDiameter * 0.5d);
        var uInset = ComputeNormalizedInset(pillarRadius, topLeft, topRight, bottomLeft, bottomRight, horizontal: true);
        var vInset = ComputeNormalizedInset(pillarRadius, topLeft, topRight, bottomLeft, bottomRight, horizontal: false);

        var safeTopLeft = ClampMachinePoint(Bilinear(topLeft, topRight, bottomRight, bottomLeft, uInset, vInset), request);
        var safeTopRight = ClampMachinePoint(Bilinear(topLeft, topRight, bottomRight, bottomLeft, 1d - uInset, vInset), request);
        var safeBottomRight = ClampMachinePoint(Bilinear(topLeft, topRight, bottomRight, bottomLeft, 1d - uInset, 1d - vInset), request);
        var safeBottomLeft = ClampMachinePoint(Bilinear(topLeft, topRight, bottomRight, bottomLeft, uInset, 1d - vInset), request);

        lines.Add(BuildLineFromMachine("array-top", "Array top", CoordinatePlannerLineKind.CustomArrayEdge, aaBounds, request, pixelWidth, pixelHeight, topLeft, topRight, safeTopLeft, safeTopRight));
        lines.Add(BuildLineFromMachine("array-right", "Array right", CoordinatePlannerLineKind.CustomArrayEdge, aaBounds, request, pixelWidth, pixelHeight, topRight, bottomRight, safeTopRight, safeBottomRight));
        lines.Add(BuildLineFromMachine("array-bottom", "Array bottom", CoordinatePlannerLineKind.CustomArrayEdge, aaBounds, request, pixelWidth, pixelHeight, bottomLeft, bottomRight, safeBottomLeft, safeBottomRight));
        lines.Add(BuildLineFromMachine("array-left", "Array left", CoordinatePlannerLineKind.CustomArrayEdge, aaBounds, request, pixelWidth, pixelHeight, topLeft, bottomLeft, safeTopLeft, safeBottomLeft));

        points.Add(BuildPointFromMachine("array-tl", "Array TL", CoordinatePlannerPointKind.CustomArrayCorner, aaBounds, request, pixelWidth, pixelHeight, topLeft, safeTopLeft));
        points.Add(BuildPointFromMachine("array-tr", "Array TR", CoordinatePlannerPointKind.CustomArrayCorner, aaBounds, request, pixelWidth, pixelHeight, topRight, safeTopRight));
        points.Add(BuildPointFromMachine("array-br", "Array BR", CoordinatePlannerPointKind.CustomArrayCorner, aaBounds, request, pixelWidth, pixelHeight, bottomRight, safeBottomRight));
        points.Add(BuildPointFromMachine("array-bl", "Array BL", CoordinatePlannerPointKind.CustomArrayCorner, aaBounds, request, pixelWidth, pixelHeight, bottomLeft, safeBottomLeft));

        var columnCount = Math.Max(0, request.CustomArrayColumnCount);
        var rowCount = Math.Max(0, request.CustomArrayRowCount);
        if (columnCount == 0 || rowCount == 0)
        {
            return;
        }

        for (var row = 0; row < rowCount; row++)
        {
            var rawV = GetArrayNormalizedPosition(row, rowCount);
            var safeV = GetArrayNormalizedPosition(row, rowCount, vInset);
            for (var column = 0; column < columnCount; column++)
            {
                var rawU = GetArrayNormalizedPosition(column, columnCount);
                var safeU = GetArrayNormalizedPosition(column, columnCount, uInset);
                var rawPoint = Bilinear(topLeft, topRight, bottomRight, bottomLeft, rawU, rawV);
                var safePoint = ClampMachinePoint(Bilinear(topLeft, topRight, bottomRight, bottomLeft, safeU, safeV), request);
                points.Add(BuildPointFromMachine(
                    $"array-dot-r{row + 1}-c{column + 1}",
                    $"P{row + 1}-{column + 1}",
                    CoordinatePlannerPointKind.CustomArrayDot,
                    aaBounds,
                    request,
                    pixelWidth,
                    pixelHeight,
                    rawPoint,
                    safePoint));
            }
        }
    }

    private static void AddCustomCoordinates(
        List<CoordinatePlannerLine> lines,
        List<CoordinatePlannerPoint> points,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight)
    {
        if (request.CustomPoints is not null)
        {
            foreach (var point in request.CustomPoints)
            {
                if (string.IsNullOrWhiteSpace(point.Key))
                {
                    continue;
                }

                var machinePoint = new MachinePoint(point.MachineX, point.MachineY);
                var safeMachinePoint = ClampMachinePoint(machinePoint, request);
                points.Add(BuildPointFromMachine(
                    $"custom-point-{point.Key}",
                    string.IsNullOrWhiteSpace(point.Label) ? "Custom point" : point.Label,
                    CoordinatePlannerPointKind.CustomPoint,
                    aaBounds,
                    request,
                    pixelWidth,
                    pixelHeight,
                    machinePoint,
                    safeMachinePoint));
            }
        }

        if (request.CustomPaths is null)
        {
            return;
        }

        foreach (var path in request.CustomPaths)
        {
            if (string.IsNullOrWhiteSpace(path.Key))
            {
                continue;
            }

            var start = new MachinePoint(path.StartMachineX, path.StartMachineY);
            var end = new MachinePoint(path.EndMachineX, path.EndMachineY);
            var safeStart = ClampMachinePoint(start, request);
            var safeEnd = ClampMachinePoint(end, request);
            var pathKey = $"custom-path-{path.Key}";
            var label = string.IsNullOrWhiteSpace(path.Label) ? "Custom path" : path.Label;
            lines.Add(BuildLineFromMachine(
                pathKey,
                label,
                CoordinatePlannerLineKind.CustomPath,
                aaBounds,
                request,
                pixelWidth,
                pixelHeight,
                start,
                end,
                safeStart,
                safeEnd));

            var stepCount = Math.Max(0, path.StepCount);
            for (var index = 0; index < stepCount; index++)
            {
                var t = GetArrayNormalizedPosition(index, stepCount);
                var rawStep = Lerp(start, end, t);
                var safeStep = ClampMachinePoint(rawStep, request);
                points.Add(BuildPointFromMachine(
                    $"{pathKey}-step-{index + 1}",
                    $"{label} P{index + 1}",
                    CoordinatePlannerPointKind.CustomPathStep,
                    aaBounds,
                    request,
                    pixelWidth,
                    pixelHeight,
                    rawStep,
                    safeStep));
            }
        }
    }

    private static CoordinatePlannerPoint BuildPoint(
        string key,
        string label,
        CoordinatePlannerPointKind kind,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight,
        double normX,
        double normY)
    {
        var raw = Transform(aaBounds, request, pixelWidth, pixelHeight, normX, normY, fromMachine: false);
        var safeMachine = ClampMachinePoint(raw.Machine, request);
        var safe = Transform(aaBounds, request, pixelWidth, pixelHeight, safeMachine.X, safeMachine.Y, fromMachine: true);
        return new CoordinatePlannerPoint(
            Key: key,
            Label: label,
            Kind: kind,
            World: raw.World,
            SafeWorld: safe.World,
            PixelX: raw.PixelX,
            PixelY: raw.PixelY,
            MachineX: raw.Machine.X,
            MachineY: raw.Machine.Y,
            SafeMachineX: safe.Machine.X,
            SafeMachineY: safe.Machine.Y);
    }

    private static CoordinatePlannerPoint BuildPointFromMachine(
        string key,
        string label,
        CoordinatePlannerPointKind kind,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight,
        MachinePoint machinePoint,
        MachinePoint safeMachinePoint)
    {
        var raw = Transform(aaBounds, request, pixelWidth, pixelHeight, machinePoint.X, machinePoint.Y, fromMachine: true);
        var safe = Transform(aaBounds, request, pixelWidth, pixelHeight, safeMachinePoint.X, safeMachinePoint.Y, fromMachine: true);
        return new CoordinatePlannerPoint(
            Key: key,
            Label: label,
            Kind: kind,
            World: raw.World,
            SafeWorld: safe.World,
            PixelX: raw.PixelX,
            PixelY: raw.PixelY,
            MachineX: raw.Machine.X,
            MachineY: raw.Machine.Y,
            SafeMachineX: safe.Machine.X,
            SafeMachineY: safe.Machine.Y);
    }

    private static CoordinatePlannerLine BuildLine(
        string key,
        string label,
        CoordinatePlannerLineKind kind,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight,
        double startNormX,
        double startNormY,
        double endNormX,
        double endNormY)
    {
        var start = Transform(aaBounds, request, pixelWidth, pixelHeight, startNormX, startNormY, fromMachine: false);
        var end = Transform(aaBounds, request, pixelWidth, pixelHeight, endNormX, endNormY, fromMachine: false);
        var safeStartMachine = ClampMachinePoint(start.Machine, request);
        var safeEndMachine = ClampMachinePoint(end.Machine, request);
        var safeStart = Transform(aaBounds, request, pixelWidth, pixelHeight, safeStartMachine.X, safeStartMachine.Y, fromMachine: true);
        var safeEnd = Transform(aaBounds, request, pixelWidth, pixelHeight, safeEndMachine.X, safeEndMachine.Y, fromMachine: true);

        return new CoordinatePlannerLine(
            Key: key,
            Label: label,
            Kind: kind,
            StartWorld: start.World,
            EndWorld: end.World,
            SafeStartWorld: safeStart.World,
            SafeEndWorld: safeEnd.World,
            StartPixelX: start.PixelX,
            StartPixelY: start.PixelY,
            EndPixelX: end.PixelX,
            EndPixelY: end.PixelY,
            StartMachineX: start.Machine.X,
            StartMachineY: start.Machine.Y,
            EndMachineX: end.Machine.X,
            EndMachineY: end.Machine.Y,
            SafeStartMachineX: safeStart.Machine.X,
            SafeStartMachineY: safeStart.Machine.Y,
            SafeEndMachineX: safeEnd.Machine.X,
            SafeEndMachineY: safeEnd.Machine.Y);
    }

    private static CoordinatePlannerLine BuildLineFromMachine(
        string key,
        string label,
        CoordinatePlannerLineKind kind,
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight,
        MachinePoint startMachinePoint,
        MachinePoint endMachinePoint,
        MachinePoint safeStartMachinePoint,
        MachinePoint safeEndMachinePoint)
    {
        var start = Transform(aaBounds, request, pixelWidth, pixelHeight, startMachinePoint.X, startMachinePoint.Y, fromMachine: true);
        var end = Transform(aaBounds, request, pixelWidth, pixelHeight, endMachinePoint.X, endMachinePoint.Y, fromMachine: true);
        var safeStart = Transform(aaBounds, request, pixelWidth, pixelHeight, safeStartMachinePoint.X, safeStartMachinePoint.Y, fromMachine: true);
        var safeEnd = Transform(aaBounds, request, pixelWidth, pixelHeight, safeEndMachinePoint.X, safeEndMachinePoint.Y, fromMachine: true);
        return new CoordinatePlannerLine(
            Key: key,
            Label: label,
            Kind: kind,
            StartWorld: start.World,
            EndWorld: end.World,
            SafeStartWorld: safeStart.World,
            SafeEndWorld: safeEnd.World,
            StartPixelX: start.PixelX,
            StartPixelY: start.PixelY,
            EndPixelX: end.PixelX,
            EndPixelY: end.PixelY,
            StartMachineX: start.Machine.X,
            StartMachineY: start.Machine.Y,
            EndMachineX: end.Machine.X,
            EndMachineY: end.Machine.Y,
            SafeStartMachineX: safeStart.Machine.X,
            SafeStartMachineY: safeStart.Machine.Y,
            SafeEndMachineX: safeEnd.Machine.X,
            SafeEndMachineY: safeEnd.Machine.Y);
    }

    private static (Point2 World, double PixelX, double PixelY, MachinePoint Machine) Transform(
        Rect2 aaBounds,
        CoordinatePlannerRequest request,
        int pixelWidth,
        int pixelHeight,
        double x,
        double y,
        bool fromMachine)
    {
        var normX = x;
        var normY = y;
        var machine = new MachinePoint(x, y);
        if (fromMachine)
        {
            normX = request.MachineWidth <= 1e-9 ? 0d : (x - request.MachineOriginX) / request.MachineWidth;
            normY = request.MachineHeight <= 1e-9 ? 0d : (y - request.MachineOriginY) / request.MachineHeight;
        }
        else
        {
            machine = new MachinePoint(
                request.MachineOriginX + (request.MachineWidth * normX),
                request.MachineOriginY + (request.MachineHeight * normY));
        }

        // Machine inputs clamp only world coordinates. Normalized inputs retain their raw projection,
        // including on degenerate machine spans, rather than round-tripping through machine space.
        var worldNormX = fromMachine ? Math.Clamp(normX, 0d, 1d) : normX;
        var worldNormY = fromMachine ? Math.Clamp(normY, 0d, 1d) : normY;
        return (
            new Point2(aaBounds.MinX + (aaBounds.Width * worldNormX), aaBounds.MaxY - (aaBounds.Height * worldNormY)),
            pixelWidth * normX,
            pixelHeight * normY,
            machine);
    }

    private static Rect2 BuildWorldRect(Point2 topLeft, Point2 bottomRight)
    {
        return new Rect2(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(bottomRight.Y, topLeft.Y),
            Math.Max(topLeft.X, bottomRight.X),
            Math.Max(bottomRight.Y, topLeft.Y));
    }

    private static (double x, double y) ClampMachinePoint(double x, double y, CoordinatePlannerRequest request)
    {
        var radius = Math.Max(0d, request.CopperPillarDiameter * 0.5d);
        return (
            ClampAxis(x, request.MachineOriginX, request.MachineOriginX + request.MachineWidth, radius),
            ClampAxis(y, request.MachineOriginY, request.MachineOriginY + request.MachineHeight, radius));
    }

    private static MachinePoint ClampMachinePoint(MachinePoint point, CoordinatePlannerRequest request)
    {
        var (x, y) = ClampMachinePoint(point.X, point.Y, request);
        return new MachinePoint(x, y);
    }

    private static (double left, double top, double right, double bottom) ClampMachineRect(
        double left,
        double top,
        double right,
        double bottom,
        CoordinatePlannerRequest request)
    {
        var radius = Math.Max(0d, request.CopperPillarDiameter * 0.5d);
        var safeLeft = ClampAxis(left + radius, request.MachineOriginX, request.MachineOriginX + request.MachineWidth, radius);
        var safeRight = ClampAxis(right - radius, request.MachineOriginX, request.MachineOriginX + request.MachineWidth, radius);
        var safeTop = ClampAxis(top + radius, request.MachineOriginY, request.MachineOriginY + request.MachineHeight, radius);
        var safeBottom = ClampAxis(bottom - radius, request.MachineOriginY, request.MachineOriginY + request.MachineHeight, radius);
        if (safeLeft > safeRight)
        {
            var centerX = (safeLeft + safeRight) * 0.5d;
            safeLeft = centerX;
            safeRight = centerX;
        }

        if (safeTop > safeBottom)
        {
            var centerY = (safeTop + safeBottom) * 0.5d;
            safeTop = centerY;
            safeBottom = centerY;
        }

        return (safeLeft, safeTop, safeRight, safeBottom);
    }

    private static double ClampAxis(double value, double min, double max, double radius)
    {
        var safeMin = min + radius;
        var safeMax = max - radius;
        if (safeMin > safeMax)
        {
            return (min + max) * 0.5d;
        }

        return Math.Clamp(value, safeMin, safeMax);
    }

    private static double[] BuildGuideNormalizedPositions(
        CoordinateGuideGenerationMode mode,
        int count,
        double span,
        double pitch,
        double inset,
        IReadOnlyList<double>? explicitPositions)
    {
        var safeSpan = Math.Max(1e-9, span);
        var clampedInset = Math.Clamp(Math.Max(0d, inset), 0d, safeSpan * 0.5d);
        return mode switch
        {
            CoordinateGuideGenerationMode.Pitch => BuildPitchGuidePositions(safeSpan, Math.Max(0d, pitch), clampedInset),
            CoordinateGuideGenerationMode.ExplicitPositions => BuildExplicitGuidePositions(safeSpan, clampedInset, explicitPositions),
            _ => BuildCountGuidePositions(Math.Max(0, count), safeSpan, clampedInset),
        };
    }

    private static double[] BuildCountGuidePositions(int count, double span, double inset)
    {
        if (count == 0)
        {
            return Array.Empty<double>();
        }

        if (count == 1)
        {
            return [inset > 1e-9 ? 0.5d : 0d];
        }

        var start = inset / span;
        var end = (span - inset) / span;
        return Enumerable.Range(0, count)
            .Select(index => start + ((end - start) * GetGuideNormalizedPosition(index, count)))
            .ToArray();
    }

    private static double[] BuildPitchGuidePositions(double span, double pitch, double inset)
    {
        if (pitch <= 1e-9)
        {
            return Array.Empty<double>();
        }

        var end = span - inset;
        if (end < inset)
        {
            return Array.Empty<double>();
        }

        var positions = new List<double>();
        for (var offset = inset; offset <= end + 1e-9; offset += pitch)
        {
            positions.Add(Math.Clamp(offset / span, 0d, 1d));
        }

        return positions.ToArray();
    }

    private static double[] BuildExplicitGuidePositions(
        double span,
        double inset,
        IReadOnlyList<double>? explicitPositions)
    {
        if (explicitPositions is null || explicitPositions.Count == 0)
        {
            return Array.Empty<double>();
        }

        var min = inset;
        var max = span - inset;
        if (max < min)
        {
            return Array.Empty<double>();
        }

        return explicitPositions
            .Where(position => position >= min - 1e-9 && position <= max + 1e-9)
            .OrderBy(static position => position)
            .Select(position => Math.Clamp(position / span, 0d, 1d))
            .Distinct()
            .ToArray();
    }

    private static double GetGuideNormalizedPosition(int index, int count)
    {
        if (count <= 1)
        {
            return 0d;
        }

        return index / (count - 1d);
    }

    private static double GetArrayNormalizedPosition(int index, int count)
    {
        if (count <= 1)
        {
            return 0.5d;
        }

        return index / (count - 1d);
    }

    private static double GetArrayNormalizedPosition(int index, int count, double inset)
    {
        if (count <= 1)
        {
            return 0.5d;
        }

        var clampedInset = Math.Clamp(inset, 0d, 0.5d);
        return clampedInset + ((1d - (2d * clampedInset)) * index / (count - 1d));
    }

    private static double ComputeNormalizedInset(
        double pillarRadius,
        MachinePoint topLeft,
        MachinePoint topRight,
        MachinePoint bottomLeft,
        MachinePoint bottomRight,
        bool horizontal)
    {
        if (pillarRadius <= 1e-9)
        {
            return 0d;
        }

        var firstLength = horizontal
            ? Distance(topLeft, topRight)
            : Distance(topLeft, bottomLeft);
        var secondLength = horizontal
            ? Distance(bottomLeft, bottomRight)
            : Distance(topRight, bottomRight);
        var baseline = Math.Max(1e-9, Math.Min(firstLength, secondLength));
        return Math.Clamp(pillarRadius / baseline, 0d, 0.5d);
    }

    private static double Distance(MachinePoint a, MachinePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static MachinePoint Bilinear(
        MachinePoint topLeft,
        MachinePoint topRight,
        MachinePoint bottomRight,
        MachinePoint bottomLeft,
        double u,
        double v)
    {
        var top = Lerp(topLeft, topRight, u);
        var bottom = Lerp(bottomLeft, bottomRight, u);
        return Lerp(top, bottom, v);
    }

    private static MachinePoint Lerp(MachinePoint a, MachinePoint b, double t)
    {
        return new MachinePoint(
            a.X + ((b.X - a.X) * t),
            a.Y + ((b.Y - a.Y) * t));
    }
}
