// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

public static class NotchExportRowSelectionPolicy
{
    public readonly record struct RowChoice(int Ordinal, NotchTableRow Row, bool IsChecked);

    public static bool MatchesVersion(NotchAlgorithmVersion rowVersion, NotchAlgorithmVersion? scope)
    {
        return !scope.HasValue || rowVersion == scope.Value;
    }

    public static bool IsEligible(bool isChecked, NotchAlgorithmVersion rowVersion, NotchAlgorithmVersion? scope)
    {
        return isChecked && MatchesVersion(rowVersion, scope);
    }

    public static NotchTable Build(IReadOnlyList<RowChoice> rows, NotchAlgorithmVersion? scope)
    {
        var selected = rows
            .Where(row => IsEligible(row.IsChecked, row.Row.Version, scope))
            .OrderBy(row => row.Ordinal)
            .Select(row => row.Row)
            .ToList();
        return new NotchTable(selected);
    }
}
