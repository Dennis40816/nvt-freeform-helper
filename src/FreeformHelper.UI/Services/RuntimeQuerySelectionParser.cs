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
        var values = new List<int>();
        error = null;

        var hasSingle = TryGetIntArg(args, "cad-id", 0, int.MaxValue, out var singleId, out var singleError);
        if (singleError is not null)
        {
            error = singleError;
            return values;
        }

        if (hasSingle)
        {
            values.Add(singleId);
        }

        if (TryGetIntListArg(args, "cad-ids", 0, int.MaxValue, out var list, out var listError))
        {
            values.AddRange(list);
        }
        else if (listError is not null)
        {
            error = listError;
            return values;
        }

        if (values.Count == 0)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Use --cad-id or --cad-ids for query select-cad.");
            return values;
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
        error = null;
        var indices = new List<int>();
        var missingIds = new HashSet<int>();
        var missingIdx = new HashSet<int>();

        var hasRegularId = TryGetIntArg(args, "regular-id", 0, int.MaxValue, out var regularId, out var regularIdError);
        if (regularIdError is not null)
        {
            error = regularIdError;
            missingRegularPadIds = Array.Empty<int>();
            missingRegularIndices = Array.Empty<int>();
            return indices;
        }

        if (hasRegularId)
        {
            if (regularIndexByPadId.TryGetValue(regularId, out var idx))
            {
                indices.Add(idx);
            }
            else
            {
                missingIds.Add(regularId);
            }
        }

        if (TryGetIntListArg(args, "regular-ids", 0, int.MaxValue, out var regularIdList, out var regularIdsError))
        {
            foreach (var id in regularIdList)
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
        }
        else if (regularIdsError is not null)
        {
            error = regularIdsError;
            missingRegularPadIds = Array.Empty<int>();
            missingRegularIndices = Array.Empty<int>();
            return indices;
        }

        var visibleIndexSet = regularIndexByPadId.Values.ToHashSet();
        var hasRegularIndex = TryGetIntArg(args, "regular-index", 0, int.MaxValue, out var regularIndex, out var regularIndexError);
        if (regularIndexError is not null)
        {
            error = regularIndexError;
            missingRegularPadIds = Array.Empty<int>();
            missingRegularIndices = Array.Empty<int>();
            return indices;
        }

        if (hasRegularIndex)
        {
            if (visibleIndexSet.Contains(regularIndex))
            {
                indices.Add(regularIndex);
            }
            else
            {
                missingIdx.Add(regularIndex);
            }
        }

        if (TryGetIntListArg(args, "regular-indices", 0, int.MaxValue, out var regularIndicesList, out var regularIndicesError))
        {
            foreach (var idx in regularIndicesList)
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
        }
        else if (regularIndicesError is not null)
        {
            error = regularIndicesError;
            missingRegularPadIds = Array.Empty<int>();
            missingRegularIndices = Array.Empty<int>();
            return indices;
        }

        if (indices.Count == 0 && missingIds.Count == 0 && missingIdx.Count == 0)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Use regular-id/regular-ids or regular-index/regular-indices for query select-regular.");
            missingRegularPadIds = Array.Empty<int>();
            missingRegularIndices = Array.Empty<int>();
            return indices;
        }

        missingRegularPadIds = missingIds.OrderBy(id => id).ToList();
        missingRegularIndices = missingIdx.OrderBy(id => id).ToList();
        return indices.Distinct().OrderBy(idx => idx).ToList();
    }

}
