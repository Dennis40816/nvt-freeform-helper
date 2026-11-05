// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;

namespace FreeformHelper.UI.UseCases.ProjectLoad;

// Owned by one project load and mutated only by its UI-thread replay.
internal sealed class DxfCombinedGroupReplayResult
{
    public HashSet<int> CombinedSourceCadIds { get; } = [];
    public List<int> SkippedGroupIds { get; } = [];

    public void ReportSkippedGroups(Action<string> reportWarning, Action markUnsaved)
    {
        ArgumentNullException.ThrowIfNull(reportWarning);
        ArgumentNullException.ThrowIfNull(markUnsaved);
        if (SkippedGroupIds.Count == 0)
        {
            return;
        }

        var ids = string.Join(", ", SkippedGroupIds.Take(5).Select(static id => id.ToString(CultureInfo.InvariantCulture)));
        var remaining = SkippedGroupIds.Count - 5;
        var suffix = remaining > 0 ? string.Create(CultureInfo.InvariantCulture, $" and {remaining} more") : string.Empty;
        markUnsaved();
        reportWarning($"Warning: Combined DXF groups skipped: {ids}{suffix}. Saving drops these groups.");
    }
}
