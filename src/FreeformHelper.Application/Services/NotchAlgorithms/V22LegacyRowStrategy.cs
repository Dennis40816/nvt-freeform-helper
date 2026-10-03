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
        var neighborContext = axis == NotchAxisKind.X
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

        v[0] = reg.DiffIndex;
        var regArea = Math.Max(reg.Area, 1e-12);
        v[1] = (int)Math.Round((matchedCad.Area * 100.0) / regArea);
        v[2] = (int)Math.Round((matchedCad.Bounds.Width * matchedCad.Bounds.Height * 100.0) / regArea);

        var lenScale = request.LenScale;
        var regDiff = reg.DiffIndex;

        v[3] = neighborContext.LeftCadNeighborDiff;
        v[6] = neighborContext.RightCadNeighborDiff;

        int leftDiff;
        int rightDiff;
        var leftLen = 0.0;
        var rightLen = 0.0;
        var caseId = geometryContext.ResolveOverlapCase(matchedCad);

        if (geometryContext.IsX)
        {
            var regL = geometryContext.LeadingEdge;
            var regN = geometryContext.TrailingEdge;
            var cadL = matchedCad.Bounds.MinX;
            var cadN = matchedCad.Bounds.MaxX;

            if (geometryContext.IsTrailingBorder)
            {
                if (caseId == 0) // Overlap on the 'near' side (right)
                {
                    leftDiff = regDiff;
                    rightDiff = nullVal; // No right neighbor
                    leftLen = lenScale * (regN - cadL);
                    rightLen = lenScale * (cadN - regN);
                }
                else if (caseId == 1) // Overlap on the 'far' side (left)
                {
                    leftDiff = neighborContext.LeftNeighborDiff;
                    rightDiff = regDiff;
                    leftLen = lenScale * (regL - cadL);
                    rightLen = lenScale * (cadN - regL);
                }
                else // Contained or no overlap
                {
                    leftDiff = regDiff;
                    rightDiff = regDiff;
                    v[3] = nullVal;
                    v[6] = nullVal;
                }
            }
            else if (geometryContext.IsLeadingBorder)
            {
                if (caseId == 0)
                {
                    leftDiff = regDiff;
                    rightDiff = neighborContext.RightNeighborDiff;
                    leftLen = lenScale * (cadL - regL); // Different calculation for left border
                    rightLen = lenScale * (regN - cadL);
                }
                else if (caseId == 1)
                {
                    leftDiff = nullVal; // No left neighbor
                    rightDiff = regDiff;
                    leftLen = lenScale * (regL - cadL);
                    rightLen = lenScale * (cadN - regL);
                }
                else
                {
                    leftDiff = regDiff;
                    rightDiff = regDiff;
                    v[3] = nullVal;
                    v[6] = nullVal;
                }
            }
            else // Not a border pad
            {
                if (caseId == 0)
                {
                    leftDiff = regDiff;
                    rightDiff = neighborContext.RightNeighborDiff;
                    leftLen = lenScale * (regN - cadL);
                    rightLen = lenScale * (cadN - regN);
                }
                else if (caseId == 1)
                {
                    leftDiff = neighborContext.LeftNeighborDiff;
                    rightDiff = regDiff;
                    leftLen = lenScale * (regL - cadL);
                    rightLen = lenScale * (cadN - regL);
                }
                else
                {
                    leftDiff = regDiff;
                    rightDiff = regDiff;
                    v[3] = nullVal;
                    v[6] = nullVal;
                }
            }
        }
        else // Y-Way
        {
            var regL = geometryContext.LeadingEdge; // top (MaxY)
            var regN = geometryContext.TrailingEdge; // bottom (MinY)
            var cadL = matchedCad.Bounds.MaxY;
            var cadN = matchedCad.Bounds.MinY;

            if (geometryContext.IsLeadingBorder)
            {
                if (caseId == 0) // Overlap on 'near' side (bottom)
                {
                    leftDiff = regDiff;
                    rightDiff = neighborContext.RightNeighborDiff;
                    leftLen = Math.Abs(lenScale * (regN - cadL));
                    rightLen = Math.Abs(lenScale * (cadN - regN));
                }
                else if (caseId == 1) // Overlap on 'far' side (top)
                {
                    leftDiff = nullVal; // No top neighbor
                    rightDiff = regDiff;
                    leftLen = Math.Abs(lenScale * (regL - cadL));
                    rightLen = Math.Abs(lenScale * (cadN - regL));
                }
                else
                {
                    leftDiff = regDiff;
                    rightDiff = regDiff;
                    v[3] = nullVal;
                    v[6] = nullVal;
                }
            }
            else if (geometryContext.IsTrailingBorder)
            {
                if (caseId == 0)
                {
                    leftDiff = regDiff;
                    rightDiff = nullVal; // No bottom neighbor
                    leftLen = Math.Abs(lenScale * (regN - cadL));
                    rightLen = Math.Abs(lenScale * (cadN - regN));
                }
                else if (caseId == 1)
                {
                    leftDiff = neighborContext.LeftNeighborDiff;
                    rightDiff = regDiff;
                    leftLen = Math.Abs(lenScale * (regL - cadL));
                    rightLen = Math.Abs(lenScale * (cadN - regL));
                }
                else
                {
                    leftDiff = regDiff;
                    rightDiff = regDiff;
                    v[3] = nullVal;
                    v[6] = nullVal;
                }
            }
            else // Not a border pad
            {
                if (caseId == 0)
                {
                    leftDiff = regDiff;
                    rightDiff = neighborContext.RightNeighborDiff;
                    leftLen = Math.Abs(lenScale * (regN - cadL));
                    rightLen = Math.Abs(lenScale * (cadN - regN));
                }
                else if (caseId == 1)
                {
                    leftDiff = neighborContext.LeftNeighborDiff;
                    rightDiff = regDiff;
                    leftLen = Math.Abs(lenScale * (regL - cadL));
                    rightLen = Math.Abs(lenScale * (cadN - regL));
                }
                else
                {
                    leftDiff = regDiff;
                    rightDiff = regDiff;
                    v[3] = nullVal;
                    v[6] = nullVal;
                }
            }
        }

        v[4] = (int)Math.Round(leftLen);
        v[7] = (int)Math.Round(rightLen);
        v[5] = leftDiff;
        v[8] = rightDiff;

        var comment = $"{NotchAlgorithmHelpers.BuildComment(reg, matchedCad)} Case={caseId}";
        return new NotchTableRow(NotchAlgorithmVersion.V22, reg.IcIndex, reg.DiffIndex, reg.RegularPadId, matchedCad.Id, v, comment);
    }
}
