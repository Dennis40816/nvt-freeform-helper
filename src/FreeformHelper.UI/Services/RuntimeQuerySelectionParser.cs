// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Nvt.Core.RuntimeQuery;
using static Nvt.Core.RuntimeQuery.RuntimeQueryArgumentParser;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQuerySelectionParser
{
    public static List<int> ParseCadSelectionArgs(
        IReadOnlyDictionary<string, string>? args,
        out RuntimeQueryResponseEnvelope? error)
    {
        if (!TryReadRequestedValues(args, "cad-id", "cad-ids", out var values, out error))
        {
            return values;
        }

        if (values.Count == 0)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Use --cad-id or --cad-ids for query select-cad.");
        }

        return values;
    }

    public static List<int> ParseRegularSelectionArgs(
        IReadOnlyDictionary<string, string>? args,
        IReadOnlyDictionary<int, int> regularIndexByPadId,
        out RuntimeQueryResponseEnvelope? error,
        out IReadOnlyList<int> missingRegularPadIds,
        out IReadOnlyList<int> missingRegularIndices)
    {
        missingRegularPadIds = Array.Empty<int>();
        missingRegularIndices = Array.Empty<int>();

        // The pad id arguments are read before the index arguments, so the first bad argument is the one reported.
        if (!TryReadRequestedValues(args, "regular-id", "regular-ids", out var requestedIds, out error) ||
            !TryReadRequestedValues(args, "regular-index", "regular-indices", out var requestedIndices, out error))
        {
            return [];
        }

        var indices = new List<int>();
        var missingIds = new HashSet<int>();
        foreach (var id in requestedIds)
        {
            if (regularIndexByPadId.TryGetValue(id, out var idx))
            {
                indices.Add(idx);
            }
            else
            {
                missingIds.Add(id);
            }
        }

        var visibleIndexSet = regularIndexByPadId.Values.ToHashSet();
        var missingIdx = new HashSet<int>();
        foreach (var idx in requestedIndices)
        {
            if (visibleIndexSet.Contains(idx))
            {
                indices.Add(idx);
            }
            else
            {
                missingIdx.Add(idx);
            }
        }

        if (indices.Count == 0 && missingIds.Count == 0 && missingIdx.Count == 0)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Use regular-id/regular-ids or regular-index/regular-indices for query select-regular.");
            return indices;
        }

        missingRegularPadIds = missingIds.OrderBy(id => id).ToList();
        missingRegularIndices = missingIdx.OrderBy(id => id).ToList();
        return indices.Distinct().OrderBy(idx => idx).ToList();
    }

    // Reads the single and the list form of one argument. The single value comes first in the result.
    private static bool TryReadRequestedValues(
        IReadOnlyDictionary<string, string>? args,
        string singleKey,
        string listKey,
        out List<int> values,
        out RuntimeQueryResponseEnvelope? error)
    {
        values = [];
        error = null;

        var hasSingle = TryGetIntArg(args, singleKey, 0, int.MaxValue, out var single, out var singleError);
        if (singleError is not null)
        {
            error = singleError;
            return false;
        }

        if (hasSingle)
        {
            values.Add(single);
        }

        if (TryGetIntListArg(args, listKey, 0, int.MaxValue, out var list, out var listError))
        {
            values.AddRange(list);
        }
        else if (listError is not null)
        {
            error = listError;
            return false;
        }

        return true;
    }
}
