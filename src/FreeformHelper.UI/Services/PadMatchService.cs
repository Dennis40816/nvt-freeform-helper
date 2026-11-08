using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Provides a service layer for matching regular pads to CAD pads.
/// This service wraps <see cref="PadMatcher"/>.
/// </summary>
public sealed class PadMatchService
{
    /// <summary>
    /// Builds many-to-many CAD-to-regular overlap evidence and updates each regular pad's best match.
    /// </summary>
    /// <param name="cad">The set of CAD pads.</param>
    /// <param name="grid">The regular grid whose pads will be matched.</param>
    /// <param name="reportProgress">Optional callback that receives the matching progress.</param>
    public static PadMatchResult Match(CadPadSet cad, RegularGrid grid, Action<double>? reportProgress = null)
    {
        return PadMatcher.Match(cad, grid, reportProgress);
    }
}
