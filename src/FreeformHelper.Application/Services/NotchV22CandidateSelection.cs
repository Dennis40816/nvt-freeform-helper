// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>The canonical candidate order and the first key that separates two candidates.</summary>
public static class NotchV22CandidateSelection
{
    private static readonly Comparer<Rank> _order = Comparer<Rank>.Create(static (left, right) => Compare(left, right).Order);

    internal static T Select<T>(IEnumerable<T> candidates, Func<T, Rank> describe) =>
        candidates.OrderBy(describe, _order).First();

    public static string DecidingKey(IReadOnlyList<NotchV22CandidateExplanation> candidates)
    {
        var ordered = candidates.Select(static candidate => new Rank(candidate.HasToFullInfluence, candidate.HasLegs,
            candidate.IsAnchorMatchedCad, candidate.SourceAreaScore, candidate.CombinePercent, candidate.CadPadId))
            .OrderBy(static rank => rank, _order).ToArray();
        return ordered.Length < 2 ? string.Empty : Compare(ordered[0], ordered[1]).Key;
    }

    private static (int Order, string Key) Compare(Rank left, Rank right)
    {
        if (left.HasToFullInfluence != right.HasToFullInfluence)
        {
            return (right.HasToFullInfluence.CompareTo(left.HasToFullInfluence), "To Full influence");
        }
        if (left.HasLegs != right.HasLegs)
        {
            return (right.HasLegs.CompareTo(left.HasLegs), "has targets");
        }
        if (left.IsAnchorMatchedCad != right.IsAnchorMatchedCad)
        {
            return (right.IsAnchorMatchedCad.CompareTo(left.IsAnchorMatchedCad), "anchor matched CAD");
        }
        var areaOrder = right.SourceAreaScore.CompareTo(left.SourceAreaScore);
        if (areaOrder != 0)
        {
            return (areaOrder, "larger source area");
        }
        if (left.CombinePercent != right.CombinePercent)
        {
            return (right.CombinePercent.CompareTo(left.CombinePercent), "higher combine");
        }
        return left.CadPadId != right.CadPadId
            ? (left.CadPadId.CompareTo(right.CadPadId), "lower CAD id")
            : (0, "input order (all keys tied)");
    }

    internal readonly record struct Rank(bool HasToFullInfluence, bool HasLegs, bool IsAnchorMatchedCad,
        double SourceAreaScore, int CombinePercent, int CadPadId);
}
