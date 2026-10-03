using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Implements the notch algorithm version 2.1.
/// This algorithm is used for pads that are not of type <see cref="FreeformType.None"/>.
/// </summary>
internal static class V21NotchAlgorithm
{
    internal static bool CanHandle(RegularPad reg) => reg.Freeform != FreeformType.None;

    internal static NotchTableRow Build(
        RegularPad reg,
        CadPad? cad,
        RegularGrid grid,
        LegacyNotchGenerationRequest request)
    {
        // EN_NHC_V2 layout:
        // [0]=IDX, [1]=REGULAR%, [2]=REGU_TO_FULL%, [3]=1ST_IDX, [4]=1ST_TYPE, [5]=1ST_RATIO_Q7, [6]=2ND_IDX, [7]=2ND_TYPE, [8]=2ND_RATIO_Q7
        var v = new int[9];
        var nullVal = request.NullValue;
        var axis = NotchAlgorithmHelpers.ResolveAxisKind(reg.Freeform);
        var geometryContext = NotchAlgorithmHelpers.CreateAxisGeometryContext(grid, reg, axis);

        v[0] = reg.DiffIndex;
        v[1] = cad is null ? 0 : (int)Math.Round((cad.Area * 100.0) / Math.Max(reg.Area, 1e-12));
        v[2] = cad is null ? 0 : (int)Math.Round((cad.Bounds.Width * cad.Bounds.Height * 100.0) / Math.Max(reg.Area, 1e-12));

        var comment = NotchAlgorithmHelpers.BuildComment(reg, cad);

        if (axis == NotchAxisKind.X)
        {
            var neighborContext = NotchAlgorithmHelpers.CreateAxisNeighborContext(
                grid,
                reg,
                axis,
                nullVal,
                leftNeighbor: (reg.Row, reg.Col - 1),
                rightNeighbor: (reg.Row, reg.Col + 1));
            FillAxis(
                geometryContext: geometryContext,
                cad: cad,
                nullValue: request.NullValue,
                neighborContext: neighborContext,
                regDiff: reg.DiffIndex,
                v: v);
        }
        else if (axis == NotchAxisKind.Y)
        {
            var neighborContext = NotchAlgorithmHelpers.CreateAxisNeighborContext(
                grid,
                reg,
                axis,
                nullVal,
                leftNeighbor: (reg.Row + 1, reg.Col),
                rightNeighbor: (reg.Row - 1, reg.Col));
            FillAxis(
                geometryContext: geometryContext,
                cad: cad,
                nullValue: request.NullValue,
                neighborContext: neighborContext,
                regDiff: reg.DiffIndex,
                v: v);
        }

        return new NotchTableRow(NotchAlgorithmVersion.V21, reg.IcIndex, reg.DiffIndex, reg.RegularPadId, cad?.Id ?? reg.MatchedCadPadId, v, comment);
    }

    /// <summary>
    /// Fills the notch data array for a specific axis (X or Y).
    /// </summary>
    /// <param name="geometryContext">Shared axis geometry context.</param>
    /// <param name="cad">The CAD pad.</param>
    /// <param name="nullValue">The frozen null sentinel.</param>
    /// <param name="neighborContext">Shared neighbor lookup context.</param>
    /// <param name="regDiff">The current regular diff index.</param>
    /// <param name="v">The integer array to fill with notch data.</param>
    private static void FillAxis(
        NotchAxisGeometryContext geometryContext,
        CadPad? cad,
        int nullValue,
        NotchAxisNeighborContext neighborContext,
        int regDiff,
        int[] v)
    {
        if (cad is null)
        {
            v[3] = NotchV21Q7Codec.TypeNone;
            v[4] = NotchV21Q7Codec.TypeNone;
            v[5] = NotchV21Q7Codec.TypeNone;
            v[6] = NotchV21Q7Codec.TypeNone;
            v[7] = NotchV21Q7Codec.TypeNone;
            v[8] = NotchV21Q7Codec.TypeNone;
            return;
        }

        if (geometryContext.IsX)
        {
            var deltaLeft = geometryContext.GetLeadingDelta(cad);
            var deltaRight = geometryContext.GetTrailingDelta(cad);

            var addLeft = geometryContext.ExpandsLeading(cad);
            var addRight = geometryContext.ExpandsTrailing(cad);

            if (neighborContext.LeftNeighborDiff == nullValue)
            {
                v[3] = NotchV21Q7Codec.TypeNone;
                v[4] = NotchV21Q7Codec.TypeNone;
                v[5] = NotchV21Q7Codec.TypeNone;
            }
            else
            {
                v[3] = addLeft ? neighborContext.LeftNeighborDiff : regDiff;
                v[4] = addLeft ? NotchV21Q7Codec.TypeAdd : NotchV21Q7Codec.TypeSub;
                v[5] = NotchV21Q7Codec.EncodeLegacyRowFractionRaw(
                    deltaLeft / Math.Max(addLeft ? neighborContext.LeftNeighborLength : geometryContext.CurrentCellLength, 1e-12));
            }

            if (neighborContext.RightNeighborDiff == nullValue)
            {
                v[6] = NotchV21Q7Codec.TypeNone;
                v[7] = NotchV21Q7Codec.TypeNone;
                v[8] = NotchV21Q7Codec.TypeNone;
            }
            else
            {
                v[6] = addRight ? neighborContext.RightNeighborDiff : regDiff;
                v[7] = addRight ? NotchV21Q7Codec.TypeAdd : NotchV21Q7Codec.TypeSub;
                v[8] = NotchV21Q7Codec.EncodeLegacyRowFractionRaw(
                    deltaRight / Math.Max(addRight ? neighborContext.RightNeighborLength : geometryContext.CurrentCellLength, 1e-12));
            }
        }
        else // Y-axis
        {
            var deltaUp = geometryContext.GetLeadingDelta(cad);
            var deltaDown = geometryContext.GetTrailingDelta(cad);

            var addUp = geometryContext.ExpandsLeading(cad);
            var addDown = geometryContext.ExpandsTrailing(cad);

            // Note: In Y-axis, leftNeighbor corresponds to the pad "above" (higher row index),
            // and rightNeighbor corresponds to the pad "below" (lower row index).
            if (neighborContext.LeftNeighborDiff == nullValue)
            {
                v[3] = NotchV21Q7Codec.TypeNone;
                v[4] = NotchV21Q7Codec.TypeNone;
                v[5] = NotchV21Q7Codec.TypeNone;
            }
            else
            {
                v[3] = addUp ? neighborContext.LeftNeighborDiff : regDiff;
                v[4] = addUp ? NotchV21Q7Codec.TypeAdd : NotchV21Q7Codec.TypeSub;
                v[5] = NotchV21Q7Codec.EncodeLegacyRowFractionRaw(
                    deltaUp / Math.Max(addUp ? neighborContext.LeftNeighborLength : geometryContext.CurrentCellLength, 1e-12));
            }

            if (neighborContext.RightNeighborDiff == nullValue)
            {
                v[6] = NotchV21Q7Codec.TypeNone;
                v[7] = NotchV21Q7Codec.TypeNone;
                v[8] = NotchV21Q7Codec.TypeNone;
            }
            else
            {
                v[6] = addDown ? neighborContext.RightNeighborDiff : regDiff;
                v[7] = addDown ? NotchV21Q7Codec.TypeAdd : NotchV21Q7Codec.TypeSub;
                v[8] = NotchV21Q7Codec.EncodeLegacyRowFractionRaw(
                    deltaDown / Math.Max(addDown ? neighborContext.RightNeighborLength : geometryContext.CurrentCellLength, 1e-12));
            }
        }
    }
}
