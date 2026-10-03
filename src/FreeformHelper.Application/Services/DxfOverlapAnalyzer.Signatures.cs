using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

public sealed partial class DxfOverlapAnalyzer
{
    /// <summary>
    /// Builds a canonical string signature for a polygon. The signature is invariant to starting vertex and direction.
    /// </summary>
    private static string BuildSignature(Polygon2 poly)
    {
        return CadPadGeometrySignature.Build(poly, SignatureDecimals, Epsilon);
    }
}
