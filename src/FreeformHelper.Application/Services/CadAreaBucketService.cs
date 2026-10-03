using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Groups CAD pads into area buckets using a relative area tolerance.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance API is intentionally preserved for service call-site stability.")]
public sealed class CadAreaBucketService
{
    private const double MinRelativeBaseArea = 1e-12;

    /// <summary>
    /// Builds ordered area buckets from the provided CAD pads.
    /// </summary>
    /// <param name="pads">Visible CAD pads to bucket.</param>
    /// <param name="relativeTolerance">Relative tolerance in [0, 0.5].</param>
    /// <returns>Ordered buckets (small area to large area).</returns>
    public static IReadOnlyList<IReadOnlyList<int>> BuildBuckets(IReadOnlyList<CadPad> pads, double relativeTolerance)
    {
        if (pads.Count == 0)
        {
            return Array.Empty<IReadOnlyList<int>>();
        }

        var tolerance = Math.Clamp(relativeTolerance, 0.0, 0.5);
        var sorted = pads
            .Select(p => (p.Id, Area: p.Area))
            .OrderBy(x => x.Area)
            .ThenBy(x => x.Id)
            .ToList();

        var buckets = new List<IReadOnlyList<int>>();
        var currentBucket = new List<int>();
        var currentRefArea = 0.0;

        foreach (var (id, area) in sorted)
        {
            if (currentBucket.Count == 0)
            {
                currentBucket.Add(id);
                currentRefArea = area;
                continue;
            }

            var relativeDelta = Math.Abs(area - currentRefArea) / Math.Max(currentRefArea, MinRelativeBaseArea);
            if (relativeDelta <= tolerance)
            {
                currentBucket.Add(id);
                continue;
            }

            buckets.Add(currentBucket);
            currentBucket = new List<int> { id };
            currentRefArea = area;
        }

        if (currentBucket.Count > 0)
        {
            buckets.Add(currentBucket);
        }

        return buckets;
    }

    /// <summary>
    /// Resolves the bucket members for a specific CAD pad ID.
    /// </summary>
    /// <param name="pads">Visible CAD pads to bucket.</param>
    /// <param name="relativeTolerance">Relative tolerance in [0, 0.5].</param>
    /// <param name="anchorPadId">Anchor CAD pad ID.</param>
    /// <returns>CAD pad IDs in the same bucket as the anchor; empty when not found.</returns>
    public IReadOnlyList<int> ResolveBucketMembers(IReadOnlyList<CadPad> pads, double relativeTolerance, int anchorPadId)
    {
        var buckets = BuildBuckets(pads, relativeTolerance);
        foreach (var bucket in buckets)
        {
            if (bucket.Contains(anchorPadId))
            {
                return bucket.ToList();
            }
        }

        return Array.Empty<int>();
    }
}
