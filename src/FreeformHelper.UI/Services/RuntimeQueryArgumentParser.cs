using System.Globalization;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryArgumentParser
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

    public static bool TryGetIntArg(
        IReadOnlyDictionary<string, string>? args,
        string key,
        int min,
        int max,
        out int value,
        out RuntimeQueryResponseEnvelope? error)
    {
        value = 0;
        error = null;

        if (args is null || !args.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Argument '--{key}' must be an integer.");
            return false;
        }

        if (parsed < min || parsed > max)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Argument '--{key}' must be in [{min}, {max}].");
            return false;
        }

        value = parsed;
        return true;
    }

    public static bool TryGetIntListArg(
        IReadOnlyDictionary<string, string>? args,
        string key,
        int min,
        int max,
        out List<int> values,
        out RuntimeQueryResponseEnvelope? error)
    {
        values = new List<int>();
        error = null;

        if (args is null || !args.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 0)
            .ToList();
        if (tokens.Count == 0)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Argument '--{key}' cannot be empty.");
            return false;
        }

        foreach (var token in tokens)
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                error = RuntimeQueryResponseEnvelope.Failure(
                    code: "INVALID_ARGUMENTS",
                    message: $"Argument '--{key}' must be a comma-separated integer list.");
                values.Clear();
                return false;
            }

            if (parsed < min || parsed > max)
            {
                error = RuntimeQueryResponseEnvelope.Failure(
                    code: "INVALID_ARGUMENTS",
                    message: $"Argument '--{key}' values must be in [{min}, {max}].");
                values.Clear();
                return false;
            }

            values.Add(parsed);
        }

        return true;
    }

    public static bool TryGetDoubleArg(
        IReadOnlyDictionary<string, string>? args,
        string key,
        double min,
        double max,
        out double value,
        out RuntimeQueryResponseEnvelope? error)
    {
        value = 0.0;
        error = null;

        if (args is null || !args.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Argument '--{key}' must be numeric.");
            return false;
        }

        if (double.IsNaN(parsed) || double.IsInfinity(parsed) || parsed < min || parsed > max)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Argument '--{key}' must be in [{min}, {max}].");
            return false;
        }

        value = parsed;
        return true;
    }

    public static bool TryGetStringArg(
        IReadOnlyDictionary<string, string>? args,
        string key,
        out string value,
        out RuntimeQueryResponseEnvelope? error)
    {
        value = string.Empty;
        error = null;

        if (args is null || !args.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        if (value.Length == 0)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Argument '--{key}' cannot be empty.");
            return false;
        }

        return true;
    }

    public static bool TryGetBoolArg(
        IReadOnlyDictionary<string, string>? args,
        string key,
        out bool value,
        out RuntimeQueryResponseEnvelope? error)
    {
        value = false;
        error = null;

        if (!TryGetStringArg(args, key, out var raw, out var stringError))
        {
            error = stringError;
            return false;
        }

        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        error = RuntimeQueryResponseEnvelope.Failure(
            code: "INVALID_ARGUMENTS",
            message: $"Argument '--{key}' must be true/false (or 1/0, on/off).");
        return false;
    }
}
