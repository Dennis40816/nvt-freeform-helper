// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.UseCases.ProjectLoad;

// Owned by one project load and mutated only by its UI-thread replay.
internal sealed class DxfCombinedGroupReplayResult
{
    public HashSet<int> CombinedSourceCadIds { get; } = [];

    // The saved snapshots of the groups that this load could not restore. A project save writes them back unchanged.
    public List<ProjectDxfCombinedCadGroup> SkippedGroups { get; } = [];

    // Set when the project's DXF path did not work and the copy stored in the project was used instead.
    public string? CadFallbackWarning { get; set; }

    public void ReportWarnings(Action<string> reportWarning, Action<string> logWarning)
    {
        ArgumentNullException.ThrowIfNull(reportWarning);
        ArgumentNullException.ThrowIfNull(logWarning);
        if (CadFallbackWarning is not null)
        {
            logWarning(CadFallbackWarning);
            reportWarning(CadFallbackWarning);
        }

        ReportSkippedGroups(reportWarning);
    }

    private void ReportSkippedGroups(Action<string> reportWarning)
    {
        if (SkippedGroups.Count == 0)
        {
            return;
        }

        var ids = string.Join(", ", SkippedGroups.Take(5).Select(static group => group.GroupId.ToString(CultureInfo.InvariantCulture)));
        var remaining = SkippedGroups.Count - 5;
        var suffix = remaining > 0 ? string.Create(CultureInfo.InvariantCulture, $" and {remaining} more") : string.Empty;
        reportWarning($"Warning: Combined DXF groups could not be restored: {ids}{suffix}. They stay in the project file.");
    }
}
