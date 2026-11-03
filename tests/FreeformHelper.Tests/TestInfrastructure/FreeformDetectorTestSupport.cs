using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class FreeformDetectorTestSupport
{
    /// <summary>
    /// Detects the freeform assignments and applies them to the grid, the way production code does in two steps.
    /// </summary>
    public static void AutoTag(CadPadSet cad, RegularGrid grid, MatchingSettings matchSettings, PadMatchResult? matchResult = null)
    {
        FreeformDetector.ApplyAssignments(grid, FreeformDetector.DetectAssignments(cad, grid, matchSettings, matchResult));
    }
}
