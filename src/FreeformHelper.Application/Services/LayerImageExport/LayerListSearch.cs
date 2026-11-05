// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Provides the shared token matching and highlight rules for layer names.</summary>
public static class LayerListSearch
{
    /// <summary>Splits on all whitespace, dropping empty terms; null is an empty query.</summary>
    public static IReadOnlyList<string> ParseTerms(string? query) =>
        Array.AsReadOnly((query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Tests whether every term occurs using ordinal case-insensitive comparison.</summary>
    public static bool Matches(string name, IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(terms);
        return terms.All(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Finds every occurrence, including overlapping occurrences, and merges overlapping or adjacent ranges.</summary>
    public static IReadOnlyList<LayerTextRange> FindRanges(string name, IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(terms);
        var ranges = new List<LayerTextRange>();
        foreach (string term in terms)
        {
            if (string.IsNullOrEmpty(term))
            {
                continue;
            }

            int start = name.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (start >= 0)
            {
                ranges.Add(new LayerTextRange(start, term.Length));
                start = name.IndexOf(term, start + 1, StringComparison.OrdinalIgnoreCase);
            }
        }

        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        var merged = new List<LayerTextRange>();
        foreach (var range in ranges)
        {
            if (merged.Count == 0 || range.Start > merged[^1].Start + merged[^1].Length)
            {
                merged.Add(range);
                continue;
            }

            var previous = merged[^1];
            int end = Math.Max(previous.Start + previous.Length, range.Start + range.Length);
            merged[^1] = new LayerTextRange(previous.Start, end - previous.Start);
        }

        return merged.AsReadOnly();
    }
}
