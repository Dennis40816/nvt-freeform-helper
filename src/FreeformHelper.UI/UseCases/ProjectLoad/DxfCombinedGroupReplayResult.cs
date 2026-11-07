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

    // Set when the project's DXF path did not work, including when no embedded copy is available.
    public string? CadFallbackWarning { get; set; }

    // One status bar message for everything this load wants to say, so one warning never hides the other.
    public void ReportWarnings(Action<string> reportWarning, Action<string> logWarning)
    {
        ArgumentNullException.ThrowIfNull(reportWarning);
        ArgumentNullException.ThrowIfNull(logWarning);
        if (CadFallbackWarning is not null)
        {
            logWarning(CadFallbackWarning);
        }

        var skippedText = BuildSkippedGroupsText();
        if (CadFallbackWarning is null && skippedText is null)
        {
            return;
        }

        reportWarning(skippedText is null
            ? CadFallbackWarning!
            : CadFallbackWarning is null ? $"Warning: {skippedText}" : $"{CadFallbackWarning} {skippedText}");
    }

    private string? BuildSkippedGroupsText()
    {
        if (SkippedGroups.Count == 0)
        {
            return null;
        }

        var ids = string.Join(", ", SkippedGroups.Take(5).Select(static group => group.GroupId.ToString(CultureInfo.InvariantCulture)));
        var remaining = SkippedGroups.Count - 5;
        var suffix = remaining > 0 ? string.Create(CultureInfo.InvariantCulture, $" and {remaining} more") : string.Empty;
        return $"Combined DXF groups could not be restored: {ids}{suffix}. They stay in the project file.";
    }
}
