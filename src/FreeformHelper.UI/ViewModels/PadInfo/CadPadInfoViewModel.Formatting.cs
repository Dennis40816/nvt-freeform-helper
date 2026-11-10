using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CadPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    private static string BuildCadMatchText(
        List<int> regularIds,
        IReadOnlyList<PadMatchLink> links,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        if (regularIds.Count == 0)
        {
            return PadMatchTextFormatter.CadMatchText(regularIds, Array.Empty<int>(), getRegularPadIcDiff);
        }

        return PadMatchTextFormatter.CadMatchText(
            regularIds, links.Select(link => link.RegularPadId), getRegularPadIcDiff);
    }

    private static string BuildCadMatchDetailsText(
        List<int> regularIds,
        List<PadMatchLink> links,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        if (regularIds.Count == 0)
        {
            return "Unmatched";
        }

        if (links.Count == 0)
        {
            var texts = regularIds
                .Select(regularId => BuildRegularMatchDetail(regularId, getRegularPadIcDiff))
                .ToList();
            return $"Regular: {string.Join(", ", texts)}";
        }

        var lines = links
            .Select(link => $"{BuildRegularMatchDetail(link.RegularPadId, getRegularPadIcDiff)}: {(link.CadCoverage * 100):0.#}%")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRegularMatchDetail(
        int regularPadId,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        var match = getRegularPadIcDiff?.Invoke(regularPadId);
        return match.HasValue
            ? $"IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex} (regId {regularPadId})"
            : $"regId {regularPadId}";
    }

    /// <summary>
    /// Helper method to format a <see cref="Point2" /> into a human-readable string.
    /// </summary>
    private static string FormatPoint(Point2 p)
    {
        return $"{p.X:0.####}, {p.Y:0.####}";
    }
}
