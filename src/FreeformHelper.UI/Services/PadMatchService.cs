using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Provides a service layer for matching regular pads to CAD pads and detecting freeform pads.
/// This service orchestrates the use of <see cref="PadMatcher"/> and <see cref="FreeformDetector"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance service API is retained for workflow composition stability.")]
public sealed class PadMatchService
{
    /// <summary>
    /// Builds the current CAD-to-regular overlap evidence.
    /// </summary>
    /// <param name="cad">The set of CAD pads.</param>
    /// <param name="grid">The regular grid whose pads will be matched.</param>
    /// <param name="settings">
    /// Compatibility parameter retained by the Application matcher API; currently ignored.
    /// </param>
    public PadMatchResult Match(CadPadSet cad, RegularGrid grid, MatchingSettings settings, Action<double>? reportProgress = null)
    {
        return PadMatcher.Match(cad, grid, settings, reportProgress);
    }

    /// <summary>
    /// Automatically detects and tags freeform pads within the regular grid after matching has occurred.
    /// </summary>
    /// <param name="cad">The set of CAD pads (needed for freeform detection logic).</param>
    /// <param name="grid">The regular grid containing pads with match information.</param>
    /// <param name="settings">The matching settings (potentially used by freeform detector).</param>
    public void AutoDetectFreeforms(CadPadSet cad, RegularGrid grid, MatchingSettings settings, PadMatchResult? matchResult = null)
    {
        FreeformDetector.AutoTagFreeforms(cad, grid, settings, matchResult);
    }
}
