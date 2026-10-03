using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

internal enum NotchAxisKind
{
    X,
    Y,
}

internal readonly record struct NotchAxisGeometryContext(
    NotchAxisKind Axis,
    Rect2 CellRect,
    bool IsLeadingBorder,
    bool IsTrailingBorder)
{
    public bool IsX => Axis == NotchAxisKind.X;

    public double LeadingEdge => IsX ? CellRect.MinX : CellRect.MaxY;

    public double TrailingEdge => IsX ? CellRect.MaxX : CellRect.MinY;

    public int ResolveOverlapCase(CadPad cad)
    {
        ArgumentNullException.ThrowIfNull(cad);

        if (IsX)
        {
            return NotchAlgorithmHelpers.RegPadOverlapCadPadCase(
                LeadingEdge,
                TrailingEdge,
                cad.Bounds.MinX,
                cad.Bounds.MaxX);
        }

        return NotchAlgorithmHelpers.RegPadOverlapCadPadCase(
            -LeadingEdge,
            -TrailingEdge,
            -cad.Bounds.MaxY,
            -cad.Bounds.MinY);
    }

    public double GetLeadingDelta(CadPad cad)
    {
        ArgumentNullException.ThrowIfNull(cad);
        return IsX
            ? Math.Abs(cad.Bounds.MinX - LeadingEdge)
            : Math.Abs(cad.Bounds.MaxY - LeadingEdge);
    }

    public double GetTrailingDelta(CadPad cad)
    {
        ArgumentNullException.ThrowIfNull(cad);
        return IsX
            ? Math.Abs(cad.Bounds.MaxX - TrailingEdge)
            : Math.Abs(cad.Bounds.MinY - TrailingEdge);
    }

    public bool ExpandsLeading(CadPad cad)
    {
        ArgumentNullException.ThrowIfNull(cad);
        return IsX
            ? cad.Bounds.MinX < LeadingEdge
            : cad.Bounds.MaxY > LeadingEdge;
    }

    public bool ExpandsTrailing(CadPad cad)
    {
        ArgumentNullException.ThrowIfNull(cad);
        return IsX
            ? cad.Bounds.MaxX > TrailingEdge
            : cad.Bounds.MinY < TrailingEdge;
    }

    public double CurrentCellLength => IsX ? CellRect.Width : CellRect.Height;
}

internal readonly record struct NotchAxisNeighborContext(
    int LeftNeighborDiff,
    int RightNeighborDiff,
    int LeftCadNeighborDiff,
    int RightCadNeighborDiff,
    double LeftNeighborLength,
    double RightNeighborLength);
