using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Builds the legacy v2.2 9-column compatibility row.
/// This strategy handles X-way and Y-way freeform pads that have a matched CAD pad.
/// </summary>
internal static class V22LegacyRowStrategy
{
    internal static bool CanHandle(RegularPad reg, CadPad? cad)
        => cad is not null && (reg.Freeform == FreeformType.XWay || reg.Freeform == FreeformType.YWay);

    internal static NotchTableRow Build(
        RegularPad reg,
        CadPad? cad,
        RegularGrid grid,
        LegacyNotchGenerationRequest request)
    {
        var matchedCad = cad ?? throw new ArgumentNullException(nameof(cad));

        // EN_NHC_V3 layout (9 ints exported):
        // [0]=IDX, [1]=REGULAR, [2]=REGU_TO_FULL, [3]=PAD_IDX_LEFT, [4]=PAD_IDX_LEFT_LEN, [5]=DIFF_IDX_LEFT,
        // [6]=PAD_IDX_RIGHT, [7]=PAD_IDX_RIGHT_LEN, [8]=DIFF_IDX_RIGHT
        var v = new int[9];
        var nullVal = request.NullValue;
        var axis = NotchAlgorithmHelpers.ResolveAxisKind(reg.Freeform);
        var geometryContext = NotchAlgorithmHelpers.CreateAxisGeometryContext(grid, reg, axis);
        var neighborContext = CreateNeighborContext(grid, reg, axis, nullVal);

        v[0] = reg.DiffIndex;
        var regArea = Math.Max(reg.Area, 1e-12);
        v[1] = (int)Math.Round((matchedCad.Area * 100.0) / regArea);
        v[2] = (int)Math.Round((matchedCad.Bounds.Width * matchedCad.Bounds.Height * 100.0) / regArea);

        v[3] = neighborContext.LeftCadNeighborDiff;
        v[6] = neighborContext.RightCadNeighborDiff;

        var caseId = geometryContext.ResolveOverlapCase(matchedCad);
        var inputs = new SideInputs(geometryContext, neighborContext, matchedCad, caseId, reg.DiffIndex, nullVal, request.LenScale);
        var sides = geometryContext.IsX ? ResolveXWaySides(inputs) : ResolveYWaySides(inputs);

        if (sides.ClearCadNeighbors)
        {
            v[3] = nullVal;
            v[6] = nullVal;
        }

        v[4] = (int)Math.Round(sides.LeftLen);
        v[7] = (int)Math.Round(sides.RightLen);
        v[5] = sides.LeftDiff;
        v[8] = sides.RightDiff;

        var comment = $"{NotchAlgorithmHelpers.BuildComment(reg, matchedCad)} Case={caseId}";
        return new NotchTableRow(NotchAlgorithmVersion.V22, reg.IcIndex, reg.DiffIndex, reg.RegularPadId, matchedCad.Id, v, comment);
    }

    private static NotchAxisNeighborContext CreateNeighborContext(
        RegularGrid grid,
        RegularPad reg,
        NotchAxisKind axis,
        int nullVal)
    {
        return axis == NotchAxisKind.X
            ? NotchAlgorithmHelpers.CreateAxisNeighborContext(
                grid,
                reg,
                axis,
                nullVal,
                leftNeighbor: (reg.Row, reg.Col - 1),
                rightNeighbor: (reg.Row, reg.Col + 1))
            : NotchAlgorithmHelpers.CreateAxisNeighborContext(
                grid,
                reg,
                axis,
                nullVal,
                leftNeighbor: (reg.Row - 1, reg.Col),
                rightNeighbor: (reg.Row + 1, reg.Col));
    }

