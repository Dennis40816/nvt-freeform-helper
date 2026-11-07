using FreeformHelper.Domain.Geometry;

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
