// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

public static class NotchExportVersionPolicy
{
    public readonly record struct VersionCount(NotchAlgorithmVersion Version, int Count);

    public static IReadOnlyList<VersionCount> BuildCatalog(
        IReadOnlyList<NotchAlgorithmVersion> rowVersions,
        IReadOnlyList<NotchAlgorithmVersion?> pinnedVersions)
    {
        var countsByVersion = rowVersions
            .GroupBy(version => version)
            .ToDictionary(group => group.Key, group => group.Count());
        var versions = new SortedSet<NotchAlgorithmVersion>(countsByVersion.Keys);
        foreach (var pinnedVersion in pinnedVersions)
        {
            if (pinnedVersion.HasValue)
            {
                versions.Add(pinnedVersion.Value);
            }
        }

        var catalog = new List<VersionCount>();
        foreach (var version in versions)
        {
            countsByVersion.TryGetValue(version, out var count);
            catalog.Add(new VersionCount(version, count));
        }

        return catalog;
    }
}