    private static SideValues ResolveXWaySides(SideInputs input)
    {
        // Case 0 = overlap on the 'near' side (right), case 1 = overlap on the 'far' side (left).
        // Contained or no overlap keeps both diffs on the regular pad and clears the CAD neighbors.
        if (input.CaseId is not (0 or 1))
        {
            return SideValues.Contained(input.RegDiff);
        }

        var geometry = input.Geometry;
        var neighbors = input.Neighbors;
        var regL = geometry.LeadingEdge;
        var regN = geometry.TrailingEdge;
        var cadL = input.Cad.Bounds.MinX;
        var cadN = input.Cad.Bounds.MaxX;
        var lenScale = input.LenScale;
        var regDiff = input.RegDiff;
        var isNear = input.CaseId == 0;

        if (geometry.IsTrailingBorder)
        {
            return isNear
                ? new SideValues(regDiff, input.NullVal, lenScale * (regN - cadL), lenScale * (cadN - regN), false)
                : new SideValues(neighbors.LeftNeighborDiff, regDiff, lenScale * (regL - cadL), lenScale * (cadN - regL), false);
        }

        if (geometry.IsLeadingBorder)
        {
            // Different calculation for the left border.
            return isNear
                ? new SideValues(regDiff, neighbors.RightNeighborDiff, lenScale * (cadL - regL), lenScale * (regN - cadL), false)
                : new SideValues(input.NullVal, regDiff, lenScale * (regL - cadL), lenScale * (cadN - regL), false);
        }

        return isNear
            ? new SideValues(regDiff, neighbors.RightNeighborDiff, lenScale * (regN - cadL), lenScale * (cadN - regN), false)
            : new SideValues(neighbors.LeftNeighborDiff, regDiff, lenScale * (regL - cadL), lenScale * (cadN - regL), false);
    }

    private static SideValues ResolveYWaySides(SideInputs input)
    {
        // Case 0 = overlap on the 'near' side (bottom), case 1 = overlap on the 'far' side (top).
        if (input.CaseId is not (0 or 1))
        {
            return SideValues.Contained(input.RegDiff);
        }

        var geometry = input.Geometry;
        var neighbors = input.Neighbors;
        var regL = geometry.LeadingEdge; // top (MaxY)
        var regN = geometry.TrailingEdge; // bottom (MinY)
        var cadL = input.Cad.Bounds.MaxY;
        var cadN = input.Cad.Bounds.MinY;
        var lenScale = input.LenScale;
        var regDiff = input.RegDiff;
        var isNear = input.CaseId == 0;
        var nearLeftLen = Math.Abs(lenScale * (regN - cadL));
        var nearRightLen = Math.Abs(lenScale * (cadN - regN));
        var farLeftLen = Math.Abs(lenScale * (regL - cadL));
        var farRightLen = Math.Abs(lenScale * (cadN - regL));

        if (geometry.IsLeadingBorder)
        {
            return isNear
                ? new SideValues(regDiff, neighbors.RightNeighborDiff, nearLeftLen, nearRightLen, false)
                : new SideValues(input.NullVal, regDiff, farLeftLen, farRightLen, false);
        }

        if (geometry.IsTrailingBorder)
        {
            return isNear
                ? new SideValues(regDiff, input.NullVal, nearLeftLen, nearRightLen, false)
                : new SideValues(neighbors.LeftNeighborDiff, regDiff, farLeftLen, farRightLen, false);
        }

        return isNear
            ? new SideValues(regDiff, neighbors.RightNeighborDiff, nearLeftLen, nearRightLen, false)
            : new SideValues(neighbors.LeftNeighborDiff, regDiff, farLeftLen, farRightLen, false);
    }

    private readonly record struct SideInputs(
        NotchAxisGeometryContext Geometry,
        NotchAxisNeighborContext Neighbors,
        CadPad Cad,
        int CaseId,
        int RegDiff,
        int NullVal,
        int LenScale);

    private readonly record struct SideValues(
        int LeftDiff,
        int RightDiff,
        double LeftLen,
        double RightLen,
        bool ClearCadNeighbors)
    {
        public static SideValues Contained(int regDiff) => new(regDiff, regDiff, 0.0, 0.0, true);
    }
}
